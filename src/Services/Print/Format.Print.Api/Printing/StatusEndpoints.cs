using System.Security.Claims;
using Format.Print.Api.Cups;
using Format.Print.Api.Data;
using Format.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Format.Print.Api.Printing;

public sealed class PickupOptions
{
    /// <summary>Где забирать напечатанное: «Корпус Б, 1 этаж, каб. 112». Пусто - блок не показывается.</summary>
    public string Location { get; set; } = "";
    public string Hours { get; set; } = "";
}

public sealed record QueueSummaryDto(int Waiting, int Printing);
public sealed record PickupDto(string Location, string Hours);
public sealed record PrintStatusDto(
    IReadOnlyList<PrinterStatusDto> Printers,
    QueueSummaryDto Queue,
    PickupDto? Pickup,
    bool PunchAvailable);
public sealed record PrintSummaryDto(int Active, int CompletedSince, double RollMmSince);

public static class StatusEndpoints
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/print");

        group.MapGet("/status", GetStatus);
        group.MapGet("/summary", GetSummary);

        return app;
    }

    /// <summary>Общая картина: принтеры, очередь всех пользователей, место выдачи.</summary>
    private static async Task<IResult> GetStatus(
        PrinterStatusService printers,
        PrintDbContext db,
        IOptions<PickupOptions> pickupOptions,
        CupsClient cups,
        CancellationToken ct)
    {
        var counts = await db.Orders
            .Where(o => o.Status == OrderStatus.Queued
                     || o.Status == OrderStatus.Processing
                     || o.Status == OrderStatus.Printing)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(OrderStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        var queue = new QueueSummaryDto(
            Waiting: CountOf(OrderStatus.Queued) + CountOf(OrderStatus.Processing),
            Printing: CountOf(OrderStatus.Printing));

        var pickup = pickupOptions.Value;

        return Results.Ok(new PrintStatusDto(
            await printers.GetAsync(ct),
            queue,
            string.IsNullOrWhiteSpace(pickup.Location) ? null : new PickupDto(pickup.Location, pickup.Hours),
            cups.PunchAvailable));
    }

    /// <summary>Личная сводка: сколько в работе, сколько напечатано и какой расход с момента since.</summary>
    private static async Task<IResult> GetSummary(
        ClaimsPrincipal user,
        PrintDbContext db,
        CancellationToken ct,
        DateTimeOffset? since = null)
    {
        var userId = user.GetUserId();
        var from = (since ?? DateTimeOffset.UtcNow.AddDays(-30)).ToUniversalTime();

        var mine = db.Orders.Where(o => o.CreatedById == userId);

        var active = await mine.CountAsync(o =>
            o.Status == OrderStatus.Queued ||
            o.Status == OrderStatus.Processing ||
            o.Status == OrderStatus.Printing, ct);

        var completed = mine.Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= from);

        return Results.Ok(new PrintSummaryDto(
            active,
            await completed.CountAsync(ct),
            await completed.SumAsync(o => o.TotalRollLengthMm, ct)));
    }
}