using Format.Layout;
using Format.Print.Api.Composition;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Format.Print.Tests;

public class SheetComposerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("format-tests-").FullName;
    private readonly LayoutPlanner _planner = new(new PlotterSettings());
    private readonly SheetComposer _composer = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Собирает единственный лист плана и возвращает размер получившейся страницы, мм.</summary>
    private (double WidthMm, double LengthMm) ComposeSingleSheet(PrintItem[] items, Dictionary<Guid, string> files)
    {
        var sheet = Assert.Single(_planner.Plan(items).Sheets);

        using var stream = new MemoryStream();
        _composer.Compose(sheet, files, stream);

        stream.Position = 0;
        using var result = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.Equal(1, result.PageCount);
        var page = result.Pages[0];

        return (page.Width.Millimeter, page.Height.Millimeter);
    }

    [Fact]
    public void Rotated_single_sheet_has_roll_width_and_short_length()
    {
        var id = Guid.NewGuid();
        var file = TestPdf.Create(_dir, "a1.pdf", 841, 594);

        var (width, length) = ComposeSingleSheet([new(id, FormatCatalog.Get("A1"), 1)], new() { [id] = file });

        Assert.Equal(914, width, tolerance: 0.1);
        Assert.Equal(594, length, tolerance: 0.1);
    }

    [Fact]
    public void Duplicate_pair_of_A2_is_A1_sized()
    {
        var id = Guid.NewGuid();
        var file = TestPdf.Create(_dir, "a2.pdf", 594, 420);

        var (width, length) = ComposeSingleSheet([new(id, FormatCatalog.Get("A2"), 2)], new() { [id] = file });

        Assert.Equal(914, width, tolerance: 0.1);
        Assert.Equal(594, length, tolerance: 0.1);
    }

    [Fact]
    public void Nested_sheet_has_length_of_longer_drawing()
    {
        var a2 = Guid.NewGuid();
        var a3x4 = Guid.NewGuid();
        var files = new Dictionary<Guid, string>
        {
            [a2] = TestPdf.Create(_dir, "a2.pdf", 594, 420),
            [a3x4] = TestPdf.Create(_dir, "a3x4.pdf", 420, 1189),
        };

        var (_, length) = ComposeSingleSheet(
            [new(a2, FormatCatalog.Get("A2"), 1), new(a3x4, FormatCatalog.Get("A3x4"), 1)], files);

        Assert.Equal(1189, length, tolerance: 0.1);
    }

    [Fact]
    public void Missing_file_throws()
    {
        var sheet = Assert.Single(_planner.Plan([new(Guid.NewGuid(), FormatCatalog.Get("A1"), 1)]).Sheets);

        Assert.Throws<InvalidOperationException>(() =>
            _composer.Compose(sheet, new Dictionary<Guid, string>(), Stream.Null));
    }
}