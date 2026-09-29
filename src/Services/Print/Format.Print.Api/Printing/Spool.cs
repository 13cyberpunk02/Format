using Microsoft.Extensions.Options;

namespace Format.Print.Api.Printing;

public sealed class SpoolOptions
{
    /// <summary>Папка для копий файлов заказов. Относительный путь - от папки сервиса.</summary>
    public string RootPath { get; set; } = "spool";
    
    /// <summary>Сколько дней хранить файлы упавших заказов для повтора.</summary>
    public int FailedRetentionDays { get; set; } = 7;
}

/// <summary>Локальные копии файлов заказов: spool/{orderId}/{drawingId}.pdf</summary>
public sealed class Spool(IOptions<SpoolOptions> options, IHostEnvironment environment)
{
    private readonly string _root = Path.Combine(environment.ContentRootPath, options.Value.RootPath);

    public string OrderDirectory(Guid orderId) => Path.Combine(_root, orderId.ToString("N"));

    public string DrawingFile(Guid orderId, Guid drawingId) =>
        Path.Combine(OrderDirectory(orderId), $"{drawingId:N}.pdf");

    public void DeleteOrder(Guid orderId)
    {
        var directory = OrderDirectory(orderId);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
    
    /// <summary>Все папки заказов в спуле и когда в них последний раз что-то менялось.</summary>
    public IReadOnlyList<(Guid OrderId, DateTimeOffset LastWrite)> ListOrderDirectories()
    {
        if (!Directory.Exists(_root))
            return [];

        return Directory.EnumerateDirectories(_root)
            .Select(path => (Path: path, Name: System.IO.Path.GetFileName(path)))
            .Where(d => Guid.TryParseExact(d.Name, "N", out _))
            .Select(d => (
                Guid.ParseExact(d.Name, "N"),
                new DateTimeOffset(Directory.GetLastWriteTimeUtc(d.Path), TimeSpan.Zero)))
            .ToList();
    }
}