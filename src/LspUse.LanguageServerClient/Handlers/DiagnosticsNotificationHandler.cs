using System.Collections.Concurrent;
using LspUse.LanguageServerClient.Models;
using StreamJsonRpc;

namespace LspUse.LanguageServerClient.Handlers;

/// <summary>
/// Handles <c>textDocument/publishDiagnostics</c> notifications.
/// Keeps track of the latest diagnostics per document URI.
/// </summary>
public sealed class DiagnosticsNotificationHandler : ILspNotificationHandler
{
    /// <summary>
    /// Gets a thread-safe map from document <see cref="Uri"/> to the last
    /// <see cref="PublishDiagnosticParams"/> received for that document.
    /// </summary>
    public ConcurrentDictionary<Uri, DiagnosticNotification> LatestDiagnostics { get; } = new();

    /// <summary>
    /// Receives a <c>textDocument/publishDiagnostics</c> notification.
    /// </summary>
    /// <param name="parameters">The diagnostics information.</param>
    [JsonRpcMethod("textDocument/publishDiagnostics",
        UseSingleObjectParameterDeserialization = true)]
    public void OnPublishDiagnostics(DiagnosticNotification parameters)
    {
        // If diagnostics are cleaned then we receive URI with empty diagnostics
        if (parameters.Uri is not null) LatestDiagnostics[parameters.Uri] = parameters;
    }
}
