using Format.Print.Api.Cups;
using Format.Print.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Format.Print.Api.Printing;

/// <summary>Сверяет незавершённые задания с CUPS и подводит итог по заказам.</summary>
public sealed class JobTracker(
    PrintDbContext db,
    CupsClient cups,
    Spool spool,
    TimeProvider time,
    ILogger<JobTracker> logger)
{
    public async Task SyncAsync(CancellationToken ct)
    {
        await UpdateJobsAsync(ct);
        await FinalizeOrdersAsync(ct);
    }

    private async Task UpdateJobsAsync(CancellationToken ct)
    {
        var jobs = await db.Jobs.Where(j => j.Status == JobStatus.Pending).ToListAsync(ct);
        if (jobs.Count == 0)
            return;

        var now = time.GetUtcNow();

        foreach (var job in jobs)
        {
            CupsJobState? state;
            try
            {
                state = await cups.GetJobStateAsync(job.CupsJobId, ct);
            }
            catch (HttpRequestException ex)
            {
                // CUPS недоступен - ничего не меняем, спросим в следующий раз
                logger.LogWarning(ex, "CUPS недоступен, состояние заданий обновится позже");
                break;
            }

            if (state is null)
            {
                // CUPS уже забыл задание - значит, оно давно завершилось
                logger.LogWarning("Задание {CupsJobId} не найдено в CUPS, считаем выполненным", job.CupsJobId);
                job.Status = JobStatus.Completed;
                job.CompletedAt = now;
                job.StateMessage = null;
                continue;
            }

            job.StateMessage = state.IsFinal ? null : Describe(state);

            switch (state.State)
            {
                case CupsJobStates.Completed:
                    job.Status = JobStatus.Completed;
                    job.CompletedAt = now;
                    break;

                case CupsJobStates.Canceled:
                    job.Status = JobStatus.Cancelled;
                    job.CompletedAt = now;
                    break;

                case CupsJobStates.Aborted:
                    job.Status = JobStatus.Failed;
                    job.CompletedAt = now;
                    job.Error = $"Печать прервана: {Describe(state) ?? "причина не указана"}";
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task FinalizeOrdersAsync(CancellationToken ct)
    {
        // Заказы в печати, у которых не осталось незавершённых заданий
        var orders = await db.Orders
            .Include(o => o.Jobs)
            .Where(o => o.Status == OrderStatus.Printing && o.Jobs.All(j => j.Status != JobStatus.Pending))
            .ToListAsync(ct);

        foreach (var order in orders)
        {
            var failed = order.Jobs.Where(j => j.Status == JobStatus.Failed).ToList();

            if (failed.Count > 0)
            {
                order.Status = OrderStatus.Failed;
                order.Error = $"Не напечатано заданий: {failed.Count} из {order.Jobs.Count}. {failed[0].Error}";
            }
            else if (order.Jobs.Any(j => j.Status == JobStatus.Cancelled))
            {
                order.Status = OrderStatus.Cancelled;
            }
            else
            {
                order.Status = OrderStatus.Completed;
            }

            order.CompletedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);

            // Спул упавшего заказа оставляем - он понадобится для повтора
            if (order.Status != OrderStatus.Failed)
                spool.DeleteOrder(order.Id);

            logger.LogInformation("Заказ {OrderId} завершён: {Status}", order.Id, order.Status);
        }
    }

    /// <summary>Понятное описание: сначала сообщение принтера, иначе причины CUPS.</summary>
    private static string? Describe(CupsJobState state) =>
        !string.IsNullOrWhiteSpace(state.PrinterMessage) ? state.PrinterMessage
        : state.Reasons.Count > 0 ? string.Join(", ", state.Reasons)
        : null;
}