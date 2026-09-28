namespace Format.Layout;

/// <summary>Результат проверки: либо формат, либо текст ошибки для пользователя.</summary>
public sealed record FormatCheckResult(DrawingFormat? Format, string? Error)
{
    public bool IsValid => Error is null;

    public static FormatCheckResult Ok(DrawingFormat format) => new(format, null);
    public static FormatCheckResult Fail(string error, DrawingFormat? format = null) => new(format, error);
}

public sealed class FormatValidator(PlotterSettings settings)
{
    /// <summary>Можно ли напечатать формат на имеющемся оборудовании.</summary>
    public FormatCheckResult Check(DrawingFormat format)
    {
        // Офисные форматы идут на обычный принтер, рулон их не касается
        if (format.IsOffice)
            return FormatCheckResult.Ok(format);

        if (format.ShortSide > settings.PrintableWidth)
            return FormatCheckResult.Fail(
                $"Формат {format.Name} ({format.ShortSide:0}×{format.LongSide:0} мм) не помещается " +
                $"на рулон плоттера шириной {settings.RollWidth:0} мм. Плоттер не может его напечатать.",
                format);

        return FormatCheckResult.Ok(format);
    }

    /// <summary>Определить формат по размеру страницы PDF (мм) и сразу проверить его.</summary>
    public FormatCheckResult CheckPageSize(double widthMm, double heightMm)
    {
        var format = FormatCatalog.Detect(widthMm, heightMm);

        return format is null
            ? FormatCheckResult.Fail(
                $"Размер листа {widthMm:0}×{heightMm:0} мм не соответствует ни одному поддерживаемому формату.")
            : Check(format);
    }
}