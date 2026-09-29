using Format.Layout;
using Format.Storage.Api.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Format.Storage.Tests;

public class PdfPageSplitterTests
{
    private readonly PdfPageSplitter _splitter = new(new FormatValidator(new PlotterSettings()));

    /// <summary>Многостраничный PDF с заданными размерами страниц, мм.</summary>
    private static MemoryStream MultiPagePdf(params (double W, double H)[] pages)
    {
        using var document = new PdfDocument();
        foreach (var (w, h) in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(w);
            page.Height = XUnit.FromMillimeter(h);
        }

        var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        stream.Position = 0;
        return stream;
    }

    private async Task<List<SplitPage>> SplitAll(Stream pdf)
    {
        var result = new List<SplitPage>();
        await _splitter.SplitAsync(pdf, page =>
        {
            result.Add(page);
            return Task.CompletedTask;
        }, CancellationToken.None);
        return result;
    }

    [Fact]
    public async Task Each_page_is_checked_separately()
    {
        using var pdf = MultiPagePdf((594, 420), (1189, 1682), (500, 500), (297, 1051));

        var pages = await SplitAll(pdf);

        Assert.Equal(4, pages.Count);
        Assert.All(pages, p => Assert.Equal(4, p.PageCount));

        Assert.Equal("A2", pages[0].Check.Format?.Name);
        Assert.False(pages[1].Check.IsValid); // A0x2 - не помещается на рулон
        Assert.False(pages[2].Check.IsValid); // неизвестный размер
        Assert.Equal("A4x5", pages[3].Check.Format?.Name);
    }

    [Fact]
    public async Task Only_valid_pages_get_content()
    {
        using var pdf = MultiPagePdf((594, 420), (500, 500));

        var pages = await SplitAll(pdf);

        Assert.NotNull(pages[0].Content);
        Assert.Null(pages[1].Content);
    }

    [Fact]
    public async Task Extracted_page_is_single_page_pdf_of_same_size()
    {
        using var pdf = MultiPagePdf((297, 420), (420, 1189));

        var pages = await SplitAll(pdf);

        using var extracted = PdfReader.Open(new MemoryStream(pages[1].Content!), PdfDocumentOpenMode.Import);
        Assert.Equal(1, extracted.PageCount);
        Assert.Equal(420, extracted.Pages[0].Width.Millimeter, tolerance: 0.1);
        Assert.Equal(1189, extracted.Pages[0].Height.Millimeter, tolerance: 0.1);
    }

    [Fact]
    public async Task Garbage_is_rejected_with_clear_message()
    {
        using var garbage = new MemoryStream("это не pdf"u8.ToArray());

        var ex = await Assert.ThrowsAsync<InvalidPdfException>(() => SplitAll(garbage));
        Assert.Contains("не удалось прочитать", ex.Message);
    }
}