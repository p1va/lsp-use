using System.Diagnostics;
using LspUse.LanguageServerClient.Handlers;
using LspUse.LanguageServerClient.Json;
using Microsoft.Extensions.Logging;

namespace LspUse.LanguageServerClient;

using System.Text.Json.Nodes;
using Models;
using StreamJsonRpc;

/// <summary>
/// Default <see cref="ILspClient"/> implementation that forwards calls through
/// a configured <see cref="JsonRpc"/> instance.  The instance is assumed to be
/// connected to the standard input/output (or any stream pair) of a running
/// Language-Server process.
/// </summary>
public sealed class JsonRpcLspClient : ILspClient
{
    private readonly ILogger<JsonRpcLspClient> _logger;
    private readonly IEnumerable<IRpcLocalTarget> _localTargets;
    private readonly JsonRpc _rpc;
    
    public JsonRpc Rpc => _rpc;

    public WorkspaceNotificationHandler Workspace =>
        GetHandler<WorkspaceNotificationHandler>() ??
        throw new InvalidOperationException("Notification handler not registered.");

    public WindowNotificationHandler Window =>
        GetHandler<WindowNotificationHandler>() ??
        throw new InvalidOperationException("Notification handler not registered.");

    public ClientCapabilityRegistrationHandler ClientCapability =>
        GetHandler<ClientCapabilityRegistrationHandler>() ??
        throw new InvalidOperationException("Notification handler not registered.");

    public DiagnosticsNotificationHandler Diagnostics =>
        GetHandler<DiagnosticsNotificationHandler>() ??
        throw new InvalidOperationException("Notification handler not registered.");

    public DefaultNotificationHandler UnhandledNotifications =>
        GetHandler<DefaultNotificationHandler>() ??
        throw new InvalidOperationException("Notification handler not registered.");

    public DefaultRequestHandler UnhandledRequests =>
        GetHandler<DefaultRequestHandler>() ??
        throw new InvalidOperationException("Request handler not registered.");

    private T? GetHandler<T>() where T : IRpcLocalTarget =>
        _localTargets
            .OfType<T>()
            .FirstOrDefault();

    public JsonRpcLspClient(Stream stdin,
        Stream stdout,
        ILogger<JsonRpcLspClient> logger,
        IEnumerable<IRpcLocalTarget> localTargets)
    {
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(localTargets);

        _logger = logger;
        _localTargets = localTargets;

        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.Converters.Add(new AbsoluteUriJsonConverter());

        var handler = new HeaderDelimitedMessageHandler(stdin, stdout, formatter);

        _rpc = new JsonRpc(handler);

        foreach (var target in _localTargets)
            _rpc.AddLocalRpcTarget(target);

        // Enable trace logging for LSP communication
        _rpc.TraceSource.Switch.Level = SourceLevels.All;
        _rpc.TraceSource.Listeners.Add(new LoggerTraceListener(_logger));
        _rpc.Disconnected += (sender, args) => _logger.LogError(args.Exception,
            "DISCONNECTED: {Description} {Reason}",
            args.Description,
            args.Reason
        );

        _rpc.StartListening();
    }

    public Task DidOpenAsync(DidOpenTextDocumentParams @params, CancellationToken ct = default)
    {
        // StreamJsonRpc's Notify* helpers do not include a CancellationToken
        // parameter, so we cancel cooperatively by observing the token on the
        // returned task if the caller awaits it.
        ct.ThrowIfCancellationRequested();
        var task = NotifyAsync("textDocument/didOpen", @params);

        return ct.IsCancellationRequested ? Task.FromCanceled(ct) : task;
    }

    public Task DidCloseAsync(DidCloseTextDocumentParams @params, CancellationToken ct = default)
    {
        // StreamJsonRpc's Notify* helpers do not include a CancellationToken
        // parameter, so we cancel cooperatively by observing the token on the
        // returned task if the caller awaits it.
        ct.ThrowIfCancellationRequested();
        var task = NotifyAsync("textDocument/didClose", @params);

        return ct.IsCancellationRequested ? Task.FromCanceled(ct) : task;
    }

    private Task<T> InvokeAsync<T>(string methodName,
        object @params,
        CancellationToken cancellationToken) =>
        _rpc.InvokeWithParameterObjectAsync<T>(methodName, @params, cancellationToken);

    private Task NotifyAsync(string methodName, object @params) =>
        _rpc.NotifyWithParameterObjectAsync(methodName, @params);

    public Task<JsonNode> InitializeAsync(object @params, CancellationToken ct = default) =>
        InvokeAsync<JsonNode>("initialize", @params, ct);

    public Task InitializedAsync(object @params, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var task = NotifyAsync("initialized", @params);

        return ct.IsCancellationRequested ? Task.FromCanceled(ct) : task;
    }

    public Task NotifyAsync(string methodName, object @params, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var task = NotifyAsync(methodName, @params);

        return ct.IsCancellationRequested ? Task.FromCanceled(ct) : task;
    }

    public Task<Location[]> DefinitionAsync(DocumentClientRequest @params,
        CancellationToken ct = default) =>
        InvokeAsync<Location[]>("textDocument/definition",
            new TextDocumentPositionParams
            {
                TextDocument = @params.Document.ToDocumentIdentifier(),
                Position = @params.Position,
            },
            ct
        );

    public Task<Location[]> TypeDefinitionAsync(DocumentClientRequest @params,
        CancellationToken ct = default) =>
        InvokeAsync<Location[]>("textDocument/typeDefinition",
            new TextDocumentPositionParams
            {
                TextDocument = @params.Document.ToDocumentIdentifier(),
                Position = @params.Position,
            },
            ct
        );

    public Task<Location[]> ImplementationAsync(DocumentClientRequest @params,
        CancellationToken ct = default) =>
        InvokeAsync<Location[]>("textDocument/implementation",
            new TextDocumentPositionParams
            {
                TextDocument = @params.Document.ToDocumentIdentifier(),
                Position = @params.Position,
            },
            ct
        );

    public Task<Location[]> ReferencesAsync(DocumentClientRequest args,
        CancellationToken ct = default) =>
        InvokeAsync<Location[]>("textDocument/references",
            new ReferenceParams
            {
                TextDocument = args.Document.ToDocumentIdentifier(),
                Position = args.Position,
                Context = new ReferenceContext
                {
                    IncludeDeclaration = true,
                },
            },
            ct
        );

    public Task<Hover?> HoverAsync(DocumentClientRequest @params, CancellationToken ct = default) =>
        InvokeAsync<Hover?>("textDocument/hover",
            new TextDocumentPositionParams
            {
                TextDocument = @params.Document.ToDocumentIdentifier(),
                Position = @params.Position,
            },
            ct
        );

    public Task<CompletionList?> CompletionAsync(DocumentClientRequest @params,
        CancellationToken ct = default) =>
        InvokeAsync<CompletionList?>("textDocument/completion",
            new CompletionParams
            {
                TextDocument = @params.Document.ToDocumentIdentifier(),
                Position = @params.Position,
                Context = new CompletionContext
                {
                    TriggerKind = CompletionTriggerKind.Invoked,
                },
            },
            ct
        );

    public Task<SymbolInformation[]?> DocumentSymbolAsync(DocumentSymbolParams @params,
        CancellationToken ct = default) =>
        InvokeAsync<SymbolInformation[]?>("textDocument/documentSymbol", @params, ct);

    public Task<IEnumerable<WorkspaceSymbolResult>> WorkspaceSymbolAsync(
        WorkspaceSymbolParams @params,
        CancellationToken ct = default) =>
        InvokeAsync<IEnumerable<WorkspaceSymbolResult>>("workspace/symbol", @params, ct);

    public Task<FullDocumentDiagnosticReport?> DiagnosticAsync(TextDocumentDiagnosticParams @params,
        CancellationToken ct = default) =>
        InvokeAsync<FullDocumentDiagnosticReport?>("textDocument/diagnostic", @params, ct);

    public Task<WorkspaceEdit?> RenameAsync(RenameParams @params, CancellationToken ct = default) =>
        InvokeAsync<WorkspaceEdit?>("textDocument/rename", @params, ct);

    public Task<PrepareRenameResult?> PrepareRenameAsync(PrepareRenameParams @params,
        CancellationToken ct = default) =>
        InvokeAsync<PrepareRenameResult?>("textDocument/prepareRename", @params, ct);

    public Task ShutdownAsync(CancellationToken ct = default) =>
        InvokeAsync<object>("shutdown",
            new
            {
            },
            ct
        );

    public Task ExitAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var task = NotifyAsync("exit",
            new
            {
            }
        );

        return ct.IsCancellationRequested ? Task.FromCanceled(ct) : task;
    }

    public ValueTask DisposeAsync()
    {
        _rpc.Dispose();

        return ValueTask.CompletedTask;
    }

    public void Dispose() => _rpc.Dispose();
}
