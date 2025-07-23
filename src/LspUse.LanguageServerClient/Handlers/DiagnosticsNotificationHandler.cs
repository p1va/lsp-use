using System.Collections.Concurrent;
using LspUse.LanguageServerClient.Models;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace LspUse.LanguageServerClient.Handlers;

public sealed class DiagnosticsNotificationHandler(ILogger<DiagnosticsNotificationHandler> logger)
    : ILspNotificationHandler
{
    public ConcurrentDictionary<Uri, DiagnosticNotification> LatestDiagnostics { get; } = new();

    [JsonRpcMethod("textDocument/publishDiagnostics",
        UseSingleObjectParameterDeserialization = true
    )]
    public void OnPublishDiagnostics(DiagnosticNotification parameters)
    {
        logger.LogDebug("[Notification] {Count} diagnostics for file [v{Version}] {File}",
            parameters.Diagnostics?.Count() ?? 0,
            parameters.Version,
            parameters.Uri
        );

        // If diagnostics are cleaned then we receive URI with empty diagnostics
        if (parameters.Uri is not null)
            LatestDiagnostics[parameters.Uri] = parameters;
    }
}
