using System.Text.Json.Nodes;
using StreamJsonRpc;

namespace LspUse.LanguageServerClient.Handlers;

/// <summary>
/// Handles the Roslyn-specific <c>workspace/projectInitializationComplete</c>
/// notification which signals that the workspace is fully loaded and ready
/// for requests such as completion, references, etc.
/// </summary>
public sealed class WorkspaceNotificationHandler : IRpcLocalTarget
{
    private readonly TaskCompletionSource _initializationTcs = new();

    /// <summary>
    /// Gets a task that completes when the workspace initialization finishes.
    /// </summary>
    public Task WorkspaceInitialization => _initializationTcs.Task;

    /// <summary>
    /// Receives the <c>workspace/projectInitializationComplete</c> notification.
    /// Roslyn currently sends an empty JSON object as the payload, but we accept
    /// any payload to remain forward-compatible.
    /// </summary>
    /// <param name="_">Unused payload.</param>
    [JsonRpcMethod("workspace/projectInitializationComplete", UseSingleObjectParameterDeserialization = true)]
    public void OnWorkspaceInitialized(JsonNode? _ = null)
    {
        _initializationTcs.TrySetResult();
    }

    [JsonRpcMethod("workspace/diagnostic/refresh")]
    public static void OnWorkspaceDiagnosticsRefresh()
    {
    }
}
