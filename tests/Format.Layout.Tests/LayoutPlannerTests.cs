namespace Format.Layout.Tests;

public class LayoutPlannerTests
{
    private readonly LayoutPlanner _planner = new(new PlotterSettings());

    private static PrintItem Item(string format, int copies = 1, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), FormatCatalog.Get(format), copies);

    [Fact]
    public void Rotation_matches_business_rules()
    {
        // Ваш список: «Так делаем для A4x3, A4x4, A3x3, A2, A1»
        var expected = new[] { "A4x3", "A4x4", "A3x3", "A2", "A1" };

        var actual = FormatCatalog.All
            .Where(f => !f.IsOffice && f.Name != "A0x2")
            .Where(_planner.IsRotatable)
            .Select(f => f.Name);

        Assert.Equal(expected.Order(), actual.Order());
    }

    [Theory]
    [InlineData("A4")]
    [InlineData("A3")]
    public void Office_formats_go_to_office_printer(string format)
    {
        var plan = _planner.Plan([Item(format, copies: 3)]);

        Assert.Empty(plan.Sheets);
        var job = Assert.Single(plan.OfficeJobs);
        Assert.Equal(3, job.Copies);
    }

    [Theory]
    [InlineData("A4x3", 297)]
    [InlineData("A4x4", 297)]
    [InlineData("A3x3", 420)]
    [InlineData("A2",   420)]
    [InlineData("A1",   594)]
    public void Rotatable_formats_use_short_side_of_roll(string format, double expectedLength)
    {
        var sheet = Assert.Single(_planner.Plan([Item(format)]).Sheets);

        Assert.Equal(SheetKind.Rotated, sheet.Kind);
        Assert.Equal(expectedLength, sheet.Length);
    }

    [Theory]
    [InlineData("A4x5", 1051)]
    [InlineData("A3x4", 1189)]
    [InlineData("A2x3", 1261)]
    [InlineData("A1x3", 1783)]
    [InlineData("A0",   1189)]
    public void Long_formats_are_printed_as_is(string format, double expectedLength)
    {
        var sheet = Assert.Single(_planner.Plan([Item(format)]).Sheets);

        Assert.Equal(SheetKind.Single, sheet.Kind);
        Assert.Equal(expectedLength, sheet.Length);
    }

    [Fact]
    public void Drawing_is_centered_across_roll()
    {
        var sheet = Assert.Single(_planner.Plan([Item("A1")]).Sheets);
        var p = Assert.Single(sheet.Placements);

        Assert.Equal(841, p.Width);
        Assert.Equal(594, p.Height);
        Assert.Equal((914 - 841) / 2.0, p.X);
        Assert.Equal(0, p.Y);
    }

    [Fact]
    public void Each_copy_gets_its_own_sheet()
    {
        // A2x3 никогда не клеится (594+594 > 914), поэтому тест не сломается на следующих шагах
        var plan = _planner.Plan([Item("A2x3", copies: 3)]);

        Assert.Equal(3, plan.Sheets.Count);
        Assert.Equal(3 * 1261, plan.TotalRollLength);
    }

    [Fact]
    public void Same_drawing_in_several_lines_is_merged()
    {
        var id = Guid.NewGuid();
        var plan = _planner.Plan([Item("A4", 1, id), Item("A4", 2, id)]);

        Assert.Equal(3, Assert.Single(plan.OfficeJobs).Copies);
    }

    [Fact]
    public void Same_drawing_with_different_formats_throws()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => _planner.Plan([Item("A2", 1, id), Item("A1", 1, id)]));
    }

    [Fact]
    public void Zero_copies_throws()
    {
        Assert.Throws<ArgumentException>(() => _planner.Plan([Item("A2", copies: 0)]));
    }

    [Fact]
    public void Unprintable_format_throws()
    {
        Assert.Throws<InvalidOperationException>(() => _planner.Plan([Item("A0x2")]));
    }
    
     [Fact]
    public void Two_A2_copies_become_one_A1_sized_sheet()
    {
        var sheet = Assert.Single(_planner.Plan([Item("A2", copies: 2)]).Sheets);

        Assert.Equal(SheetKind.DuplicatePair, sheet.Kind);
        Assert.Equal(594, sheet.Length);
        Assert.Equal(2, sheet.Placements.Count);

        var x0 = (914 - 840) / 2.0;
        Assert.Equal(x0, sheet.Placements[0].X);
        Assert.Equal(x0 + 420, sheet.Placements[1].X);
        Assert.All(sheet.Placements, p =>
        {
            Assert.Equal(420, p.Width);
            Assert.Equal(594, p.Height);
        });
    }

    [Fact]
    public void A4x3_duplicates_are_not_paired()
    {
        var plan = _planner.Plan([Item("A4x3", copies: 2)]);

        Assert.Equal(2, plan.Sheets.Count);
        Assert.All(plan.Sheets, s => Assert.Equal(SheetKind.Rotated, s.Kind));
    }

    [Fact]
    public void Odd_copies_give_pairs_plus_one_single()
    {
        var plan = _planner.Plan([Item("A3x5", copies: 5)]);

        Assert.Equal(2, plan.Sheets.Count(s => s.Kind == SheetKind.DuplicatePair));
        Assert.Equal(1, plan.Sheets.Count(s => s.Kind == SheetKind.Single));
        Assert.Equal(3 * 1486, plan.TotalRollLength);
    }

    [Fact]
    public void Long_format_pair_halves_paper()
    {
        var plan = _planner.Plan([Item("A4x9", copies: 2)]);

        Assert.Equal(1892, plan.TotalRollLength);
    }

    [Fact]
    public void Gap_is_placed_between_drawings()
    {
        var planner = new LayoutPlanner(new PlotterSettings { Gap = 10 });
        var sheet = Assert.Single(planner.Plan([Item("A2", copies: 2)]).Sheets);

        var x0 = (914 - 850) / 2.0;
        Assert.Equal(x0, sheet.Placements[0].X);
        Assert.Equal(x0 + 420 + 10, sheet.Placements[1].X);
    }

    [Fact]
    public void All_pairable_formats_fit_on_roll()
    {
        // Страховка: флаг AllowDuplicatePair не должен стоять у формата, пара которого не влезает
        var pairable = FormatCatalog.All.Where(f => f.AllowDuplicatePair);

        Assert.All(pairable, f => Assert.True(2 * f.ShortSide <= 914, f.Name));
    }

    [Theory]
    [InlineData("A4x4", 841)]
    [InlineData("A3x3", 891)]
    public void By_default_unprofitable_pairs_follow_business_rules(string format, double expectedLength)
    {
        var plan = _planner.Plan([Item(format, copies: 2)]);

        Assert.Equal(SheetKind.DuplicatePair, Assert.Single(plan.Sheets).Kind);
        Assert.Equal(expectedLength, plan.TotalRollLength);
    }

    [Theory]
    [InlineData("A4x4", 594)]
    [InlineData("A3x3", 840)]
    public void PairOnlyIfSaves_prints_unprofitable_pairs_separately(string format, double expectedLength)
    {
        var planner = new LayoutPlanner(new PlotterSettings { PairOnlyIfSaves = true });
        var plan = planner.Plan([Item(format, copies: 2)]);

        Assert.Equal(2, plan.Sheets.Count);
        Assert.Equal(expectedLength, plan.TotalRollLength);
    }

    [Fact]
    public void PairOnlyIfSaves_keeps_profitable_A2_pairs()
    {
        var planner = new LayoutPlanner(new PlotterSettings { PairOnlyIfSaves = true });

        Assert.Equal(SheetKind.DuplicatePair, Assert.Single(planner.Plan([Item("A2", copies: 2)]).Sheets).Kind);
    }
    
      [Fact]
    public void Nesting_whitelist_matches_business_rules()
    {
        // Ваш список: «A4x4–A4x9, A2, A3x4–A3x7»
        var expected = new[]
        {
            "A4x4", "A4x5", "A4x6", "A4x7", "A4x8", "A4x9",
            "A2",
            "A3x4", "A3x5", "A3x6", "A3x7",
        };

        var actual = FormatCatalog.All.Where(f => f.AllowNesting).Select(f => f.Name);

        Assert.Equal(expected.Order(), actual.Order());
    }

    [Fact]
    public void A2_and_A3x4_are_nested_and_A2_is_centered()
    {
        var a2 = Item("A2");
        var a3x4 = Item("A3x4");

        var sheet = Assert.Single(_planner.Plan([a2, a3x4]).Sheets);

        Assert.Equal(SheetKind.Nested, sheet.Kind);
        Assert.Equal(1189, sheet.Length);

        var x0 = (914 - 840) / 2.0;
        var longOne = sheet.Placements.Single(p => p.DrawingId == a3x4.DrawingId);
        var shortOne = sheet.Placements.Single(p => p.DrawingId == a2.DrawingId);

        Assert.Equal(x0, longOne.X);
        Assert.Equal(0, longOne.Y);
        Assert.Equal(x0 + 420, shortOne.X);
        Assert.Equal((1189 - 594) / 2.0, shortOne.Y);
    }

    [Fact]
    public void Two_different_A2_are_nested()
    {
        var sheet = Assert.Single(_planner.Plan([Item("A2"), Item("A2")]).Sheets);

        Assert.Equal(SheetKind.Nested, sheet.Kind);
        Assert.Equal(594, sheet.Length);
    }

    [Fact]
    public void Two_different_A4x4_are_not_nested_because_it_wastes_paper()
    {
        var plan = _planner.Plan([Item("A4x4"), Item("A4x4")]);

        Assert.Equal(2, plan.Sheets.Count);
        Assert.Equal(594, plan.TotalRollLength);
    }

    [Fact]
    public void A4x4_is_nested_with_longer_A4xN()
    {
        var sheet = Assert.Single(_planner.Plan([Item("A4x4"), Item("A4x9")]).Sheets);

        Assert.Equal(SheetKind.Nested, sheet.Kind);
        Assert.Equal(1892, sheet.Length);
    }

    [Fact]
    public void Different_short_sides_are_not_nested()
    {
        var plan = _planner.Plan([Item("A4x6"), Item("A3x6")]);

        Assert.Equal(2, plan.Sheets.Count);
        Assert.DoesNotContain(plan.Sheets, s => s.Kind == SheetKind.Nested);
    }

    [Theory]
    [InlineData("A4x3")]
    [InlineData("A3x3")]
    [InlineData("A1")]
    public void Formats_outside_whitelist_are_never_nested(string format)
    {
        var plan = _planner.Plan([Item(format), Item(format)]);

        Assert.DoesNotContain(plan.Sheets, s => s.Kind == SheetKind.Nested);
    }

    [Fact]
    public void Longest_drawings_are_paired_together()
    {
        var plan = _planner.Plan([Item("A4x5"), Item("A4x9"), Item("A4x8")]);

        // A4x9 + A4x8 на одном листе (1892), A4x5 отдельно (1051)
        Assert.Equal(1892 + 1051, plan.TotalRollLength);
    }

    [Fact]
    public void Leftover_duplicate_is_nested_with_another_drawing()
    {
        var plan = _planner.Plan([Item("A2", copies: 3), Item("A3x5")]);

        Assert.Single(plan.Sheets, s => s.Kind == SheetKind.DuplicatePair);
        Assert.Single(plan.Sheets, s => s.Kind == SheetKind.Nested);
        Assert.Equal(594 + 1486, plan.TotalRollLength);
    }
}