namespace Format.Storage.Api.Files;

public interface IDrawingFileStore
{
    Task SaveAsync(Guid drawingId, byte[] content, CancellationToken ct);

    /// <summary>Открыть файл на чтение; null, если файла нет. Поток обязан закрыть вызывающий.</summary>
    Task<Stream?> OpenReadAsync(Guid drawingId, CancellationToken ct);

    Task DeleteAsync(Guid drawingId, CancellationToken ct);
}