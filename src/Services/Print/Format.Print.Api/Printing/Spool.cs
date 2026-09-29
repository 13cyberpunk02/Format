using Microsoft.Extensions.Options;

namespace Format.Print.Api.Printing;

public sealed class SpoolOptions
{
    /// <summary>Папка для копий файлов заказов. Относительный путь - от папки сервиса.</summary>
    public string RootPath { get; set; } = "spool";
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
}