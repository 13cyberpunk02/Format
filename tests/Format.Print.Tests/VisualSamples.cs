using Format.Layout;
using Format.Print.Api.Composition;
using PdfSharp.Pdf.IO;
using Xunit.Abstractions;

namespace Format.Print.Tests;

/// <summary>
/// Не автоматические проверки, а генерация листов, которые нужно открыть и посмотреть.
/// Запуск: dotnet test --filter "FullyQualifiedName~VisualSamples" --logger "console;verbosity=detailed"
/// </summary>
public class VisualSamples(ITestOutputHelper output)
{
    private readonly LayoutPlanner _planner = new(new PlotterSettings());
    private readonly SheetComposer _composer = new();

    [Fact]
    public void Synthetic_drawings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "format-samples");
        Directory.CreateDirectory(dir);

        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var a2Rotated = Guid.NewGuid();
        var a3x4 = Guid.NewGuid();

        var files = new Dictionary<Guid, string>
        {
            [a1] = TestPdf.Create(dir, "src-A1.pdf", 841, 594),
            [a2] = TestPdf.Create(dir, "src-A2.pdf", 594, 420),
            [a2Rotated] = TestPdf.Create(dir, "src-A2-rotate90.pdf", 420, 594, rotate: 90),
            [a3x4] = TestPdf.Create(dir, "src-A3x4.pdf", 420, 1189),
        };

        var plan = _planner.Plan(
        [
            new(a1, FormatCatalog.Get("A1"), 1),        // одиночный, повёрнут
            new(a2, FormatCatalog.Get("A2"), 2),        // пара дублей
            new(a2Rotated, FormatCatalog.Get("A2"), 1), // /Rotate=90, уйдёт в nesting
            new(a3x4, FormatCatalog.Get("A3x4"), 1),    // nesting с A2
        ]);

        ComposeAll(plan, files, dir);
    }

    [Fact]
    public void Real_drawings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "format-real");
        if (!Directory.Exists(dir))
        {
            output.WriteLine($"Положите реальные PDF в {dir} и запустите снова.");
            return;
        }

        var files = new Dictionary<Guid, string>();
        var items = new List<PrintItem>();

        foreach (var path in Directory.GetFiles(dir, "*.pdf").Where(p => !Path.GetFileName(p).StartsWith("sheet-")))
        {
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var page = doc.Pages[0];
            var format = FormatCatalog.DetectFromPoints(page.Width.Point, page.Height.Point);

            output.WriteLine(
                $"{Path.GetFileName(path)}: страниц {doc.PageCount}, " +
                $"{page.Width.Millimeter:0.0}×{page.Height.Millimeter:0.0} мм, " +
                $"/Rotate={page.Rotate}, формат {format?.Name ?? "НЕ РАСПОЗНАН"}");

            if (format is null || format.IsOffice || format.Name == "A0x2")
                continue;

            var id = Guid.NewGuid();
            files[id] = path;
            items.Add(new PrintItem(id, format, 2)); // по 2 копии, чтобы увидеть и пары
        }

        ComposeAll(_planner.Plan(items), files, dir);
    }

    private void ComposeAll(PrintPlan plan, IReadOnlyDictionary<Guid, string> files, string dir)
    {
        var n = 0;
        foreach (var sheet in plan.Sheets)
        {
            var path = Path.Combine(dir, $"sheet-{++n:00}-{sheet.Kind}-{sheet.Length:0}mm.pdf");
            using var stream = File.Create(path);
            _composer.Compose(sheet, files, stream);
            output.WriteLine($"Создан {path}");
        }

        output.WriteLine($"Всего листов: {plan.Sheets.Count}, расход рулона: {plan.TotalRollLength:0} мм");
    }
}