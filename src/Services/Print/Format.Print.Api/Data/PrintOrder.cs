namespace Format.Print.Api.Data;

public enum OrderStatus
{
    Queued,
    Processing,
    Printing,
    Completed,
    Failed,
    Cancelled,
}

public sealed class PrintOrder
{
    public Guid Id { get; init; }

    public OrderStatus Status { get; set; }

    public Guid CreatedById { get; init; }
    public required string CreatedByName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Оценка по раскладке в момент заказа - для списка заказов.</summary>
    public double TotalRollLengthMm { get; init; }
    public int SheetCount { get; init; }
    public int OfficeJobCount { get; init; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Причина ошибки для пользователя, если статус Failed.</summary>
    public string? Error { get; set; }

    public List<PrintOrderItem> Items { get; init; } = [];
    public List<PrintJob> Jobs { get; init; } = [];
}

public sealed class PrintOrderItem
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }

    /// <summary>Порядок строк, как их выбрал пользователь.</summary>
    public int Position { get; init; }

    public Guid DrawingId { get; init; }

    // Копия сведений о чертеже на момент заказа: чертёж могут удалить, а история заказа должна остаться
    public required string FileName { get; init; }
    public int PageNumber { get; init; }
    public required string Format { get; init; }

    public int Copies { get; init; }
}