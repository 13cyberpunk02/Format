namespace Format.Storage.Api.Data;

public sealed class Drawing
{
    public Guid Id { get; init; }

    /// <summary>Имя файла, как его загрузил пользователь.</summary>
    public required string FileName { get; set; }

    /// <summary>Распознанный формат: "A2", "A4x5".</summary>
    public required string FormatName { get; init; }

    /// <summary>Фактический размер страницы из PDF, мм.</summary>
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>Кто загрузил. Пока строка, после этапа 6 - идентификатор пользователя.</summary>
    public required string UploadedBy { get; init; }

    public DateTimeOffset UploadedAt { get; init; }
}