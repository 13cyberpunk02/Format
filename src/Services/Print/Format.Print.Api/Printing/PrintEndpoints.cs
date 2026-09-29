using System.Security.Claims;
using Format.Layout;
using Format.Print.Api.Cups;
using Format.Print.Api.Data;
using Format.Print.Api.Storage;
using Format.Security;
using Microsoft.EntityFrameworkCore;

namespace Format.Print.Api.Printing;

public static class PrintEndpoints
{
    public const int MaxCopies = 100;
    public const int MaxItems = 500;

    /// <summary>Заказ после проверок: строки без повторов, сведения о чертежах и раскладка.</summary>
    private sealed record ResolvedRequest(
        IReadOnlyList<PrintItemRequest> Items,
        Dictionary<Guid, StorageDrawing> Drawings,
        PrintPlan Plan);

    public static IEndpointRouteBuilder MapPrintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/print");
        var admin = app.MapGroup("/print/admin").RequireAuthorization(FormatAuthentication.AdminPolicy);
        
        admin.MapGet("/orders", ListAllOrders);
        group.MapPost("/preview", Preview);
        group.MapPost("/orders", CreateOrder);
        group.MapPost("/orders/{id:guid}/cancel", CancelOrder);
        group.MapGet("/orders", ListMyOrders);
        group.MapGet("/orders/{id:guid}", GetOrder);
        group.MapPost("/orders/{id:guid}/retry", RetryOrder);

        return app;
    }

    private static async Task<IResult> ListAllOrders(
        PrintDbContext db,
        CancellationToken ct,
        int page = 1,
        int pageSize = 50,
        OrderStatus? status = null,
        Guid? userId = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Orders.AsNoTracking();

        if (status is not null)
            query = query.Where(o => o.Status == status);

        if (userId is not null)
            query = query.Where(o => o.CreatedById == userId);

        var total = await query.CountAsync(ct);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<OrderDto>([.. orders.Select(OrderDto.From)], total, page, pageSize));
    }
    
    private static async Task<IResult> Preview(
        PrintRequest request, StorageClient storage, LayoutPlanner planner, CancellationToken ct)
    {
        var (resolved, error) = await ResolveAsync(request, storage, planner, ct);

        return error ?? Results.Ok(PreviewDto.From(resolved!.Plan, resolved.Drawings));
    }

    private static async Task<IResult> CreateOrder(
        PrintRequest request,
        ClaimsPrincipal user,
        StorageClient storage,
        LayoutPlanner planner,
        PrintDbContext db,
        Spool spool,
        TimeProvider time,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var (resolved, error) = await ResolveAsync(request, storage, planner, ct);
        if (error is not null)
            return error;

        var orderId = Guid.NewGuid();
        Directory.CreateDirectory(spool.OrderDirectory(orderId));

        // 1. Копируем файлы в спул - пока пользователь здесь и его токен действителен
        try
        {
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct };

            await Parallel.ForEachAsync(resolved!.Drawings.Keys, parallel, async (drawingId, token) =>
                await storage.DownloadAsync(drawingId, spool.DrawingFile(orderId, drawingId), token));
        }
        catch (HttpRequestException ex)
        {
            spool.DeleteOrder(orderId);
            loggerFactory.CreateLogger("Orders").LogError(ex, "Не удалось скачать файлы для заказа {OrderId}", orderId);

            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Не удалось получить файлы чертежей из хранилища. Попробуйте ещё раз.");
        }
        catch
        {
            spool.DeleteOrder(orderId);
            throw;
        }

        // 2. Сохраняем заказ - с этого момента его видит обработчик очереди
        var order = new PrintOrder
        {
            Id = orderId,
            Status = OrderStatus.Queued,
            CreatedById = user.GetUserId(),
            CreatedByName = user.GetDisplayName(),
            CreatedAt = time.GetUtcNow(),
            TotalRollLengthMm = resolved.Plan.TotalRollLength,
            SheetCount = resolved.Plan.Sheets.Count,
            OfficeJobCount = resolved.Plan.OfficeJobs.Count,
            Items =
            [
                .. resolved.Items.Select((item, index) =>
                {
                    var drawing = resolved.Drawings[item.DrawingId];
                    return new PrintOrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        Position = index,
                        DrawingId = item.DrawingId,
                        FileName = drawing.FileName,
                        PageNumber = drawing.PageNumber,
                        Format = drawing.Format,
                        Copies = item.Copies,
                    };
                })
            ],
        };

        db.Orders.Add(order);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            spool.DeleteOrder(orderId);
            throw;
        }

        return Results.Created($"/print/orders/{order.Id}", OrderDto.From(order));
    }
    
    private static async Task<IResult> RetryOrder(
        Guid id, ClaimsPrincipal user, PrintDbContext db, Spool spool, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return Results.NotFound();

        if (order.CreatedById != user.GetUserId() && !user.IsAdmin())
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Это чужой заказ.");

        if (order.Status != OrderStatus.Failed)
            return Conflict("Повторить можно только заказ, завершившийся ошибкой.");

        if (!Directory.Exists(spool.OrderDirectory(id)))
            return Conflict("Файлы заказа уже удалены. Создайте заказ заново.");

        // Два изменения должны случиться вместе - открываем транзакцию явно
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var updated = await db.Orders
            .Where(o => o.Id == id && o.Status == OrderStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Queued)
                .SetProperty(o => o.Error, (string?)null)
                .SetProperty(o => o.StartedAt, (DateTimeOffset?)null)
                .SetProperty(o => o.CompletedAt, (DateTimeOffset?)null), ct);

        if (updated == 0)
            return Conflict("Заказ уже повторяется.");

        await db.Jobs
            .Where(j => j.OrderId == id && (j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, JobStatus.Replaced), ct);

        await transaction.CommitAsync(ct);

        return Results.Accepted($"/print/orders/{id}");
    }

    private static async Task<IResult> ListMyOrders(
        ClaimsPrincipal user,
        PrintDbContext db,
        CancellationToken ct,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var userId = user.GetUserId();
        var query = db.Orders.AsNoTracking().Where(o => o.CreatedById == userId);

        var total = await query.CountAsync(ct);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<OrderDto>(orders.Select(OrderDto.From).ToList(), total, page, pageSize));
    }

    private static async Task<IResult> GetOrder(Guid id, ClaimsPrincipal user, PrintDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Jobs)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return Results.NotFound();

        if (order.CreatedById != user.GetUserId() && !user.IsAdmin())
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Это чужой заказ.");

        return Results.Ok(OrderDto.From(order));
    }

    /// <summary>Общие проверки и раскладка для предпросмотра и заказа. Возвращает либо результат, либо ошибку.</summary>
    private static async Task<(ResolvedRequest? Resolved, IResult? Error)> ResolveAsync(
        PrintRequest request, StorageClient storage, LayoutPlanner planner, CancellationToken ct)
    {
        var raw = request.Items ?? [];

        if (raw.Count == 0)
            return (null, BadRequest("Выберите хотя бы один чертёж."));

        if (raw.Count > MaxItems)
            return (null, BadRequest($"Слишком много чертежей в одном заказе (максимум {MaxItems})."));

        // Один чертёж в нескольких строках - складываем копии, сохраняя порядок первого появления
        var items = raw
            .GroupBy(i => i.DrawingId)
            .Select(g => new PrintItemRequest(g.Key, g.Sum(i => i.Copies)))
            .ToList();

        if (items.Any(i => i.Copies is < 1 or > MaxCopies))
            return (null, BadRequest($"Количество копий должно быть от 1 до {MaxCopies}."));

        var found = await storage.LookupAsync(items.Select(i => i.DrawingId).ToList(), ct);
        var drawings = found.ToDictionary(d => d.Id);

        var missing = items.Where(i => !drawings.ContainsKey(i.DrawingId)).Select(i => i.DrawingId).ToList();
        if (missing.Count > 0)
            return (null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Некоторые чертежи не найдены - возможно, их удалили.",
                extensions: new Dictionary<string, object?> { ["missingDrawingIds"] = missing }));

        var plan = planner.Plan(items.Select(i =>
            new PrintItem(i.DrawingId, FormatCatalog.Get(drawings[i.DrawingId].Format), i.Copies)));

        return (new ResolvedRequest(items, drawings, plan), null);
    }
    
    private static async Task<IResult> CancelOrder(
        Guid id,
        ClaimsPrincipal user,
        PrintDbContext db,
        CupsClient cups,
        Spool spool,
        TimeProvider time,
        CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Jobs)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return Results.NotFound();

        if (order.CreatedById != user.GetUserId() && !user.IsAdmin())
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Это чужой заказ.");

        var now = time.GetUtcNow();

        switch (order.Status)
        {
            case OrderStatus.Queued:
            {
                // Условный UPDATE: обработчик мог забрать заказ в эту же долю секунды
                var updated = await db.Orders
                    .Where(o => o.Id == id && o.Status == OrderStatus.Queued)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(o => o.Status, OrderStatus.Cancelled)
                        .SetProperty(o => o.CompletedAt, (DateTimeOffset?)now), ct);

                if (updated == 0)
                    return Conflict("Заказ уже начал отправляться на печать. Попробуйте отменить через несколько секунд.");

                spool.DeleteOrder(id);
                return Results.NoContent();
            }

            case OrderStatus.Processing:
                return Conflict("Заказ сейчас отправляется на печать. Попробуйте отменить через несколько секунд.");

            case OrderStatus.Printing:
            {
                foreach (var job in order.Jobs.Where(j => j.Status == JobStatus.Pending))
                {
                    var cancelled = await cups.CancelJobAsync(job.CupsJobId, ct);

                    job.Status = cancelled ? JobStatus.Cancelled : JobStatus.Completed;
                    job.CompletedAt = now;
                    job.StateMessage = null;
                }

                order.Status = order.Jobs.Any(j => j.Status == JobStatus.Cancelled)
                    ? OrderStatus.Cancelled
                    : OrderStatus.Completed;
                order.CompletedAt = now;

                await db.SaveChangesAsync(ct);
                spool.DeleteOrder(id);

                return Results.NoContent();
            }

            default:
                return Conflict("Заказ уже завершён.");
        }
    }

    private static IResult Conflict(string title) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: title);
    private static IResult BadRequest(string title) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
}