using Format.Print.Api.Data;

namespace Format.Print.Api.Printing;

public sealed record OrderItemDto(Guid DrawingId, string FileName, int PageNumber, string Format, int Copies);

public sealed record OrderDto(
    Guid Id,
    long Number,
    string Title,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    Guid CreatedById,
    string CreatedByName,
    string CreatedByDepartment,
    double TotalRollLengthMm,
    int SheetCount,
    int OfficeJobCount,
    bool Punch,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<JobDto> Jobs)
{
    public static OrderDto From(PrintOrder o) => new(
        o.Id, o.Number, o.Title, o.Status, o.CreatedAt, o.CreatedById, o.CreatedByName, o.CreatedByDepartment,
        o.TotalRollLengthMm, o.SheetCount, o.OfficeJobCount, o.Punch,
        o.StartedAt, o.CompletedAt, o.Error,
        [
            .. o.Items
                .OrderBy(i => i.Position)
                .Select(i => new OrderItemDto(i.DrawingId, i.FileName, i.PageNumber, i.Format, i.Copies))
        ],
        [
            .. o.Jobs
                .OrderBy(j => j.CreatedAt)
                .Select(j => new JobDto(j.CupsJobId, j.Printer, j.Description, j.Copies, j.Status, j.CreatedAt,
                    j.CompletedAt, j.StateMessage + j.Error))
        ]);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record JobDto(
    int CupsJobId,
    string Printer,
    string Description,
    int Copies,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error);