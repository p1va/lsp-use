using Microsoft.Extensions.Logging;

namespace LspUse.McpServer;

public class FileLoggerProvider(string filePath) : ILoggerProvider
{
    private readonly StreamWriter _writer = new(filePath, true)
    {
        AutoFlush = true
    };

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer);

    public void Dispose() => _writer?.Dispose();
}
