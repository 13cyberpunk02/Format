using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Format.Storage.Api.Files;

public sealed class S3Options
{
    public string ServiceUrl { get; set; } = "http://localhost:8333";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "drawings";
}

public sealed class S3DrawingFileStore(IAmazonS3 s3, IOptions<S3Options> options) : IDrawingFileStore
{
    private readonly string _bucket = options.Value.Bucket;

    /// <summary>Ключ объекта в бакете: имя файла по Id чертежа.</summary>
    private static string Key(Guid drawingId) => $"{drawingId:N}.pdf";

    public async Task SaveAsync(Guid drawingId, byte[] content, CancellationToken ct)
    {
        using var stream = new MemoryStream(content, writable: false);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = Key(drawingId),
            InputStream = stream,
            ContentType = "application/pdf",
        }, ct);
    }

    public async Task<Stream?> OpenReadAsync(Guid drawingId, CancellationToken ct)
    {
        try
        {
            var response = await s3.GetObjectAsync(_bucket, Key(drawingId), ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(Guid drawingId, CancellationToken ct) =>
        s3.DeleteObjectAsync(_bucket, Key(drawingId), ct);
}