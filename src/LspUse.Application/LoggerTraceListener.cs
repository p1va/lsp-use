using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LspUse.Application;

internal class LoggerTraceListener(ILogger logger) : TraceListener
{
    public override void Write(string? message)
    {
        if (!string.IsNullOrEmpty(message)) logger.LogTrace("{Message}", message);
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message)) logger.LogTrace("{Message}", message);
    }
}
