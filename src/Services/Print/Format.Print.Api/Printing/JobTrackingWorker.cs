namespace Format.Print.Api.Printing;

public sealed class JobTrackingWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<JobTrackingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<JobTracker>().SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при отслеживании заданий печати");
            }

            await Task.Delay(Interval, time, stoppingToken);
        }
    }
}