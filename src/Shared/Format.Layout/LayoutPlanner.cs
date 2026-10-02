namespace Format.Layout;

public sealed class LayoutPlanner(PlotterSettings settings)
{
    public PrintPlan Plan(IEnumerable<PrintItem> items)
    {
        var officeJobs = new List<OfficeJob>();
        var sheets = new List<PlotterSheet>();
        var leftovers = new List<(Guid Id, DrawingFormat Format)>();

        foreach (var (drawingId, format, copies) in Normalize(items))
        {
            // Правило 1: A4 и A3 - на офисный принтер
            if (format.IsOffice)
            {
                officeJobs.Add(new OfficeJob(drawingId, format, copies));
                continue;
            }

            EnsureFitsRoll(format);

            var singles = copies;

            // Правило 3: дубли одного чертежа - парами на один лист
            if (CanPairDuplicates(format))
            {
                for (var i = 0; i < copies / 2; i++)
                    sheets.Add(SideBySide(SheetKind.DuplicatePair, (drawingId, format), (drawingId, format)));

                singles = copies % 2;
            }

            // Оставшиеся одиночные экземпляры ждут nesting
            for (var i = 0; i < singles; i++)
                leftovers.Add((drawingId, format));
        }

        // Правила 4 и 2: nesting разных чертежей, остальное - по одному листу
        sheets.AddRange(NestOrSingle(leftovers));

        return new PrintPlan(officeJobs, sheets);
    }

    /// <summary>Можно ли повернуть так, чтобы длинная сторона легла поперёк рулона.</summary>
    public bool IsRotatable(DrawingFormat format) => format.LongSide <= settings.PrintableWidth;
    
    /// <summary>Сколько добавляется к длине каждого куска рулона ради полей у краёв.</summary>
    private double EndMargins => settings.LeadMargin + settings.TrailMargin;

    /// <summary>Расход рулона на один экземпляр, напечатанный отдельно.</summary>
    public double SingleCost(DrawingFormat format) =>
        (IsRotatable(format) ? format.ShortSide : format.LongSide) + EndMargins;

    private IEnumerable<PlotterSheet> NestOrSingle(List<(Guid Id, DrawingFormat Format)> leftovers)
    {
        // Не из белого списка - сразу на отдельные листы
        foreach (var (id, format) in leftovers.Where(x => !CanNest(x.Format)))
            yield return SingleSheet(id, format);

        // Из белого списка - группируем по короткой стороне (297 и 420)
        var groups = leftovers
            .Where(x => CanNest(x.Format))
            .GroupBy(x => x.Format.ShortSide);

        foreach (var group in groups)
        {
            // Самые длинные - первыми: соседи по списку дают максимальную экономию
            var sorted = group.OrderByDescending(x => x.Format.LongSide).ToList();

            var i = 0;
            while (i < sorted.Count)
            {
                if (i + 1 < sorted.Count && ShouldNest(sorted[i], sorted[i + 1]))
                {
                    yield return SideBySide(SheetKind.Nested, sorted[i], sorted[i + 1]);
                    i += 2;
                }
                else
                {
                    yield return SingleSheet(sorted[i].Id, sorted[i].Format);
                    i++;
                }
            }
        }
    }

    private bool CanNest(DrawingFormat format) =>
        format.AllowNesting && FitsSideBySide(format, format);

    private bool ShouldNest((Guid Id, DrawingFormat Format) a, (Guid Id, DrawingFormat Format) b) =>
        a.Id != b.Id
        && FitsSideBySide(a.Format, b.Format)
        && NestingSaving(a.Format, b.Format) > 0;

    /// <summary>Сколько рулона экономит склейка двух чертежей по сравнению с раздельной печатью.</summary>
    private double NestingSaving(DrawingFormat a, DrawingFormat b) =>
        SingleCost(a) + SingleCost(b) - (Math.Max(a.LongSide, b.LongSide) + EndMargins);

    /// <summary>Помещаются ли два чертежа рядом, короткими сторонами поперёк рулона.</summary>
    private bool FitsSideBySide(DrawingFormat a, DrawingFormat b) =>
        a.ShortSide + settings.Gap + b.ShortSide <= settings.PrintableWidth;

    private bool CanPairDuplicates(DrawingFormat format) =>
        format.AllowDuplicatePair
        && FitsSideBySide(format, format)
        && (!settings.PairOnlyIfSaves || format.LongSide + EndMargins < 2 * SingleCost(format));

    /// <summary>
    /// Объединяет строки заказа по чертежу: если один чертёж указан дважды, копии складываются.
    /// </summary>
    private static IEnumerable<(Guid DrawingId, DrawingFormat Format, int Copies)> Normalize(IEnumerable<PrintItem> items)
    {
        foreach (var group in items.GroupBy(i => i.DrawingId))
        {
            var format = group.First().Format;

            if (group.Any(i => i.Format != format))
                throw new ArgumentException($"Чертёж {group.Key} указан в заказе с разными форматами.");

            if (group.Any(i => i.Copies < 1))
                throw new ArgumentException($"Чертёж {group.Key}: количество копий должно быть не меньше 1.");

            yield return (group.Key, format, group.Sum(i => i.Copies));
        }
    }

    private void EnsureFitsRoll(DrawingFormat format)
    {
        if (format.ShortSide > settings.PrintableWidth)
            throw new InvalidOperationException(
                $"Формат {format.Name} не помещается на рулон. Он должен был быть отклонён при загрузке.");
    }

    private PlotterSheet SingleSheet(Guid drawingId, DrawingFormat format)
    {
        var rotate = IsRotatable(format);

        // Повёрнут: длинная сторона поперёк рулона, короткая вдоль
        var width = rotate ? format.LongSide : format.ShortSide;
        var length = rotate ? format.ShortSide : format.LongSide;

        var x = (settings.RollWidth - width) / 2;

        return new PlotterSheet(
            settings.RollWidth,
            length + EndMargins,
            rotate ? SheetKind.Rotated : SheetKind.Single,
            [new Placement(drawingId, format, x, settings.LeadMargin, width, length)]);
    }

    /// <summary>
    /// Два чертежа в книжной ориентации, короткими сторонами рядом.
    /// Длина листа - по более длинному; более короткий центрируется по длине.
    /// </summary>
    private PlotterSheet SideBySide(
        SheetKind kind,
        (Guid Id, DrawingFormat Format) left,
        (Guid Id, DrawingFormat Format) right)
    {
        var contentLength = Math.Max(left.Format.LongSide, right.Format.LongSide);
        var usedWidth = left.Format.ShortSide + settings.Gap + right.Format.ShortSide;
        var x0 = (settings.RollWidth - usedWidth) / 2;

        return new PlotterSheet(settings.RollWidth, contentLength + EndMargins, kind,
        [
            new Placement(left.Id, left.Format,
                X: x0,
                Y: settings.LeadMargin + (contentLength - left.Format.LongSide) / 2,
                Width: left.Format.ShortSide,
                Height: left.Format.LongSide),

            new Placement(right.Id, right.Format,
                X: x0 + left.Format.ShortSide + settings.Gap,
                Y: settings.LeadMargin + (contentLength - right.Format.LongSide) / 2,
                Width: right.Format.ShortSide,
                Height: right.Format.LongSide),
        ]);
    }
}