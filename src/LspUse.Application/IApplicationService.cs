using LspUse.Application.Models;
using LspUse.LanguageServerClient.Handlers;
using OneOf;

namespace LspUse.Application;

public interface IApplicationService : IAsyncDisposable
{
    Task InitialiseAsync(CancellationToken cancellationToken = default);

    Task WaitForWorkspaceReadyAsync(CancellationToken ct = default);

    Task<OneOf<FindReferencesSuccess, ApplicationServiceError>> FindReferencesAsync(
        FindReferencesRequest request, CancellationToken cancellationToken = default);

    Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToDefinitionAsync(GoToRequest request,
        CancellationToken cancellationToken = default);

    Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToTypeDefinitionAsync(GoToRequest request,
        CancellationToken cancellationToken = default);

    Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToImplementationAsync(GoToRequest request,
        CancellationToken cancellationToken = default);

    Task<OneOf<CompletionSuccess, ApplicationServiceError>> CompletionAsync(
        CompletionRequest request, CancellationToken cancellationToken = default);

    Task<OneOf<HoverSuccess, ApplicationServiceError>> HoverAsync(HoverRequest request,
        CancellationToken cancellationToken = default);

    Task<OneOf<SearchSymbolSuccess, ApplicationServiceError>> SearchSymbolAsync(
        SearchSymbolRequest request, CancellationToken cancellationToken = default);

    Task<OneOf<WindowLog, ApplicationServiceError>> GetWindowLogMessagesAsync(
        WindowLogRequest request, CancellationToken cancellationToken = default);

    Task<OneOf<GetSymbolsSuccess, ApplicationServiceError>> GetDocumentSymbolsAsync(
        GetSymbolsRequest request, CancellationToken cancellationToken = default);

    Task<OneOf<IEnumerable<DocumentDiagnostic>, ApplicationServiceError>>
        GetDocumentDiagnosticsAsync(DocumentDiagnosticsRequest request,
            CancellationToken cancellationToken = default);

    Task<OneOf<RenameSymbolSuccess, ApplicationServiceError>> RenameSymbolAsync(
        RenameSymbolRequest request, CancellationToken cancellationToken = default);

    DefaultNotificationHandler? GetDefaultNotificationHandler();

    DefaultRequestHandler? GetDefaultRequestHandler();

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
