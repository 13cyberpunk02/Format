using Format.Print.Api.Cups;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Format.Print.Api.Printing;

public enum PrinterAvailability
{
    Idle,
    Printing,
    Stopped,
    NotAccepting,
    Unavailable,
}

public sealed record PrinterStatusDto(string Key, string Name, PrinterAvailability State, string? Message, int QueuedJobs);

/// <summary>Состояние принтеров для интерфейса. Кэшируется, чтобы не спрашивать CUPS на каждый запрос.</summary>
public sealed class PrinterStatusService(
    CupsClient cups,
    IOptions<CupsOptions> options,
    IMemoryCache cache,
    ILogger<PrinterStatusService> logger)
{
    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(15);

    public async Task<IReadOnlyList<PrinterStatusDto>> GetAsync(CancellationToken ct)
    {
        var result = await cache.GetOrCreateAsync("printer-status", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTime;

            IReadOnlyList<PrinterStatusDto> printers =
            [
                await GetOneAsync("plotter", options.Value.Plotter, ct),
                await GetOneAsync("office", options.Value.Office, ct),
            ];
            return printers;
        });

        return result ?? [];
    }

    private async Task<PrinterStatusDto> GetOneAsync(string key, QueueOptions queue, CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(queue.DisplayName) ? queue.Queue : queue.DisplayName;

        try
        {
            var state = await cups.GetPrinterStateAsync(queue.Queue, ct);

            var availability = !state.AcceptingJobs
                ? PrinterAvailability.NotAccepting
                : state.State switch
                {
                    CupsPrinterStates.Processing => PrinterAvailability.Printing,
                    CupsPrinterStates.Stopped => PrinterAvailability.Stopped,
                    _ => PrinterAvailability.Idle,
                };

            var message = !string.IsNullOrWhiteSpace(state.Message) ? state.Message
                : state.Reasons.Count > 0 ? string.Join(", ", state.Reasons)
                : null;

            return new PrinterStatusDto(key, name, availability, message, state.QueuedJobs);
        }
        catch (Exception ex) when (ex is HttpRequestException or CupsException)
        {
            logger.LogWarning(ex, "Не удалось получить состояние принтера {Queue}", queue.Queue);
            return new PrinterStatusDto(key, name, PrinterAvailability.Unavailable, null, 0);
        }
    }
}