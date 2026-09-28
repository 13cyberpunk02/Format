using Format.Layout;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Format.Print.Api.Composition;

public sealed class SheetComposer
{
    /// <summary>
    /// Собирает один лист рулона в PDF.
    /// drawingFiles - пути к PDF чертежей по их Id.
    /// </summary>
    public void Compose(PlotterSheet sheet, IReadOnlyDictionary<Guid, string> drawingFiles, Stream output)
    {
        var sources = new Dictionary<Guid, SourceDrawing>();

        try
        {
            using var document = new PdfDocument();

            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(sheet.Width);
            page.Height = XUnit.FromMillimeter(sheet.Length);

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                foreach (var placement in sheet.Placements)
                {
                    var source = GetSource(placement.DrawingId, drawingFiles, sources);
                    DrawPlacement(gfx, source, placement);
                }
            }

            document.Save(output, closeStream: false);
        }
        finally
        {
            // Исходники держим открытыми до Save: содержимое копируется в итоговый файл при сохранении
            foreach (var source in sources.Values)
                source.Dispose();
        }
    }

    private static SourceDrawing GetSource(
        Guid drawingId,
        IReadOnlyDictionary<Guid, string> drawingFiles,
        Dictionary<Guid, SourceDrawing> cache)
    {
        if (cache.TryGetValue(drawingId, out var source))
            return source;

        if (!drawingFiles.TryGetValue(drawingId, out var path))
            throw new InvalidOperationException($"Не передан файл для чертежа {drawingId}.");

        source = SourceDrawing.Open(path);
        cache[drawingId] = source;
        return source;
    }

    private static void DrawPlacement(XGraphics gfx, SourceDrawing source, Placement placement)
    {
        // Размер хранимой страницы (без /Rotate), в пунктах
        var rawW = source.Form.PointWidth;
        var rawH = source.Form.PointHeight;

        // Как чертёж выглядит в просмотрщике PDF, то есть с учётом его /Rotate
        var shownLandscape = source.Rotate is 90 or 270 ? rawH > rawW : rawW > rawH;
        var slotLandscape = placement.Width > placement.Height;

        // Итоговый угол: поворот из самого PDF + наш, если ориентация не совпала с местом
        var angle = (source.Rotate + (shownLandscape != slotLandscape ? 90 : 0)) % 360;

        // Размер чертежа на листе после поворота
        var drawnW = angle is 90 or 270 ? rawH : rawW;
        var drawnH = angle is 90 or 270 ? rawW : rawH;

        // Центрируем в отведённом месте: масштаб строго 1:1
        var x = Mm(placement.X) + (Mm(placement.Width) - drawnW) / 2;
        var y = Mm(placement.Y) + (Mm(placement.Height) - drawnH) / 2;

        var state = gfx.Save();

        // Сдвигаем начало координат в тот угол, откуда после поворота начнётся чертёж
        switch (angle)
        {
            case 0: gfx.TranslateTransform(x, y); break;
            case 90: gfx.TranslateTransform(x + drawnW, y); break;
            case 180: gfx.TranslateTransform(x + drawnW, y + drawnH); break;
            case 270: gfx.TranslateTransform(x, y + drawnH); break;
        }

        gfx.RotateTransform(angle);
        gfx.DrawImage(source.Form, 0, 0, rawW, rawH);
        gfx.Restore(state);
    }

    private static double Mm(double millimeters) => XUnit.FromMillimeter(millimeters).Point;
}