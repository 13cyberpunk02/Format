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
    
    /// <summary>Как принтер называется в интерфейсе: «Canon TM-300».</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Опции, добавляемые к каждому заданию в эту очередь (InputSlot, CutMedia...).</summary>
    public Dictionary<string, string> JobOptions { get; set; } = new();
    
    /// <summary>
    /// Опции, которые добавляются к заданию, когда пользователь попросил перфорацию.
    /// Имена и значения - из PPD-файла МФУ. Пусто - перфорация недоступна.
    /// </summary>
    public Dictionary<string, string> PunchJobOptions { get; set; } = new();
}