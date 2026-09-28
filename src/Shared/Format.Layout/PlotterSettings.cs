namespace Format.Layout;

public sealed record PlotterSettings
{
    /// <summary>Ширина рулона, мм.</summary>
    public double RollWidth { get; init; } = 914;

    /// <summary>Непечатаемое поле плоттера с каждого края по ширине рулона, мм.</summary>
    public double SideMargin { get; init; } = 0;
    
    /// <summary>Зазор между двумя чертежами на одном листе (под рез), мм.</summary>
    public double Gap { get; init; } = 0;

    /// <summary>Клеить дубли, только если пара тратит меньше рулона, чем две копии по отдельности.</summary>
    public bool PairOnlyIfSaves { get; init; } = false;

    /// <summary>Ширина, на которой плоттер реально может печатать.</summary>
    public double PrintableWidth => RollWidth - 2 * SideMargin;
}