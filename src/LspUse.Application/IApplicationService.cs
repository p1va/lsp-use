using LspUse.Application.Models;

namespace LspUse.Application;

public interface IApplicationService : IAsyncDisposable
{
    Task InitialiseAsync(CancellationToken cancellationToken = default);

    Task WaitForWorkspaceReadyAsync(CancellationToken ct = default);

    Task<FindReferencesResult> FindReferencesAsync(FindReferencesRequest request, CancellationToken cancellationToken = default);

    Task<GoToResult> GoToDefinitionAsync(GoToRequest request, CancellationToken cancellationToken = default);
    Task<GoToResult> GoToTypeDefinitionAsync(GoToRequest request, CancellationToken cancellationToken = default);
    Task<GoToResult> GoToImplementationAsync(GoToRequest request, CancellationToken cancellationToken = default);

    Task<CompletionResult> CompletionAsync(CompletionRequest request, CancellationToken cancellationToken = default);

    Task<HoverResult> HoverAsync(HoverRequest request, CancellationToken cancellationToken = default);

    Task<SearchSymbolResponse> SearchSymbolAsync(SearchSymbolRequest request, CancellationToken cancellationToken = default);

    Task<WindowLog> GetWindowLogMessagesAsync(WindowLogRequest request, CancellationToken cancellationToken = default);

    Task<GetSymbolsResult> GetDocumentSymbolsAsync(GetSymbolsRequest request, CancellationToken cancellationToken = default);

    Task<IEnumerable<DocumentDiagnostic>> GetDocumentDiagnosticsAsync(DocumentDiagnosticsRequest request, CancellationToken cancellationToken = default);

    Task<RenameSymbolResult> RenameSymbolAsync(RenameSymbolRequest request, CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
