using LspUse.LanguageServerClient.Handlers;
using StreamJsonRpc;

namespace LspUse.LanguageServerClient;

using Models;

/// <summary>
/// Strongly-typed façade over the JSON-RPC Language Server Protocol calls that
/// the application layer relies on.  Only the subset required by our CLI
/// proxy is exposed; more methods can be added incrementally as new
/// capabilities are needed.
/// </summary>
public interface ILspClient : IAsyncDisposable, IDisposable
{
    JsonRpc Rpc { get; }
    WorkspaceNotificationHandler Workspace { get; }
    WindowNotificationHandler Window { get; }
    ClientCapabilityRegistrationHandler ClientCapability { get; }
    DiagnosticsNotificationHandler Diagnostics { get; }
    DefaultNotificationHandler UnhandledNotifications { get; }
    DefaultRequestHandler UnhandledRequests { get; }

    /// <summary>Sends <c>textDocument/didOpen</c>.</summary>
    Task DidOpenAsync(DidOpenTextDocumentParams @params, CancellationToken ct = default);

    /// <summary>Sends <c>textDocument/didClose</c>.</summary>
    Task DidCloseAsync(DidCloseTextDocumentParams @params, CancellationToken ct = default);

    /// <summary>Requests <c>textDocument/definition</c>.</summary>
    Task<Location[]> DefinitionAsync(DocumentClientRequest @params, CancellationToken ct = default);

    Task<Location[]> TypeDefinitionAsync(DocumentClientRequest @params,
        CancellationToken ct = default);

    Task<Location[]> ImplementationAsync(DocumentClientRequest @params,
        CancellationToken ct = default);

    /// <summary>Requests <c>textDocument/references</c>.</summary>
    Task<Location[]> ReferencesAsync(DocumentClientRequest @params, CancellationToken ct = default);

    /// <summary>Requests <c>textDocument/hover</c>.</summary>
    Task<Hover?> HoverAsync(DocumentClientRequest @params, CancellationToken ct = default);

    /// <summary>Requests <c>textDocument/completion</c>.</summary>
    Task<CompletionList?> CompletionAsync(DocumentClientRequest @params,
        CancellationToken ct = default);

    /// <summary>Requests <c>textDocument/documentSymbol</c> for the given document.</summary>
    Task<SymbolInformation[]?> DocumentSymbolAsync(DocumentSymbolParams @params,
        CancellationToken ct = default);

    /// <summary>Requests <c>workspace/symbol</c> to search for symbols across the workspace.</summary>
    Task<IEnumerable<WorkspaceSymbolResult>> WorkspaceSymbolAsync(WorkspaceSymbolParams @params,
        CancellationToken ct = default);

    /// <summary>Requests pull diagnostics for the specified document.</summary>
    Task<FullDocumentDiagnosticReport?> DiagnosticAsync(TextDocumentDiagnosticParams @params,
        CancellationToken ct = default);

    /// <summary>Requests <c>textDocument/rename</c> to rename a symbol.</summary>
    Task<WorkspaceEdit?> RenameAsync(RenameParams @params, CancellationToken ct = default);

    /// <summary>Requests <c>textDocument/prepareRename</c> to prepare and validate a rename operation.</summary>
    Task<PrepareRenameResult?> PrepareRenameAsync(PrepareRenameParams @params,
        CancellationToken ct = default);

    /// <summary>
    /// Performs the mandatory LSP handshake by sending the <c>initialize</c>
    /// request. The returned JSON payload contains the server capabilities and
    /// any custom extension data published by the server.
    /// </summary>
    Task<System.Text.Json.Nodes.JsonNode> InitializeAsync(object @params,
        CancellationToken ct = default);

    /// <summary>
    /// Sends the <c>initialized</c> notification that finalises the LSP
    /// handshake. This is typically invoked with an empty parameter object.
    /// </summary>
    Task InitializedAsync(object @params, CancellationToken ct = default);

    /// <summary>
    /// Sends an arbitrary JSON-RPC notification. This allows higher layers to
    /// forward calls that are not yet part of <see cref="ILspClient"/>'s typed
    /// surface without depending on <c>JsonRpc</c> directly.
    /// </summary>
    Task NotifyAsync(string methodName, object @params, CancellationToken ct = default);

    /// <summary>
    /// Sends the <c>shutdown</c> request to gracefully shutdown the LSP server.
    /// This should be followed by an <c>exit</c> notification.
    /// </summary>
    Task ShutdownAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends the <c>exit</c> notification to terminate the LSP server process.
    /// This should only be called after <c>shutdown</c> has been sent.
    /// </summary>
    Task ExitAsync(CancellationToken ct = default);
}
