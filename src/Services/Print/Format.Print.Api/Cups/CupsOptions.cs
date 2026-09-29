namespace Format.Print.Api.Cups;

public sealed class CupsOptions
{
    /// <summary>Адрес CUPS-сервера. Слеш в конце обязателен.</summary>
    public string BaseUrl { get; set; } = "http://localhost:631/";

    /// <summary>От чьего имени отправляются задания - видно в CUPS.</summary>
    public string UserName { get; set; } = "format";

    public QueueOptions Plotter { get; set; } = new();
    public QueueOptions Office { get; set; } = new();
}

public sealed class QueueOptions
{
    public string Queue { get; set; } = "";

    /// <summary>Опции, добавляемые к каждому заданию в эту очередь (InputSlot, CutMedia...).</summary>
    public Dictionary<string, string> JobOptions { get; set; } = new();
}