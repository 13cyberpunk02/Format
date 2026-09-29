using Format.Print.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Format.Print.Api.Printing;

public sealed class SpoolCleaner(
    PrintDbContext db,
    Spool spool,
    IOptions<SpoolOptions> options,
    TimeProvider time,
    ILogger<SpoolCleaner> logger)
{
    /// <summary>Папку без заказа не трогаем час: заказ может как раз создаваться.</summary>
    private static readonly TimeSpan OrphanGrace = TimeSpan.FromHours(1);

    public async Task CleanAsync(CancellationToken ct)
    {
        var directories = spool.ListOrderDirectories();
        if (directories.Count == 0)
            return;

        var ids = directories.Select(d => d.OrderId).ToList();

        var orders = await db.Orders
            .AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.Status, o.CompletedAt })
            .ToDictionaryAsync(o => o.Id, ct);

        var now = time.GetUtcNow();
        var failedBefore = now.AddDays(-options.Value.FailedRetentionDays);

        foreach (var (orderId, lastWrite) in directories)
        {
            string? reason = orders.TryGetValue(orderId, out var order)
                ? order.Status switch
                {
                    OrderStatus.Completed or OrderStatus.Cancelled => "заказ завершён",
                    OrderStatus.Failed when order.CompletedAt < failedBefore => "истёк срок хранения упавшего заказа",
                    _ => null,
                }
                : lastWrite < now - OrphanGrace ? "заказа нет в базе" : null;

            if (reason is null)
                continue;

            spool.DeleteOrder(orderId);
            logger.LogInformation("Спул заказа {OrderId} удалён: {Reason}", orderId, reason);
        }
    }
}