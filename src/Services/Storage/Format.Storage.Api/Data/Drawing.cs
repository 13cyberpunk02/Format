namespace Format.Storage.Api.Data;

public sealed class Drawing
{
    public Guid Id { get; init; }

    /// <summary>Имя файла, как его загрузил пользователь.</summary>
    public required string FileName { get; set; }

    /// <summary>Распознанный формат: "A2", "A4x5".</summary>
    public required string FormatName { get; init; }
    
    /// <summary>Общий идентификатор всех страниц одного загруженного файла.</summary>
    public Guid UploadId { get; init; }

    /// <summary>Номер страницы в исходном файле, с 1.</summary>
    public int PageNumber { get; init; }

    /// <summary>Сколько всего страниц было в исходном файле.</summary>
    public int PageCount { get; init; }

    /// <summary>Фактический размер страницы из PDF, мм.</summary>
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>Кто загрузил. Пока строка, после этапа 6 - идентификатор пользователя.</summary>
    /// <summary>Идентификатор пользователя из токена (sub).</summary>
    public Guid UploadedById { get; init; }

    /// <summary>Имя на момент загрузки - чтобы показывать в списке, не обращаясь к сервису авторизации.</summary>
    public required string UploadedByName { get; init; }

    public DateTimeOffset UploadedAt { get; init; }
}