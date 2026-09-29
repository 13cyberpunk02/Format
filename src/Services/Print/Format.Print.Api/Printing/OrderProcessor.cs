using Format.Layout;
using Format.Print.Api.Composition;
using Format.Print.Api.Cups;
using Format.Print.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Format.Print.Api.Printing;

public sealed class OrderProcessor(
    PrintDbContext db,
    LayoutPlanner planner,
    SheetComposer composer,
    CupsClient cups,
    Spool spool,
    TimeProvider time,
    ILogger<OrderProcessor> logger)
{
    /// <summary>Заказы, брошенные на середине при остановке сервиса, - в Failed (см. объяснение выше).</summary>
    public async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        var count = await db.Orders
            .Where(o => o.Status == OrderStatus.Processing)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Failed)
                .SetProperty(o => o.Error,
                    "Обработка прервана перезапуском сервиса печати. Проверьте, что уже напечаталось, и при необходимости повторите заказ.")
                .SetProperty(o => o.CompletedAt, time.GetUtcNow()), ct);

        if (count > 0)
            logger.LogWarning("Заказов, прерванных при остановке сервиса: {Count}. Они помечены как Failed.", count);
    }

    /// <summary>Взять и обработать следующий заказ. false - очередь пуста.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var orderId = await ClaimNextAsync(ct);
        if (orderId is null)
            return false;

        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Jobs)
            .FirstAsync(o => o.Id == orderId, ct);

        logger.LogInformation("Обработка заказа {OrderId} от {User}", order.Id, order.CreatedByName);

        try
        {
            await SendToPrintersAsync(order, ct);

            order.Status = OrderStatus.Printing;
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogInformation("Заказ {OrderId}: отправлено заданий в CUPS - {Count}", order.Id, order.Jobs.Count);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            logger.LogError(ex, "Заказ {OrderId} не удалось обработать", order.Id);

            var error = ex switch
            {
                CupsException cupsError => cupsError.Message,
                HttpRequestException => "Сервер печати (CUPS) недоступен.",
                _ => "Не удалось подготовить или отправить печать. Подробности - в журнале сервиса печати.",
            };
            
            await db.Orders
                .Where(o => o.Id == order.Id)
                .ExecuteUpdateAsync(s => s
                        .SetProperty(o => o.Status, OrderStatus.Failed)
                        .SetProperty(o => o.Error, error)
                        .SetProperty(o => o.CompletedAt, (DateTimeOffset?)time.GetUtcNow()),
                    CancellationToken.None);
        }

        return true;
    }

    private async Task<Guid?> ClaimNextAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();

        var ids = await db.Database.SqlQuery<Guid>($"""
            UPDATE orders
            SET status = 'Processing', started_at = {now}
            WHERE id = (
                SELECT id FROM orders
                WHERE status = 'Queued'
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1)
            RETURNING id AS "Value"
            """).ToListAsync(ct);

        return ids.Count == 0 ? null : ids[0];
    }

    private async Task SendToPrintersAsync(PrintOrder order, CancellationToken ct)
    {
        var files = order.Items.ToDictionary(i => i.DrawingId, i => spool.DrawingFile(order.Id, i.DrawingId));

        var missing = files.Values.Count(path => !File.Exists(path));
        if (missing > 0)
            throw new InvalidOperationException($"В спуле заказа {order.Id} не хватает файлов: {missing}.");

        // Раскладка заново - по строкам заказа, без обращения к сервису хранения
        var plan = planner.Plan(order.Items.Select(i =>
            new PrintItem(i.DrawingId, FormatCatalog.Get(i.Format), i.Copies)));

        var items = order.Items.ToDictionary(i => i.DrawingId);
        var orderLabel = order.Id.ToString("N")[..8];

        // При повторе: всё, что уже напечатано или стоит в очереди CUPS, второй раз не отправляем
        var alreadySent = order.Jobs
            .Where(j => j.Status is JobStatus.Completed or JobStatus.Pending)
            .Select(j => j.Key)
            .ToHashSet();

        var skipped = 0;
        
        // Офисный принтер: файл из спула как есть
        foreach (var office in plan.OfficeJobs)
        {
            var key = $"office:{office.DrawingId}";
            if (alreadySent.Contains(key))
            {
                skipped++;
                continue;
            }

            var item = items[office.DrawingId];
            var description = $"{item.FileName}, лист {item.PageNumber} ({office.Format.Name})";

            await using var pdf = File.OpenRead(files[office.DrawingId]);
            var cupsJobId = await cups.PrintOfficeAsync(
                office.Format, pdf, office.Copies, $"Заказ {orderLabel}: {description}", ct);

            await RecordJobAsync(order, cupsJobId, key, "office", description, office.Copies);
        }

        // Плоттер: одинаковые листы - одним заданием с несколькими копиями
        var sheetGroups = plan.Sheets.GroupBy(SheetKey).ToList();
        var sheetsDirectory = Path.Combine(spool.OrderDirectory(order.Id), "sheets");
        Directory.CreateDirectory(sheetsDirectory);

        for (var n = 0; n < sheetGroups.Count; n++)
        {
            var key = $"sheet:{sheetGroups[n].Key}";
            if (alreadySent.Contains(key))
            {
                skipped++;
                continue;
            }

            var sheet = sheetGroups[n].First();
            var copies = sheetGroups[n].Count();
            var description = $"Лист {n + 1} из {sheetGroups.Count}: {Describe(sheet)}";
            var path = Path.Combine(sheetsDirectory, $"sheet-{n + 1:000}.pdf");

            await using (var output = File.Create(path))
            {
                composer.Compose(sheet, files, output);
            }

            try
            {
                await using var pdf = File.OpenRead(path);
                var cupsJobId = await cups.PrintPlotterSheetAsync(
                    sheet, pdf, copies, $"Заказ {orderLabel}: {description}", ct);

                await RecordJobAsync(order, cupsJobId, key, "plotter", description, copies);
            }
            finally
            {
                // CUPS уже сохранил себе копию задания
                File.Delete(path);
            }
        }
        if (skipped > 0)
            logger.LogInformation("Заказ {OrderId}: пропущено уже отправленных заданий — {Count}", order.Id, skipped);
    }

    /// <summary>Запомнить отправленное задание сразу - даже если следующий лист упадёт.</summary>
    private async Task RecordJobAsync(
        PrintOrder order, int cupsJobId, string key, string printer, string description, int copies)
    {
        db.Jobs.Add(new PrintJob
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            CupsJobId = cupsJobId,
            Key = key,
            Printer = printer,
            Description = description,
            Copies = copies,
            Status = JobStatus.Pending,
            CreatedAt = time.GetUtcNow(),
        });

        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static string SheetKey(PlotterSheet sheet) =>
        string.Join('|', sheet.Placements.Select(p => $"{p.DrawingId}:{p.X}:{p.Y}:{p.Width}:{p.Height}")) + $"|{sheet.Length}";

    private static string Describe(PlotterSheet sheet) =>
        $"{string.Join(" + ", sheet.Placements.Select(p => p.Format.Name))}, {sheet.Length:0} мм";
}