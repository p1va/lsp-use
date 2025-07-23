using System.Diagnostics;

namespace LspUse.TestHarness;

internal class ActionTraceListener(Action<string> onWriteLine) : TraceListener
{
    public override void Write(string? _)
    {
    }

    public override void WriteLine(string? message) => onWriteLine(message ?? string.Empty);
}
