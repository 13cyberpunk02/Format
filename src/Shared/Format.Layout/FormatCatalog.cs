namespace Format.Layout;

public static class FormatCatalog
{
    /// <summary>Сколько типографских пунктов в одном миллиметре. В PDF размеры хранятся в пунктах (1/72 дюйма).</summary>
    public const double PointsPerMm = 72.0 / 25.4;

    /// <summary>Допуск при определении формата по размеру страницы, мм.</summary>
    public const double DefaultToleranceMm = 15;

    public static readonly IReadOnlyList<DrawingFormat> All =
    [
        // Офисные - на обычный принтер
        new("A4", 210, 297, IsOffice: true),
        new("A3", 297, 420, IsOffice: true),

        // A4xN, короткая сторона 297
        new("A4x3", 297, 630),   // дубли не клеим, в nesting не идёт
        new("A4x4", 297, 841,  AllowDuplicatePair: true, AllowNesting: true),
        new("A4x5", 297, 1051, AllowDuplicatePair: true, AllowNesting: true),
        new("A4x6", 297, 1261, AllowDuplicatePair: true, AllowNesting: true),
        new("A4x7", 297, 1471, AllowDuplicatePair: true, AllowNesting: true),
        new("A4x8", 297, 1682, AllowDuplicatePair: true, AllowNesting: true),
        new("A4x9", 297, 1892, AllowDuplicatePair: true, AllowNesting: true),

        // A3xN, короткая сторона 420
        new("A3x3", 420, 891,  AllowDuplicatePair: true),   // в nesting не идёт (N=3)
        new("A3x4", 420, 1189, AllowDuplicatePair: true, AllowNesting: true),
        new("A3x5", 420, 1486, AllowDuplicatePair: true, AllowNesting: true),
        new("A3x6", 420, 1783, AllowDuplicatePair: true, AllowNesting: true),
        new("A3x7", 420, 2080, AllowDuplicatePair: true, AllowNesting: true),

        // A2, короткая сторона 420: две A2 дают лист A1
        new("A2",   420, 594,  AllowDuplicatePair: true, AllowNesting: true),
        new("A2x3", 594, 1261),
        new("A2x4", 594, 1682),
        new("A2x5", 594, 2102),

        // Крупные: не клеим и не вкладываем
        new("A1",   594,  841),
        new("A1x3", 841,  1783),
        new("A1x4", 841,  2378),
        new("A0",   841,  1189),
        new("A0x2", 1189, 1682),
    ];

    private static readonly Dictionary<string, DrawingFormat> ByName =
        All.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Найти формат по имени ("A2", "a4x5"). Неизвестное имя - исключение.</summary>
    public static DrawingFormat Get(string name) =>
        ByName.TryGetValue(name, out var format)
            ? format
            : throw new KeyNotFoundException($"Неизвестный формат: {name}");

    /// <summary>Определить формат по размеру страницы в мм. Ориентация не важна. Не подошёл ни один - null.</summary>
    public static DrawingFormat? Detect(double widthMm, double heightMm, double toleranceMm = DefaultToleranceMm)
    {
        var shortSide = Math.Min(widthMm, heightMm);
        var longSide = Math.Max(widthMm, heightMm);

        return All
            .Where(f => Math.Abs(f.ShortSide - shortSide) <= toleranceMm
                     && Math.Abs(f.LongSide - longSide) <= toleranceMm)
            .OrderBy(f => Math.Abs(f.ShortSide - shortSide) + Math.Abs(f.LongSide - longSide))
            .FirstOrDefault();
    }

    /// <summary>То же, но размеры в пунктах - так их отдаёт PDF.</summary>
    public static DrawingFormat? DetectFromPoints(double widthPt, double heightPt) =>
        Detect(widthPt / PointsPerMm, heightPt / PointsPerMm);
}