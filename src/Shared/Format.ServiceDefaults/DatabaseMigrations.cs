using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Format.ServiceDefaults;

public static class DatabaseMigrations
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Применяет миграции при запуске. Ждёт до минуты, пока база станет доступна,
    /// потом сдаётся - и Docker перезапустит контейнер.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();

                var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                if (pending.Count > 0)
                {
                    logger.LogInformation("Применяем миграции: {Migrations}", string.Join(", ", pending));
                    await db.Database.MigrateAsync();
                }

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning("База пока недоступна (попытка {Attempt} из {Max}): {Error}",
                    attempt, MaxAttempts, ex.Message);
                await Task.Delay(RetryDelay);
            }
        }
    }
}