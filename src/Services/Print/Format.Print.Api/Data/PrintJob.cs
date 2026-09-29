namespace Format.Print.Api.Data;

public enum JobStatus
{
    /// <summary>Отправлено в CUPS, ждёт или печатается.</summary>
    Pending,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>Одно задание в CUPS: лист рулона (возможно, в нескольких копиях) или документ на офисный принтер.</summary>
public sealed class PrintJob
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }

    /// <summary>Номер задания в CUPS - по нему узнаём его состояние.</summary>
    public int CupsJobId { get; init; }

    /// <summary>"plotter" или "office".</summary>
    public required string Printer { get; init; }

    /// <summary>Для людей: «Лист 3 из 12: A3x4 + A2, 1189 мм».</summary>
    public required string Description { get; init; }

    public int Copies { get; init; }

    public JobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
}