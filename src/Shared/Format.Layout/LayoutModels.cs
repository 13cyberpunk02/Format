namespace Format.Layout;

/// <summary>Что заказали: какой чертёж, какого формата, сколько копий.</summary>
public sealed record PrintItem(Guid DrawingId, DrawingFormat Format, int Copies);

public enum SheetKind
{
    /// <summary>Один чертёж как есть: короткая сторона поперёк рулона.</summary>
    Single,
    /// <summary>Один чертёж, повёрнут: длинная сторона поперёк рулона.</summary>
    Rotated,
    /// <summary>Две копии одного чертежа рядом.</summary>
    DuplicatePair,
    /// <summary>Два разных чертежа рядом.</summary>
    Nested,
}

/// <summary>
/// Положение чертежа на листе, мм. X - поперёк рулона, Y - вдоль, от левого верхнего угла.
/// Width - размер поперёк рулона, Height - вдоль (уже с учётом поворота).
/// </summary>
public sealed record Placement(Guid DrawingId, DrawingFormat Format, double X, double Y, double Width, double Height);

/// <summary>Кусок рулона под один лист. Length - сколько бумаги уйдёт по длине.</summary>
public sealed record PlotterSheet(double Width, double Length, SheetKind Kind, IReadOnlyList<Placement> Placements);

/// <summary>Задание на офисный принтер.</summary>
public sealed record OfficeJob(Guid DrawingId, DrawingFormat Format, int Copies);

/// <summary>Итог раскладки заказа.</summary>
public sealed record PrintPlan(IReadOnlyList<OfficeJob> OfficeJobs, IReadOnlyList<PlotterSheet> Sheets)
{
    /// <summary>Суммарный расход рулона, мм. Покажем пользователю в предпросмотре.</summary>
    public double TotalRollLength => Sheets.Sum(s => s.Length);
}