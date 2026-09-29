using Format.Storage.Api.Data;

namespace Format.Storage.Api.Endpoints;

public sealed record DrawingDto(
    Guid Id,
    Guid UploadId,
    string FileName,
    int PageNumber,
    int PageCount,
    string Format,
    double WidthMm,
    double HeightMm,
    long SizeBytes,
    Guid UploadedById,
    string UploadedByName,
    DateTimeOffset UploadedAt)
{
    public static DrawingDto From(Drawing d) => new(
        d.Id, d.UploadId, d.FileName, d.PageNumber, d.PageCount, d.FormatName,
        Math.Round(d.WidthMm, 1), Math.Round(d.HeightMm, 1), d.SizeBytes,
        d.UploadedById, d.UploadedByName, d.UploadedAt);
}

public sealed record RejectedPageDto(int PageNumber, string Reason);

public sealed record UploadResultDto(
    Guid UploadId,
    IReadOnlyList<DrawingDto> Drawings,
    IReadOnlyList<RejectedPageDto> RejectedPages);
   
/// <summary>Одна страница списка.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record FormatDto(
    string Name,
    double ShortSide,
    double LongSide,
    string Printer,
    bool IsPrintable,
    string? Reason);
    