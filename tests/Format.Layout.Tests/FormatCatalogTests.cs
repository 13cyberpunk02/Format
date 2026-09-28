namespace Format.Layout.Tests;

public class FormatCatalogTests
{
    [Fact]
    public void Catalog_is_consistent()
    {
        Assert.All(FormatCatalog.All, f => Assert.True(f.ShortSide <= f.LongSide, $"{f.Name}: стороны перепутаны"));
        Assert.Equal(FormatCatalog.All.Count, FormatCatalog.All.Select(f => f.Name).Distinct().Count());
    }

    [Fact]
    public void Office_formats_have_no_plotter_rules()
    {
        Assert.All(FormatCatalog.All.Where(f => f.IsOffice), f =>
        {
            Assert.False(f.AllowDuplicatePair);
            Assert.False(f.AllowNesting);
        });
    }

    [Fact]
    public void Get_ignores_case()
    {
        Assert.Same(FormatCatalog.Get("A4x5"), FormatCatalog.Get("a4X5"));
    }

    [Fact]
    public void Get_throws_for_unknown_name()
    {
        Assert.Throws<KeyNotFoundException>(() => FormatCatalog.Get("B5"));
    }

    [Theory]
    [InlineData(594, 420, "A2")]      // альбомная
    [InlineData(420, 594, "A2")]      // книжная - тот же формат
    [InlineData(420.4, 593.8, "A2")]  // неточные размеры из PDF
    [InlineData(841, 297, "A4x4")]
    [InlineData(1189, 841, "A0")]
    public void Detect_recognizes_format_in_any_orientation(double w, double h, string expected)
    {
        Assert.Equal(expected, FormatCatalog.Detect(w, h)?.Name);
    }

    [Theory]
    [InlineData(500, 500)]   // квадрат - такого формата нет
    [InlineData(420, 610)]   // A2 с ошибкой 16 мм - вне допуска
    public void Detect_returns_null_for_unknown_size(double w, double h)
    {
        Assert.Null(FormatCatalog.Detect(w, h));
    }

    [Fact]
    public void DetectFromPoints_recognizes_standard_pdf_A4()
    {
        // Так A4 выглядит в любом PDF: 595 × 842 пункта
        Assert.Equal("A4", FormatCatalog.DetectFromPoints(595, 842)?.Name);
    }
}