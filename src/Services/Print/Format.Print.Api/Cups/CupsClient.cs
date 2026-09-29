using System.Globalization;
using Format.Layout;
using Microsoft.Extensions.Options;

namespace Format.Print.Api.Cups;

public sealed class CupsClient(HttpClient http, IOptions<CupsOptions> options, ILogger<CupsClient> logger)
{
    private static int _lastRequestId;
    private readonly CupsOptions _options = options.Value;

    /// <summary>Отправить лист рулона на плоттер. Возвращает номер задания в CUPS.</summary>
    public Task<int> PrintPlotterSheetAsync(
        PlotterSheet sheet, Stream pdf, int copies, string jobName, CancellationToken ct)
    {
        var jobOptions = new Dictionary<string, string>(_options.Plotter.JobOptions)
        {
            // Размер куска рулона: ширина рулона × длина листа
            ["media"] = string.Create(CultureInfo.InvariantCulture,
                $"Custom.{Math.Round(sheet.Width)}x{Math.Round(sheet.Length)}mm"),
        };

        return PrintAsync(_options.Plotter.Queue, jobName, pdf, copies, jobOptions, ct);
    }

    /// <summary>Отправить A4/A3 на офисный принтер. Возвращает номер задания в CUPS.</summary>
    public Task<int> PrintOfficeAsync(
        DrawingFormat format, Stream pdf, int copies, string jobName, CancellationToken ct)
    {
        var jobOptions = new Dictionary<string, string>(_options.Office.JobOptions)
        {
            ["media"] = format.Name, // "A4" или "A3"
        };

        return PrintAsync(_options.Office.Queue, jobName, pdf, copies, jobOptions, ct);
    }

    private async Task<int> PrintAsync(
        string queue, string jobName, Stream pdf, int copies,
        IReadOnlyDictionary<string, string> jobOptions, CancellationToken ct)
    {
        var writer = new IppWriter(IppOperation.PrintJob, Interlocked.Increment(ref _lastRequestId))
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

        using var request = new HttpRequestMessage(HttpMethod.Post, $"printers/{queue}")
        {
            Content = new IppContent(writer.Build(), pdf),
        };

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var ipp = IppResponse.Parse(await response.Content.ReadAsByteArrayAsync(ct));

        if (!ipp.IsSuccess)
            throw new CupsException(
                $"CUPS отклонил задание в очередь {queue}: 0x{ipp.StatusCode:X4} {ipp.Get("status-message")}");

        var jobId = ipp.Get("job-id") as int?
            ?? throw new CupsException("CUPS не вернул номер задания.");

        logger.LogInformation("Задание {JobId} ({JobName}) отправлено в {Queue}, копий: {Copies}",
            jobId, jobName, queue, copies);

        return jobId;
    }
}