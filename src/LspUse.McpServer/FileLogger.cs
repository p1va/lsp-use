using Microsoft.Extensions.Logging;

namespace LspUse.McpServer;

public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly StreamWriter _writer;

    public FileLogger(string categoryName, StreamWriter writer)
    {
        _categoryName = categoryName;
        _writer = writer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var message = formatter(state, exception);

        lock (_writer)
        {
            _writer.WriteLine($"[{timestamp}] [{logLevel}] [{_categoryName}] {message}");
            if (exception != null)
            {
                _writer.WriteLine(exception.ToString());
            }
        }
    }
}
