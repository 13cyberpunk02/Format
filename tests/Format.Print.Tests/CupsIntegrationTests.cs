using Format.Layout;
using Format.Print.Api.Composition;
using Format.Print.Api.Cups;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Format.Print.Tests;

/// <summary>Требует запущенный контейнер format-cups. Печатает на виртуальный плоттер.</summary>
public class CupsIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Prints_long_sheet_to_virtual_plotter()
    {
        var options = Options.Create(new CupsOptions
        {
            BaseUrl = "http://localhost:631/",
            Plotter = new QueueOptions
            {
                Queue = "plotter",
                JobOptions = { ["print-scaling"] = "none" },
            },
        });

        using var http = new HttpClient();
        http.BaseAddress = new Uri(options.Value.BaseUrl);

        try
        {
            await http.GetAsync("");
        }
        catch (HttpRequestException)
        {
            output.WriteLine("CUPS не запущен: docker compose -f deploy/docker-compose.dev.yml up -d");
            return;
        }

        // Собираем лист A3x4: 420 × 1189 мм, печатается без поворота
        var dir = Directory.CreateTempSubdirectory("format-cups-").FullName;
        var id = Guid.NewGuid();
        var file = TestPdf.Create(dir, "a3x4.pdf", 420, 1189);

        var planner = new LayoutPlanner(new PlotterSettings());
        var sheet = Assert.Single(planner.Plan([new PrintItem(id, FormatCatalog.Get("A3x4"), 1)]).Sheets);

        using var pdf = new MemoryStream();
        new SheetComposer().Compose(sheet, new Dictionary<Guid, string> { [id] = file }, pdf);
        pdf.Position = 0;

        var client = new CupsClient(http, options, NullLogger<CupsClient>.Instance);
        var jobId = await client.PrintPlotterSheetAsync(sheet, pdf, copies: 1, "Тест A3x4", CancellationToken.None);

        output.WriteLine($"CUPS принял задание {jobId}");
        Assert.True(jobId > 0);
    }
}