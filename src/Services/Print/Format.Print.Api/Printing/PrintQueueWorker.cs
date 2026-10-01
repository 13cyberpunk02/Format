namespace Format.Print.Api.Printing;

/// <summary>Фоновый цикл: забирает заказы из очереди и отдаёт их OrderProcessor.</summary>
public sealed class PrintQueueWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<PrintQueueWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverWithRetryAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = false;
                await RunInScopeAsync(async p => processed = await p.ProcessNextAsync(stoppingToken));

                // Очередь пуста - ждём. Был заказ - сразу проверяем следующий.
                if (!processed)
                    await Task.Delay(IdleDelay, time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Например, база недоступна. Не падаем, а пробуем снова через паузу.
                logger.LogError(ex, "Ошибка в обработчике очереди печати");
                await Task.Delay(ErrorDelay, time, stoppingToken);
            }
        }
    }

    /// <summary>Каждый заказ - в своей области DI: свой DbContext, свежий на каждый заказ.</summary>
    private async Task RunInScopeAsync(Func<OrderProcessor, Task> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<OrderProcessor>());
    }
    
    /// <summary>База может быть ещё не готова (например, сразу после перезагрузки сервера) - пробуем, пока не получится.</summary>
    private async Task RecoverWithRetryAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunInScopeAsync(p => p.RecoverInterruptedAsync(stoppingToken));
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не удалось проверить прерванные заказы, повторим через {Delay}", ErrorDelay);
                await Task.Delay(ErrorDelay, time, stoppingToken);
            }
        }
    }
}