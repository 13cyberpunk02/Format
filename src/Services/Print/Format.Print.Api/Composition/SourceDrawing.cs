using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace Format.Print.Api.Composition;

/// <summary>
/// Исходный чертёж, готовый к отрисовке: содержимое страницы без /Rotate
/// и угол (по часовой), на который его нужно повернуть, чтобы он выглядел как в просмотрщике.
/// </summary>
internal sealed class SourceDrawing : IDisposable
{
    private readonly MemoryStream? _buffer;

    public XPdfForm Form { get; }
    public int Rotate { get; }

    private SourceDrawing(XPdfForm form, int rotate, MemoryStream? buffer)
    {
        Form = form;
        Rotate = rotate;
        _buffer = buffer;
    }

    public static SourceDrawing Open(string path)
    {
        using (var probe = PdfReader.Open(path, PdfDocumentOpenMode.Modify))
        {
            var page = probe.Pages[0];

            // Приводим к 0..359: в PDF встречается и -90, и 450
            var rotate = (page.Elements.GetInteger("/Rotate") % 360 + 360) % 360;

            if (rotate != 0)
            {
                // Убираем /Rotate в копии в памяти, поворот сделаем сами
                page.Elements.SetInteger("/Rotate", 0);

                var buffer = new MemoryStream();
                probe.Save(buffer, closeStream: false);
                buffer.Position = 0;

                return new SourceDrawing(XPdfForm.FromStream(buffer), rotate, buffer);
            }
        }

        // Обычная страница без /Rotate - открываем как раньше
        return new SourceDrawing(XPdfForm.FromFile(path), 0, null);
    }

    public void Dispose()
    {
        Form.Dispose();
        _buffer?.Dispose();
    }
}