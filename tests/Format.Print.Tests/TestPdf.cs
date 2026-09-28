using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Format.Print.Tests;

/// <summary>Генерирует «чертёж-заглушку» заданного размера с метками ориентации.</summary>
public static class TestPdf
{
    public static string Create(string directory, string fileName, double widthMm, double heightMm, int rotate = 0)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromMillimeter(widthMm);
        page.Height = XUnit.FromMillimeter(heightMm);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var w = page.Width.Point;
            var h = page.Height.Point;
            var margin = XUnit.FromMillimeter(5).Point;

            // Рамка чертежа
            gfx.DrawRectangle(new XPen(XColors.Black, 2), margin, margin, w - 2 * margin, h - 2 * margin);

            // Красный треугольник в левом верхнем углу - видно, куда смотрит «верх»
            var tri = XUnit.FromMillimeter(40).Point;
            gfx.DrawPolygon(XBrushes.Red,
                [new XPoint(margin, margin), new XPoint(margin + tri, margin), new XPoint(margin, margin + tri)],
                XFillMode.Winding);

            // Синий прямоугольник 185×55 мм в правом нижнем углу - как основная надпись (штамп)
            var stampW = XUnit.FromMillimeter(185).Point;
            var stampH = XUnit.FromMillimeter(55).Point;
            gfx.DrawRectangle(XBrushes.LightBlue, w - margin - stampW, h - margin - stampH, stampW, stampH);
        }

        // Как делают некоторые CAD: ориентация через атрибут /Rotate, а не через размеры страницы
        if (rotate != 0)
            page.Elements.SetInteger("/Rotate", rotate);

        var path = Path.Combine(directory, fileName);
        document.Save(path);
        return path;
    }
}