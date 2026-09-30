using Format.Layout;
using Format.Print.Api.Storage;

namespace Format.Print.Api.Printing;

public sealed record PrintItemRequest(Guid DrawingId, int Copies);

public sealed record PrintRequest(IReadOnlyList<PrintItemRequest>? Items, string? Title = null);

public sealed record PlacementDto(
    Guid DrawingId,
    string FileName,
    int PageNumber,
    string Format,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record SheetDto(int Index, string Kind, double WidthMm, double LengthMm, IReadOnlyList<PlacementDto> Placements);

public sealed record OfficeJobDto(Guid DrawingId, string FileName, int PageNumber, string Format, int Copies);

public sealed record PreviewDto(
    IReadOnlyList<SheetDto> Sheets,
    IReadOnlyList<OfficeJobDto> OfficeJobs,
    double TotalRollLengthMm)
{
    public static PreviewDto From(PrintPlan plan, IReadOnlyDictionary<Guid, StorageDrawing> drawings) => new(
        [
            .. plan.Sheets.Select((sheet, index) => new SheetDto(
                index + 1,
                sheet.Kind.ToString(),
                sheet.Width,
                sheet.Length,
                [
                    .. sheet.Placements.Select(p => new PlacementDto(
                        p.DrawingId,
                        drawings[p.DrawingId].FileName,
                        drawings[p.DrawingId].PageNumber,
                        p.Format.Name,
                        p.X, p.Y, p.Width, p.Height))
                ]))
        ],
        [
            .. plan.OfficeJobs.Select(j => new OfficeJobDto(
                j.DrawingId,
                drawings[j.DrawingId].FileName,
                drawings[j.DrawingId].PageNumber,
                j.Format.Name,
                j.Copies))
        ],
        plan.TotalRollLength);
}