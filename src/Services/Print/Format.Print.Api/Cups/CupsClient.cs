using System.Globalization;
using System.Net.Http.Headers;
using Format.Layout;
using Microsoft.Extensions.Options;

namespace Format.Print.Api.Cups;

/// <summary>Состояния задания по IPP (job-state).</summary>
public static class CupsJobStates
{
    public const int Pending = 3, Held = 4, Processing = 5, Stopped = 6, Canceled = 7, Aborted = 8, Completed = 9;
}

public static class CupsPrinterStates
{
    public const int Idle = 3, Processing = 4, Stopped = 5;
}

public sealed record CupsPrinterState(
    int State,
    bool AcceptingJobs,
    IReadOnlyList<string> Reasons,
    string? Message,
    int QueuedJobs);

public sealed record CupsJobState(int State, IReadOnlyList<string> Reasons, string? PrinterMessage)
{
    public bool IsFinal => State >= CupsJobStates.Canceled;
}

public sealed class CupsClient(HttpClient http, IOptions<CupsOptions> options, ILogger<CupsClient> logger)
{
    private const ushort StatusNotPossible = 0x0404;
    private const ushort StatusNotFound = 0x0406;
    private const ushort StatusNotAcceptingJobs = 0x0506;
    
    private static int _lastRequestId;
    private readonly CupsOptions _options = options.Value;
    
    /// <summary>Формат, который умеет перфорировать финишер МФУ.</summary>
    public const string PunchableFormat = "A4";

    /// <summary>Настроена ли перфорация для офисного принтера.</summary>
    public bool PunchAvailable => _options.Office.PunchJobOptions.Count > 0;

    /// <summary>Отправить лист рулона на плоттер. Возвращает номер задания в CUPS.</summary>
    public Task<int> PrintPlotterSheetAsync(
        PlotterSheet sheet, Stream pdf, int copies, string jobName, CancellationToken ct)
    {
        var jobOptions = new Dictionary<string, string>(_options.Plotter.JobOptions)
        {
            ["media"] = string.Create(CultureInfo.InvariantCulture,
                $"Custom.{Math.Round(sheet.Width)}x{Math.Round(sheet.Length)}mm"),
        };

        return PrintAsync(_options.Plotter.Queue, jobName, pdf, copies, jobOptions, ct);
    }

    /// <summary>Отправить A4/A3 на офисный принтер. Возвращает номер задания в CUPS.</summary>
    public Task<int> PrintOfficeAsync(
        DrawingFormat format, Stream pdf, int copies, string jobName, bool punch, CancellationToken ct)
    {
        var jobOptions = new Dictionary<string, string>(_options.Office.JobOptions)
        {
            ["media"] = format.Name,
        };

        if (punch && format.Name == PunchableFormat)
        {
            foreach (var (name, value) in _options.Office.PunchJobOptions)
                jobOptions[name] = value;
        }

        return PrintAsync(_options.Office.Queue, jobName, pdf, copies, jobOptions, ct);
    }

    /// <summary>Состояние задания. null - CUPS такого задания не знает (история очищена).</summary>
    public async Task<CupsJobState?> GetJobStateAsync(int jobId, CancellationToken ct)
    {
        var ipp = await SendAsync("jobs", JobRequest(IppOperation.GetJobAttributes, jobId), document: null, ct);

        if (ipp.StatusCode == StatusNotFound)
            return null;

        EnsureSuccess(ipp, $"Не удалось получить состояние задания {jobId}");

        var state = ipp.Get("job-state") as int?
            ?? throw new CupsException($"CUPS не сообщил состояние задания {jobId}.");

        var reasons = ipp.GetAll("job-state-reasons")
            .OfType<string>()
            .Where(r => r != "none")
            .ToList();

        return new CupsJobState(state, reasons, ipp.Get("job-printer-state-message") as string);
    }

    /// <summary>Отменить задание. false - отменять уже нечего: задание завершено или не найдено.</summary>
    public async Task<bool> CancelJobAsync(int jobId, CancellationToken ct)
    {
        var ipp = await SendAsync("jobs", JobRequest(IppOperation.CancelJob, jobId), document: null, ct);

        if (ipp.StatusCode is StatusNotFound or StatusNotPossible)
            return false;

        EnsureSuccess(ipp, $"Не удалось отменить задание {jobId}");

        logger.LogInformation("Задание {JobId} отменено в CUPS", jobId);
        return true;
    }

    private async Task<int> PrintAsync(
        string queue, string jobName, Stream pdf, int copies,
        IReadOnlyDictionary<string, string> jobOptions, CancellationToken ct)
    {
        var writer = new IppWriter(IppOperation.PrintJob, NextRequestId())
            .Group(IppTag.OperationGroup)
            .String(IppTag.Charset, "attributes-charset", "utf-8")
            .String(IppTag.NaturalLanguage, "attributes-natural-language", "ru")
            .String(IppTag.Uri, "printer-uri", $"ipp://{http.BaseAddress!.Authority}/printers/{queue}")
            .String(IppTag.Name, "requesting-user-name", _options.UserName)
            .String(IppTag.Name, "job-name", jobName)
            .String(IppTag.MimeMediaType, "document-format", "application/pdf")
            .Group(IppTag.JobGroup)
            .Integer("copies", copies);

        foreach (var (name, value) in jobOptions)
            writer.String(IppTag.Keyword, name, value);

        var ipp = await SendAsync($"printers/{queue}", writer.Build(), pdf, ct);

        if (ipp.StatusCode == StatusNotAcceptingJobs)
            throw new CupsException(
                "Принтер временно не принимает задания - возможно, идёт обслуживание. " +
                "Повторите печать позже или обратитесь к администратору.");
        
        EnsureSuccess(ipp, $"CUPS отклонил задание в очередь {queue}");

        var jobId = ipp.Get("job-id") as int?
            ?? throw new CupsException("CUPS не вернул номер задания.");

        logger.LogInformation("Задание {JobId} ({JobName}) отправлено в {Queue}, копий: {Copies}",
            jobId, jobName, queue, copies);

        return jobId;
    }
    
    public async Task<CupsPrinterState> GetPrinterStateAsync(string queue, CancellationToken ct)
    {
        var header = new IppWriter(IppOperation.GetPrinterAttributes, NextRequestId())
            .Group(IppTag.OperationGroup)
            .String(IppTag.Charset, "attributes-charset", "utf-8")
            .String(IppTag.NaturalLanguage, "attributes-natural-language", "ru")
            .String(IppTag.Uri, "printer-uri", $"ipp://{http.BaseAddress!.Authority}/printers/{queue}")
            .String(IppTag.Name, "requesting-user-name", _options.UserName)
            .Strings(IppTag.Keyword, "requested-attributes",
                "printer-state", "printer-is-accepting-jobs", "printer-state-reasons",
                "printer-state-message", "queued-job-count")
            .Build();

        var ipp = await SendAsync($"printers/{queue}", header, document: null, ct);
        EnsureSuccess(ipp, $"Не удалось получить состояние принтера {queue}");

        return new CupsPrinterState(
            ipp.Get("printer-state") as int? ?? CupsPrinterStates.Stopped,
            ipp.Get("printer-is-accepting-jobs") as bool? ?? false,
            [.. ipp.GetAll("printer-state-reasons").OfType<string>().Where(r => r != "none")],
            ipp.Get("printer-state-message") as string,
            ipp.Get("queued-job-count") as int? ?? 0);
    }

    /// <summary>Запрос, который касается одного задания: состояние или отмена.</summary>
    private byte[] JobRequest(ushort operation, int jobId) =>
        new IppWriter(operation, NextRequestId())
            .Group(IppTag.OperationGroup)
            .String(IppTag.Charset, "attributes-charset", "utf-8")
            .String(IppTag.NaturalLanguage, "attributes-natural-language", "ru")
            .String(IppTag.Uri, "job-uri", $"ipp://{http.BaseAddress!.Authority}/jobs/{jobId}")
            .String(IppTag.Name, "requesting-user-name", _options.UserName)
            .Build();

    private async Task<IppResponse> SendAsync(string path, byte[] header, Stream? document, CancellationToken ct)
    {
        HttpContent content = document is null
            ? new ByteArrayContent(header) { Headers = { ContentType = new MediaTypeHeaderValue("application/ipp") } }
            : new IppContent(header, document);

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return IppResponse.Parse(await response.Content.ReadAsByteArrayAsync(ct));
    }

    private static void EnsureSuccess(IppResponse ipp, string what)
    {
        if (!ipp.IsSuccess)
            throw new CupsException($"{what}: 0x{ipp.StatusCode:X4} {ipp.Get("status-message")}");
    }

    private static int NextRequestId() => Interlocked.Increment(ref _lastRequestId);
}