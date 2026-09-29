using Format.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Format.Storage.Api.Pdf;

/// <summary>Одна страница исходного файла: размер, результат проверки и, если она подходит, отдельный PDF.</summary>
public sealed record SplitPage(
    int PageNumber,
    int PageCount,
    double WidthMm,
    double HeightMm,
    FormatCheckResult Check,
    byte[]? Content);

public sealed class InvalidPdfException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class PdfPageSplitter(FormatValidator validator)
{
    /// <summary>
    /// Разрезает PDF на страницы и передаёт каждую в onPage по мере готовности.
    /// Возвращает число страниц в файле.
    /// </summary>
    public async Task<int> SplitAsync(Stream pdf, Func<SplitPage, Task> onPage, CancellationToken ct)
    {
        PdfDocument source;
        try
        {
            source = PdfReader.Open(pdf, PdfDocumentOpenMode.Import);
        }
        catch (Exception ex)
        {
            throw new InvalidPdfException("Файл не удалось прочитать как PDF: он повреждён или защищён паролем.", ex);
        }

        using (source)
        {
            if (source.PageCount == 0)
                throw new InvalidPdfException("В PDF нет ни одной страницы.");

            for (var i = 0; i < source.PageCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var page = source.Pages[i];
                var widthMm = page.Width.Millimeter;
                var heightMm = page.Height.Millimeter;
                var check = validator.CheckPageSize(widthMm, heightMm);

                var content = check.IsValid ? ExtractPage(source, i) : null;

                await onPage(new SplitPage(i + 1, source.PageCount, widthMm, heightMm, check, content));
            }

            return source.PageCount;
        }
    }

    private static byte[] ExtractPage(PdfDocument source, int index)
    {
        using var single = new PdfDocument();
        single.AddPage(source.Pages[index]);

        using var buffer = new MemoryStream();
        single.Save(buffer, closeStream: false);
        return buffer.ToArray();
    }
}