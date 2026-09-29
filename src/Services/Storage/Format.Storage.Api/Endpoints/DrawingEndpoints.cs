using System.Security.Claims;
using Format.Storage.Api.Data;
using Format.Storage.Api.Files;
using Format.Storage.Api.Pdf;
using Format.Layout;
using Format.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Format.Storage.Api.Endpoints;

public static class DrawingEndpoints
{
    public static IEndpointRouteBuilder MapDrawingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/drawings");

        group.MapPost("/", Upload).DisableAntiforgery();
        group.MapPost("/lookup", Lookup);
        group.MapGet("/", List);
        group.MapGet("/formats", GetFormats);
        group.MapGet("/{id:guid}", GetById);
        group.MapGet("/{id:guid}/file", Download);
        group.MapDelete("/{id:guid}", Delete);
        group.MapGet("/uploads/{uploadId:guid}", GetUpload);
        group.MapDelete("/uploads/{uploadId:guid}", DeleteUpload);

        return app;
    }

    private static async Task<IResult> Upload(
        IFormFile file,
        ClaimsPrincipal user,
        StorageDbContext db,
        IDrawingFileStore files,
        PdfPageSplitter splitter,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (file.Length == 0 || !await HasPdfSignatureAsync(file, ct))
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Это не PDF",
                detail: "Чертежи принимаются только в формате PDF.");

        var uploadId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;
        var fileName = Path.GetFileName(file.FileName);
        var created = new List<Drawing>();
        var rejected = new List<RejectedPageDto>();

        try
        {
            await using var stream = file.OpenReadStream();

            await splitter.SplitAsync(stream, async page =>
            {
                if (!page.Check.IsValid)
                {
                    rejected.Add(new RejectedPageDto(page.PageNumber, page.Check.Error!));
                    return;
                }

                var drawing = new Drawing
                {
                    Id = Guid.NewGuid(),
                    UploadId = uploadId,
                    PageNumber = page.PageNumber,
                    PageCount = page.PageCount,
                    FileName = fileName,
                    FormatName = page.Check.Format!.Name,
                    WidthMm = page.WidthMm,
                    HeightMm = page.HeightMm,
                    SizeBytes = page.Content!.Length,
                    UploadedById = user.GetUserId(),
                    UploadedByName = user.GetDisplayName(),
                    UploadedAt = uploadedAt,
                };

                await files.SaveAsync(drawing.Id, page.Content, ct);
                created.Add(drawing);
            }, ct);
        }
        catch (InvalidPdfException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Некорректный PDF",
                detail: ex.Message);
        }
        catch
        {
            // Что-то пошло не так посередине: убираем уже загруженные страницы
            await DeleteFilesAsync(files, created.Select(d => d.Id), loggerFactory);
            throw;
        }

        if (created.Count == 0)
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Нет подходящих страниц",
                detail: "Ни одна страница файла не может быть напечатана.",
                extensions: new Dictionary<string, object?> { ["rejectedPages"] = rejected });

        db.Drawings.AddRange(created);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteFilesAsync(files, created.Select(d => d.Id), loggerFactory);
            throw;
        }

        return Results.Created(
            $"/drawings/uploads/{uploadId}",
            new UploadResultDto(uploadId, created.Select(DrawingDto.From).ToList(), rejected));
    }
    
    /// <summary>Сведения о нескольких чертежах одним запросом. Отсутствующие просто не попадут в ответ.</summary>
    private static async Task<IResult> Lookup(LookupRequest request, StorageDbContext db, CancellationToken ct)
    {
        var ids = request.Ids?.Distinct().ToList() ?? [];

        if (ids.Count > 500)
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Слишком много чертежей в одном запросе (максимум 500).");

        if (ids.Count == 0)
            return Results.Ok(Array.Empty<DrawingDto>());

        var drawings = await db.Drawings
            .AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(ct);

        return Results.Ok(drawings.Select(DrawingDto.From).ToList());
    }

    /// <summary>Настоящий PDF всегда начинается с "%PDF-". Проверяем содержимое, а не расширение файла.</summary>
    private static async Task<bool> HasPdfSignatureAsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[5];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        return read == header.Length && header.AsSpan().SequenceEqual("%PDF-"u8);
    }

    private static async Task DeleteFilesAsync(
        IDrawingFileStore files, IEnumerable<Guid> drawingIds, ILoggerFactory loggerFactory)
    {
        foreach (var id in drawingIds)
        {
            try
            {
                await files.DeleteAsync(id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Drawings")
                    .LogWarning(ex, "Не удалось удалить файл чертежа {Id}", id);
            }
        }
    }
    
     private static async Task<IResult> List(
        StorageDbContext db,
        CancellationToken ct,
        int page = 1,
        int pageSize = 50,
        string? format = null,
        string? search = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Drawings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(format))
        {
            var known = FormatCatalog.All.FirstOrDefault(f =>
                string.Equals(f.Name, format, StringComparison.OrdinalIgnoreCase));

            if (known is null)
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Неизвестный формат",
                    detail: $"Формат «{format}» не поддерживается.");

            query = query.Where(d => d.FormatName == known.Name);
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => EF.Functions.ILike(d.FileName, $"%{search}%"));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.UploadedAt)
            .ThenBy(d => d.UploadId)
            .ThenBy(d => d.PageNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<DrawingDto>(
            items.Select(DrawingDto.From).ToList(), total, page, pageSize));
    }

    private static async Task<IResult> GetById(Guid id, StorageDbContext db, CancellationToken ct)
    {
        var drawing = await db.Drawings.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

        return drawing is null ? Results.NotFound() : Results.Ok(DrawingDto.From(drawing));
    }

    private static async Task<IResult> GetUpload(Guid uploadId, StorageDbContext db, CancellationToken ct)
    {
        var pages = await db.Drawings
            .AsNoTracking()
            .Where(d => d.UploadId == uploadId)
            .OrderBy(d => d.PageNumber)
            .ToListAsync(ct);

        return pages.Count == 0 ? Results.NotFound() : Results.Ok(pages.Select(DrawingDto.From).ToList());
    }

    private static async Task<IResult> Download(
        Guid id,
        StorageDbContext db,
        IDrawingFileStore files,
        HttpContext http,
        ILoggerFactory loggerFactory,
        CancellationToken ct,
        bool download = false)
    {
        var drawing = await db.Drawings.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (drawing is null)
            return Results.NotFound();

        var stream = await files.OpenReadAsync(id, ct);
        if (stream is null)
        {
            loggerFactory.CreateLogger("Drawings")
                .LogError("Чертёж {Id} есть в базе, но его файла нет в хранилище", id);

            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Файл не найден",
                detail: "Файл чертежа отсутствует в хранилище.");
        }

        var disposition = new ContentDispositionHeaderValue(download ? "attachment" : "inline")
        {
            FileNameStar = DownloadName(drawing),
        };
        http.Response.Headers.ContentDisposition = disposition.ToString();

        return Results.Stream(stream, "application/pdf");
    }

    private static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal user,
        StorageDbContext db,
        IDrawingFileStore files,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var ownerId = await db.Drawings
            .Where(d => d.Id == id)
            .Select(d => (Guid?)d.UploadedById)
            .FirstOrDefaultAsync(ct);

        if (ownerId is null)
            return Results.NotFound();

        if (ownerId != user.GetUserId() && !user.IsAdmin())
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Нельзя удалить чужой чертёж");

        await db.Drawings.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        await DeleteFilesAsync(files, [id], loggerFactory);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteUpload(
        Guid uploadId,
        ClaimsPrincipal user,
        StorageDbContext db,
        IDrawingFileStore files,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var pages = await db.Drawings
            .Where(d => d.UploadId == uploadId)
            .Select(d => new { d.Id, d.UploadedById })
            .ToListAsync(ct);

        if (pages.Count == 0)
            return Results.NotFound();

        var userId = user.GetUserId();
        if (pages.Any(p => p.UploadedById != userId) && !user.IsAdmin())
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Нельзя удалить чужой комплект");

        await db.Drawings.Where(d => d.UploadId == uploadId).ExecuteDeleteAsync(ct);
        await DeleteFilesAsync(files, pages.Select(p => p.Id), loggerFactory);

        return Results.NoContent();
    }

    private static IEnumerable<FormatDto> GetFormats(FormatValidator validator) =>
        FormatCatalog.All.Select(f =>
        {
            var check = validator.Check(f);
            return new FormatDto(
                f.Name, f.ShortSide, f.LongSide,
                f.IsOffice ? "office" : "plotter",
                check.IsValid, check.Error);
        });

    /// <summary>Имя файла при скачивании: для страницы комплекта - с номером листа и форматом.</summary>
    private static string DownloadName(Drawing drawing)
    {
        if (drawing.PageCount == 1)
            return drawing.FileName;

        var name = Path.GetFileNameWithoutExtension(drawing.FileName);
        return $"{name} (лист {drawing.PageNumber} из {drawing.PageCount}, {drawing.FormatName}).pdf";
    }
}