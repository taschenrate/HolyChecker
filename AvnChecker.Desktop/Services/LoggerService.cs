namespace AvnChecker.Desktop.Services;

public sealed class LoggerService
{
    private readonly string _logsDirectory;
    private readonly object _sync = new();
    private string _mode;

    public LoggerService(string baseDirectory, string mode)
    {
        _logsDirectory = Path.Combine(baseDirectory, "Logs");
        Directory.CreateDirectory(_logsDirectory);
        _mode = mode;
    }

    public void SetMode(string mode)
    {
        _mode = mode;
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warn(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        if (ex is null)
        {
            Write("ERROR", message);
            return;
        }

        Write("ERROR", $"{message}. {ex.GetType().Name}: {ex.Message}");
        if (string.Equals(_mode, "verbose", StringComparison.OrdinalIgnoreCase))
        {
            Write("ERROR", ex.StackTrace ?? string.Empty);
        }
    }

    private void Write(string level, string message)
    {
        if (string.Equals(_mode, "minimal", StringComparison.OrdinalIgnoreCase) && level == "INFO")
        {
            return;
        }

        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        var filePath = Path.Combine(_logsDirectory, $"avnchecker_{DateTime.Now:yyyyMMdd}.log");

        lock (_sync)
        {
            File.AppendAllText(filePath, line + Environment.NewLine);
        }
    }
}
