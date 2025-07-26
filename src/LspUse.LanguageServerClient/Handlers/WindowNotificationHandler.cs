using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using StreamJsonRpc;

namespace LspUse.LanguageServerClient.Handlers;

public enum LogMessageType
{
    Error = 1,
    Warning = 2,
    Info = 3,
    Log = 4,
}


public record LogMessageParams
{
    [JsonPropertyName("type")]
    public LogMessageType MessageType { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public record RoslynToastCommand
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }
}

public record RoslynToastParams
{
    [JsonPropertyName("messageType")]
    public LogMessageType MessageType { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("commands")]
    public RoslynToastCommand[]? Commands { get; init; }
}


/// <summary>
/// Handles <c>window/logMessage</c> and <c>window/_roslyn_showToast</c> notifications coming from the language server.
/// Messages are stored in thread-safe <see cref="ConcurrentQueue{T}"/> for later
/// consumption by the rest of the application or for test assertions.
/// </summary>
public sealed class WindowNotificationHandler : IRpcLocalTarget
{
    /// <summary>
    /// Gets the queue that accumulates all <see cref="LogMessageParams"/> that
    /// the server sends via <c>window/logMessage</c>.
    /// </summary>
    public ConcurrentQueue<LogMessageParams> LogMessages { get; } = new();

    /// <summary>
    /// Gets the queue that accumulates all <see cref="RoslynToastParams"/> that
    /// the server sends via <c>window/_roslyn_showToast</c>.
    /// </summary>
    public ConcurrentQueue<RoslynToastParams> RoslynToasts { get; } = new();

    /// <summary>
    /// Receives a <c>window/logMessage</c> notification.
    /// The method name must match the JSON-RPC method name that Roslyn sends.
    /// </summary>
    /// <param name="parameters">The parameters of the log message.</param>
    [JsonRpcMethod("window/logMessage", UseSingleObjectParameterDeserialization = true)]
    public void OnLogMessage(LogMessageParams parameters)
    {
        LogMessages.Enqueue(parameters);
    }

    /// <summary>
    /// Receives a <c>window/_roslyn_showToast</c> notification.
    /// The method name must match the JSON-RPC method name that Roslyn sends.
    /// </summary>
    /// <param name="parameters">The parameters of the toast notification.</param>
    [JsonRpcMethod("window/_roslyn_showToast", UseSingleObjectParameterDeserialization = true)]
    public void OnRoslynShowToast(RoslynToastParams parameters)
    {
        RoslynToasts.Enqueue(parameters);
    }
}
