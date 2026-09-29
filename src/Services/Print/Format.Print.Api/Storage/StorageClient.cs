namespace Format.Print.Api.Storage;

public sealed class StorageOptions
{
    /// <summary>Адрес сервиса хранения. Слеш в конце обязателен.</summary>
    public string BaseUrl { get; set; } = "";
}

/// <summary>Чертёж, как его описывает сервис хранения. Лишние поля ответа просто игнорируются.</summary>
public sealed record StorageDrawing(
    Guid Id,
    string FileName,
    int PageNumber,
    int PageCount,
    string Format,
    Guid UploadedById,
    string UploadedByName);

public sealed record StorageLookupRequest(IReadOnlyList<Guid> Ids);

public sealed class StorageClient(HttpClient http)
{
    public async Task<IReadOnlyList<StorageDrawing>> LookupAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("drawings/lookup", new StorageLookupRequest(ids), ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<StorageDrawing>>(ct) ?? [];
    }
}