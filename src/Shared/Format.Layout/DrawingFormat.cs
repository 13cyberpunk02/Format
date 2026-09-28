namespace Format.Layout;

/// <summary>
/// Формат чертежа. Размеры в миллиметрах, без привязки к ориентации:
/// ShortSide всегда меньше или равна LongSide.
/// </summary>
public sealed record DrawingFormat(
    string Name,
    double ShortSide,
    double LongSide,
    bool IsOffice = false,
    bool AllowDuplicatePair = false,
    bool AllowNesting = false);