using Format.Layout;
using Format.Print.Api.Storage;

namespace Format.Print.Api.Printing;

public static class PrintEndpoints
{
    public const int MaxCopies = 100;
    public const int MaxItems = 500;

    public static IEndpointRouteBuilder MapPrintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/print");

        group.MapPost("/preview", Preview);

        return app;
    }

    private static async Task<IResult> Preview(
        PreviewRequest request,
        StorageClient storage,
        LayoutPlanner planner,
        CancellationToken ct)
    {
        var items = request.Items ?? [];

        if (items.Count == 0)
            return BadRequest("Выберите хотя бы один чертёж.");

        if (items.Count > MaxItems)
            return BadRequest($"Слишком много чертежей в одном заказе (максимум {MaxItems}).");

        if (items.Any(i => i.Copies is < 1 or > MaxCopies))
            return BadRequest($"Количество копий должно быть от 1 до {MaxCopies}.");

        var ids = items.Select(i => i.DrawingId).Distinct().ToList();
        var found = await storage.LookupAsync(ids, ct);
        var drawings = found.ToDictionary(d => d.Id);

        var missing = ids.Where(id => !drawings.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Некоторые чертежи не найдены - возможно, их удалили.",
                extensions: new Dictionary<string, object?> { ["missingDrawingIds"] = missing });

        var plan = planner.Plan(items.Select(i =>
            new PrintItem(i.DrawingId, FormatCatalog.Get(drawings[i.DrawingId].Format), i.Copies)));

        return Results.Ok(PreviewDto.From(plan, drawings));
    }

    private static IResult BadRequest(string title) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
}