namespace Format.Layout.Tests;

public class FormatValidatorTests
{
    private readonly FormatValidator _validator = new(new PlotterSettings());

    [Fact]
    public void A0x2_is_rejected_with_clear_message()
    {
        var result = _validator.Check(FormatCatalog.Get("A0x2"));

        Assert.False(result.IsValid);
        Assert.Equal("A0x2", result.Format?.Name);
        Assert.Contains("не может", result.Error);
    }

    [Fact]
    public void All_other_formats_are_printable()
    {
        var printable = FormatCatalog.All.Where(f => f.Name != "A0x2");

        Assert.All(printable, f => Assert.True(_validator.Check(f).IsValid, f.Name));
    }

    [Fact]
    public void CheckPageSize_rejects_unknown_size()
    {
        var result = _validator.CheckPageSize(500, 500);

        Assert.False(result.IsValid);
        Assert.Null(result.Format);
    }

    [Fact]
    public void CheckPageSize_accepts_known_format()
    {
        var result = _validator.CheckPageSize(594, 420);

        Assert.True(result.IsValid);
        Assert.Equal("A2", result.Format?.Name);
    }

    [Fact]
    public void Wider_roll_makes_A0x2_printable()
    {
        var wide = new FormatValidator(new PlotterSettings { RollWidth = 1270 });

        Assert.True(wide.Check(FormatCatalog.Get("A0x2")).IsValid);
    }
}