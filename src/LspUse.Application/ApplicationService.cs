using System.Diagnostics;
using LspUse.Application.Configuration;
using LspUse.Application.Models;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.LanguageServerClient.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;
using StreamJsonRpc;

namespace LspUse.Application;

using System.IO;
using LanguageServerClient.Models;

public class ApplicationService : IApplicationService
{
    private readonly LanguageServerProcessConfiguration _config;
    private readonly IEnumerable<ILspNotificationHandler> _lspNotificationHandlers;
    private readonly ILogger<ApplicationService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // TODO: This needs to be wrapped in a process monitor
    private Process? _process;
    private JsonRpc? _rpc;
    private JsonRpcLspClient? _languageServer;

    private JsonRpcLspClient LanguageServer =>
        _languageServer ?? throw new InvalidOperationException("LSP client not initialized");

    /// <summary>
    /// Determines whether the Roslyn workspace has finished loading by checking
    /// the <c>workspace/projectInitializationComplete</c> notification
    /// completion task exposed by <see cref="WorkspaceNotificationHandler"/>.
    /// </summary>
    /// <returns><c>true</c> when the workspace load is complete or when the
    /// notification handler is not available (e.g. for non-C# workspaces);
    /// otherwise, <c>false</c>.</returns>
    private bool IsWorkspaceReady()
    {
        var handler = _lspNotificationHandlers.OfType<WorkspaceNotificationHandler>()
            .FirstOrDefault();

        // If the handler is not present we assume the workspace does not need
        // to signal readiness (non-Roslyn LS) and therefore treat it as ready.
        return handler is null || handler.WorkspaceInitialization.IsCompleted;
    }

    private static ApplicationServiceError WorkspaceLoadingError() =>
        new()
        {
            Message = "Workspace is still loading",
            ErrorCode = ErrorCode.WorkspaceLoadInProgress
        };

    public ApplicationService(IOptions<LanguageServerProcessConfiguration> options,
        IEnumerable<ILspNotificationHandler> handlers, ILogger<ApplicationService> logger,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);

        _config = options.Value;
        _lspNotificationHandlers = handlers ?? [];
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initialize invoked with config: {@Config}", _config);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _config.Command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _config.WorkspacePath
        };

        foreach (var arg in _config.Arguments ?? []) processStartInfo.ArgumentList.Add(arg);

        // TBD whether this is needed on linux
        processStartInfo.Environment.Add("DOTNET_USE_POLLING_FILE_WATCHER", "true");

        _process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true
        };

        // On process error event
        _process.ErrorDataReceived += (sender, args) =>
        {
            _logger.LogError("Process raised error: {ProcessData}", args.Data);
        };

        if (!_process.Start())
        {
            _logger.LogError("Failed to start process");

            throw new InvalidOperationException($"Failed to start process {_config.Command}");
        }

        _process.BeginErrorReadLine();

        _logger.LogInformation("Process started with PID: {ProcessId}", _process.Id);

        var jsonFormatter = new SystemTextJsonFormatter();
        jsonFormatter.JsonSerializerOptions.Converters.Add(new AbsoluteUriJsonConverter());

        _rpc = new JsonRpc(new HeaderDelimitedMessageHandler(_process.StandardInput.BaseStream,
            _process.StandardOutput.BaseStream, jsonFormatter));

        foreach (var h in _lspNotificationHandlers) _rpc.AddLocalRpcTarget(h);

        // Enable trace logging for LSP communication
        _rpc.TraceSource.Switch.Level = SourceLevels.All;
        _rpc.TraceSource.Listeners.Add(new LoggerTraceListener(_logger));

        _rpc.Disconnected += (sender, args) => _logger.LogError(args.Exception,
            "DISCONNECTED: {Description} {Reason}", args.Description, args.Reason);

        _rpc.StartListening();

        _languageServer = new JsonRpcLspClient(_rpc);

        var workspaceFullPath = Path.GetFullPath(_config.WorkspacePath);

        _logger.LogInformation("Sending Initialize request to LSP: {WorkspacePath}",
            workspaceFullPath);

        var initializeRequest = new InitializeParams
        {
            ProcessId = Environment.ProcessId,
            RootUri = new Uri(workspaceFullPath),
            Capabilities = new ClientCapabilities
            {
                Workspace = new WorkspaceClientCapabilities
                {
                    Diagnostic = null
                },
                TextDocument = new TextDocumentClientCapabilities
                {
                    PublishDiagnostics = new PublishDiagnosticsTextDocumentSetting
                    {
                        RelatedInformation = true,
                        VersionSupport = true,
                        CodeDescriptionSupport = true,
                        DataSupport = true
                    },
                    Diagnostic = new DiagnosticTextDocumentSetting
                    {
                        DynamicRegistration = true,
                        RelatedDocumentSupport = true
                    }
                }
            }
        };

        var serverCapabilities =
            await _languageServer.InitializeAsync(initializeRequest, cancellationToken);

        _logger.LogDebug("LSP Server replied with capabilities {@ServerCapabilities}",
            serverCapabilities);

        await _languageServer.InitializedAsync(new
        {
        }, cancellationToken);

        _logger.LogDebug("Sent Initialized notifcation");

        if (!string.IsNullOrWhiteSpace(_config.SolutionPath))
        {
            var solutionFullPath = Path.GetFullPath(_config.SolutionPath);
            _logger.LogInformation("Opening solution: {SolutionPath}", solutionFullPath);

            await _languageServer.NotifyAsync("solution/open", new
            {
                solution = new Uri(solutionFullPath)
            }, cancellationToken);
        }
        else if (_config.ProjectPaths is { Count: > 0 })
        {
            _logger.LogInformation("Opening projects: {ProjectPaths}",
                string.Join(", ", _config.ProjectPaths));
            var uris = _config.ProjectPaths.Select(p => new Uri(Path.GetFullPath(p))).ToArray();
            await _languageServer.NotifyAsync("project/open", new
            {
                projects = uris
            }, cancellationToken);
        }

        _logger.LogDebug("Initialization completed");
    }

    public async Task WaitForWorkspaceReadyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Waiting for workspace to load");

        // TODO: Either don't inject the Workspace Notification Hanlder for non C# projects or put another if here to avoid waiting
        var handler = _lspNotificationHandlers.OfType<WorkspaceNotificationHandler>()
            .FirstOrDefault();

        if (handler is not null) await handler.WorkspaceInitialization;

        _logger.LogInformation("Successully loaded workspace");
    }

    public async Task<OneOf<FindReferencesSuccess, ApplicationServiceError>> FindReferencesAsync(
        FindReferencesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(FindReferencesAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async (fileUri) =>
            {
                var references = await LanguageServer.ReferencesAsync(new DocumentClientRequest
                {
                    Document = fileUri,
                    Position = request.Position.ToZeroBased()
                }, cancellationToken);

                var locations = references.Select(x => x.ToSymbolLocation());
                var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                return new FindReferencesSuccess
                {
                    Value = enrichedLocations
                };
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[FindReferences] Error occurred while processing find references request for {FilePath}:{Line}:{Character}",
                request.FilePath, request.Position.Line, request.Position.Character);

            return new ApplicationServiceError
            {
                Message = "Failed to find references",
                Exception = ex
            };
        }
    }

    public async Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToDefinitionAsync(
        GoToRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GoToDefinitionAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async (fileUri) =>
            {
                var definitions = await LanguageServer.DefinitionAsync(new DocumentClientRequest
                {
                    Document = fileUri,
                    Position = request.Position.ToZeroBased()
                }, cancellationToken);

                var locations = definitions.Select(x => x.ToSymbolLocation());
                var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                return new GoToSuccess
                {
                    Locations = enrichedLocations
                };
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GoToDefinition] Error occurred while processing go to definition request for {FilePath}:{Line}:{Character}",
                request.FilePath, request.Position.Line, request.Position.Character);

            return new ApplicationServiceError
            {
                Message = "Failed to go to definition",
                Exception = ex
            };
        }
    }

    public async Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToTypeDefinitionAsync(
        GoToRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GoToTypeDefinitionAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async (fileUri) =>
            {
                var typeDefinitions = await LanguageServer.TypeDefinitionAsync(
                    new DocumentClientRequest
                    {
                        Document = fileUri,
                        Position = request.Position.ToZeroBased()
                    }, cancellationToken);

                var locations = typeDefinitions.Select(x => x.ToSymbolLocation());
                var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                return new GoToSuccess
                {
                    Locations = enrichedLocations
                };
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GoToTypeDefinition] Error occurred while processing go to type definition request for {FilePath}:{Line}:{Character}",
                request.FilePath, request.Position.Line, request.Position.Character);

            return new ApplicationServiceError
            {
                Message = "Failed to go to type definition",
                Exception = ex
            };
        }
    }

    public async Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToImplementationAsync(
        GoToRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GoToImplementationAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async (fileUri) =>
            {
                var implementations = await LanguageServer.ImplementationAsync(
                    new DocumentClientRequest
                    {
                        Document = fileUri,
                        Position = request.Position.ToZeroBased()
                    }, cancellationToken);

                var locations = implementations.Select(x => x.ToSymbolLocation());
                var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                return new GoToSuccess
                {
                    Locations = enrichedLocations
                };
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GoToImplementation] Error occurred while processing go to implementation request for {FilePath}:{Line}:{Character}",
                request.FilePath, request.Position.Line, request.Position.Character);

            return new ApplicationServiceError
            {
                Message = "Failed to go to implementation",
                Exception = ex
            };
        }
    }

    private static string ExtractLanguageFromExt(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" or ".csx" => "csharp",
            ".py" => "python",
            ".ts" => "typescript",
            ".js" => "javascript",
            _ => "plaintext"
        };

    public async Task<OneOf<CompletionSuccess, ApplicationServiceError>> CompletionAsync(
        CompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[Completion] File:{FilePath} Position:{Line}:{Character}",
            request.FilePath, request.Position.Line, request.Position.Character);

        _logger.LogTrace("[Process] Pid:{Pid} Exited:{HasExited}", _process?.Id,
            _process?.HasExited);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async (fileUri) =>
            {
                var completionList = await LanguageServer.CompletionAsync(new DocumentClientRequest
                {
                    Document = fileUri,
                    Position = request.Position.ToZeroBased()
                }, cancellationToken);

                var count = completionList?.Items?.Length ?? 0;

                return new CompletionSuccess
                {
                    Items = count > 0 ? completionList?.Items ?? [] : [],
                    IsIncomplete = completionList?.IsIncomplete ?? false
                };
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Completion] Error occurred while processing completion request for {FilePath}:{Line}:{Character}",
                request.FilePath, request.Position.Line, request.Position.Character);

            return new ApplicationServiceError
            {
                Message = "Failed to get completion suggestions",
                Exception = ex
            };
        }
    }

    public async Task<OneOf<HoverSuccess, ApplicationServiceError>> HoverAsync(HoverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[Hover] File:{FilePath} Position:{Line}:{Character}",
            request.FilePath, request.Position.Line, request.Position.Character);

        _logger.LogTrace("[Process] Pid:{Pid} Exited:{HasExited}", _process?.Id,
            _process?.HasExited);

        try
        {
            EnsureFileExists(request.FilePath, out var absoluteFileUri);

            await OpenFileOnLspAsync(absoluteFileUri, cancellationToken);

            var hover = await LanguageServer.HoverAsync(new DocumentClientRequest
            {
                Document = absoluteFileUri,
                Position = request.Position.ToZeroBased()
            }, cancellationToken);

            _logger.LogInformation("[Hover] Content:{Content}", hover?.Contents?.Value);

            await CloseFileOnLspAsync(absoluteFileUri, cancellationToken);

            return new HoverSuccess
            {
                Value = hover?.Contents?.Value ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Hover] Error occurred while processing hover request for {FilePath}:{Line}:{Character}",
                request.FilePath, request.Position.Line, request.Position.Character);

            return new ApplicationServiceError
            {
                Message = "Failed to get hover information",
                Exception = ex
            };
        }
    }

    public async Task<OneOf<SearchSymbolSuccess, ApplicationServiceError>> SearchSymbolAsync(
        SearchSymbolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        try
        {
            var matchingSymbols = await LanguageServer.WorkspaceSymbolAsync(
                new WorkspaceSymbolParams
                {
                    Query = request.Query
                }, cancellationToken);

            var documentSymbols = matchingSymbols.Select(s => new DocumentSymbol
                {
                    Name = s.Name ?? string.Empty,
                    Kind = s.Kind?.ToString() ?? "Unknown",
                    ContainerName = s.ContainerName,
                    Location = s.Location?.ToSymbolLocation()
                })
                .ToList();

            // Extract locations, enrich them, and create a lookup
            var locations = documentSymbols.Where(s => s.Location != null).Select(s => s.Location!);
            var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);
            var enrichedLookup = enrichedLocations.ToLookup(loc =>
                $"{loc.FilePath}:{loc.StartLine}:{loc.StartCharacter}:{loc.EndLine}:{loc.EndCharacter}");

            // Update document symbols with enriched locations
            var enrichedSymbols = documentSymbols.Select(symbol =>
            {
                if (symbol.Location == null) return symbol;

                var key =
                    $"{symbol.Location.FilePath}:{symbol.Location.StartLine}:{symbol.Location.StartCharacter}:{symbol.Location.EndLine}:{symbol.Location.EndCharacter}";
                var enrichedLocation = enrichedLookup[key].FirstOrDefault();

                return symbol with
                {
                    Location = enrichedLocation ?? symbol.Location
                };
            });

            return new SearchSymbolSuccess
            {
                Value = enrichedSymbols
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[SearchSymbol] Error occurred while processing search symbol request for query: {Query}",
                request.Query);

            return new ApplicationServiceError
            {
                Message = "Failed to search symbols",
                Exception = ex
            };
        }
    }

    public Task<OneOf<WindowLog, ApplicationServiceError>> GetWindowLogMessagesAsync(
        WindowLogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("[GetWindowLogMessages] Retrieving window log messages");

        _logger.LogTrace("[Process] Pid:{Pid} Exited:{HasExited}", _process?.Id,
            _process?.HasExited);

        try
        {
            var windowNotificationHandler = _lspNotificationHandlers
                .OfType<WindowNotificationHandler>()
                .FirstOrDefault();

            if (windowNotificationHandler == null)
            {
                _logger.LogWarning("[GetWindowLogMessages] WindowNotificationHandler not found");

                var empty = new WindowLogMessage[]
                {
                };

                return Task.FromResult(
                    OneOf<WindowLog, ApplicationServiceError>.FromT0(new WindowLog(empty)));
            }

            var logMessages = windowNotificationHandler.LogMessages.Select(x =>
                    new WindowLogMessage(x.Message ?? string.Empty, x.MessageType))
                .ToList();

            _logger.LogInformation("[GetWindowLogMessages] Retrieved {Count} log messages",
                logMessages.Count);

            return Task.FromResult(
                OneOf<WindowLog, ApplicationServiceError>.FromT0(new WindowLog(logMessages)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GetWindowLogMessages] Error occurred while retrieving window log messages");

            return Task.FromResult(OneOf<WindowLog, ApplicationServiceError>.FromT1(
                new ApplicationServiceError
                {
                    Message = "Failed to get window log messages",
                    Exception = ex
                }));
        }
    }

    // TODO: this has many concerns, to sort
    private void EnsureFileExists(string filePath, out Uri fileUri)
    {
        fileUri = new Uri(Path.GetFullPath(filePath));

        // Ensure file exists. TODO: When using results this should be an error code maybe mapped
        if (File.Exists(fileUri.LocalPath) is not true)
        {
            _logger.LogWarning("No file exists at {FilePath}", fileUri.LocalPath);

            throw new FileNotFoundException(fileUri.LocalPath);
        }
    }

    private async Task OpenFileOnLspAsync(Uri fileUri, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(fileUri.LocalPath, cancellationToken);

        // Open file on the LSP
        await LanguageServer.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Version = 1,
                Uri = fileUri,
                LanguageId = ExtractLanguageFromExt(fileUri.LocalPath),
                Text = content
            }
        }, cancellationToken);

        _logger.LogDebug("File {FilePath} opened on LSP", fileUri.LocalPath);
    }

    private async Task CloseFileOnLspAsync(Uri fileUri, CancellationToken cancellationToken)
    {
        //TODO: Do we need to check for file existance again? No don't think so

        await LanguageServer.DidCloseAsync(new DidCloseTextDocumentParams
        {
            TextDocument = fileUri.ToDocumentIdentifier()
        }, cancellationToken);

        _logger.LogDebug("File {FilePath} closed on LSP", fileUri.LocalPath);
    }

    private async Task<T> ExecuteWithFileLifecycleAsync<T>(string filePath,
        Func<Uri, Task<T>> operation, CancellationToken cancellationToken)
    {
        // Ensure file exists and gets its absolute URI
        EnsureFileExists(filePath, out var absoluteFileUri);

        // Open the file on the LSP
        await OpenFileOnLspAsync(absoluteFileUri, cancellationToken);

        try
        {
            // Perform action on file
            return await operation(absoluteFileUri);
        }
        finally
        {
            // Close file on LSP
            await CloseFileOnLspAsync(absoluteFileUri, cancellationToken);
        }
    }

    public async Task<OneOf<GetSymbolsSuccess, ApplicationServiceError>> GetDocumentSymbolsAsync(
        GetSymbolsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async (fileUri) =>
            {
                var documentSymbols = await LanguageServer.DocumentSymbolAsync(
                    new DocumentSymbolParams
                    {
                        TextDocument = fileUri.ToDocumentIdentifier()
                    }, cancellationToken);

                var symbols = documentSymbols?.Select(x => new DocumentSymbol
                    {
                        Name = x.Name,
                        ContainerName = x.ContainerName,
                        Kind = x.Kind.ToString(),
                        Location = x.Location?.ToSymbolLocation()
                    })
                    .ToList() ?? [];

                // Extract locations, enrich them, and create a lookup
                var locations = symbols.Where(s => s.Location != null).Select(s => s.Location!);
                var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);
                var enrichedLookup = enrichedLocations.ToLookup(loc =>
                    $"{loc.FilePath}:{loc.StartLine}:{loc.StartCharacter}:{loc.EndLine}:{loc.EndCharacter}");

                // Update document symbols with enriched locations
                var enrichedSymbols = symbols.Select(symbol =>
                {
                    if (symbol.Location == null) return symbol;

                    var key =
                        $"{symbol.Location.FilePath}:{symbol.Location.StartLine}:{symbol.Location.StartCharacter}:{symbol.Location.EndLine}:{symbol.Location.EndCharacter}";
                    var enrichedLocation = enrichedLookup[key].FirstOrDefault();

                    return symbol with
                    {
                        Location = enrichedLocation ?? symbol.Location
                    };
                });

                return new GetSymbolsSuccess
                {
                    Symbols = enrichedSymbols
                };
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GetDocumentSymbols] Error occurred while processing get document symbols request for {FilePath}",
                request.FilePath);

            return new ApplicationServiceError
            {
                Message = "Failed to get document symbols",
                Exception = ex
            };
        }
    }

    public async Task<OneOf<IEnumerable<DocumentDiagnostic>, ApplicationServiceError>>
        GetDocumentDiagnosticsAsync(DocumentDiagnosticsRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GetDocumentDiagnosticsAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async fileUri =>
            {
                var clientCapabilityRegistrationHandler = _lspNotificationHandlers
                    .OfType<ClientCapabilityRegistrationHandler>()
                    .FirstOrDefault();

                if (clientCapabilityRegistrationHandler == null)
                {
                    _logger.LogWarning(
                        "[GetDocumentDiagnosticsAsync] ClientCapabilityRegistrationHandler not found");

                    return [];
                }

                // Check if registrations are completed or if we have any registrations
                if (!clientCapabilityRegistrationHandler.RegistrationCompleted.IsCompleted &&
                    !clientCapabilityRegistrationHandler.Registrations.Any())
                {
                    _logger.LogWarning(
                        "[GetDocumentDiagnosticsAsync] No diagnostic registrations available");

                    return [];
                }

                var diagnostics = new List<Diagnostic>();

                // Get all diagnostic registrations
                var registrations = clientCapabilityRegistrationHandler.Registrations.ToArray();
                var textDocumentDiagnosticRegistrations = registrations
                    .Where(r => r.Method == "textDocument/diagnostic")
                    .ToArray();

                foreach (var registration in textDocumentDiagnosticRegistrations)
                {
                    var identifier = registration.RegisterOptions?.Identifier;

                    if (string.IsNullOrEmpty(identifier))
                        continue;

                    _logger.LogDebug(
                        "[GetDocumentDiagnosticsAsync] Fetching diagnostics for identifier: {Identifier}",
                        identifier);

                    try
                    {
                        var response = await LanguageServer.DiagnosticAsync(
                            new TextDocumentDiagnosticParams
                            {
                                TextDocument = fileUri.ToDocumentIdentifier(),
                                Identifier = identifier
                            }, cancellationToken);

                        if (response?.Items != null) diagnostics.AddRange(response.Items);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[GetDocumentDiagnosticsAsync] Failed to fetch diagnostics for identifier: {Identifier}",
                            identifier);
                    }
                }

                // Convert to our model format
                var documentDiagnostics = new List<DocumentDiagnostic>();

                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Range?.Start == null || diagnostic.Range?.End == null)
                        continue;

                    var severity = diagnostic.Severity switch
                    {
                        DiagnosticSeverity.Error => "Error",
                        DiagnosticSeverity.Warning => "Warning",
                        DiagnosticSeverity.Information => "Information",
                        DiagnosticSeverity.Hint => "Hint",
                        _ => "Unknown"
                    };

                    documentDiagnostics.Add(new DocumentDiagnostic
                    {
                        Severity = severity,
                        StartLine = (uint)diagnostic.Range.Start.Line,
                        StartCharacter = (uint)diagnostic.Range.Start.Character,
                        EndLine = (uint)diagnostic.Range.End.Line,
                        EndCharacter = (uint)diagnostic.Range.End.Character,
                        Message = diagnostic.Message ?? string.Empty,
                        Code = diagnostic.Code,
                        CodeDescription = diagnostic.CodeDescription?.Href
                    });
                }

                // Enrich diagnostics with text content
                var enrichedDiagnostics =
                    await documentDiagnostics.EnrichWithTextAsync(request.FilePath,
                        cancellationToken);

                // Sort by severity (Error → Warning → Information → Hint) then by line number
                return enrichedDiagnostics.OrderBy(d => d.SeverityOrder)
                    .ThenBy(d => d.StartLine)
                    .ThenBy(d => d.StartCharacter)
                    .ToArray();
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GetDocumentDiagnosticsAsync] Error occurred while processing get document diagnostics request for {FilePath}",
                request.FilePath);

            return new ApplicationServiceError
            {
                Message = "Failed to get document diagnostics",
                Exception = ex
            };
        }
    }

    public async Task<OneOf<RenameSymbolSuccess, ApplicationServiceError>> RenameSymbolAsync(
        RenameSymbolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady()) return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(RenameSymbolAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath, async (fileUri) =>
            {
                var workspaceEdit = await LanguageServer.RenameAsync(new RenameParams
                {
                    TextDocument = fileUri.ToDocumentIdentifier(),
                    Position = request.Position.ToZeroBased(),
                    NewName = request.NewName
                }, cancellationToken);

                if (workspaceEdit == null)
                {
                    _logger.LogWarning("LSP returned null workspace edit for rename operation");

                    return new RenameSymbolSuccess
                    {
                        Success = false,
                        Errors = ["LSP returned no changes for rename operation"],
                        ChangedFiles = [],
                        TotalEditsApplied = 0,
                        TotalFilesChanged = 0,
                        TotalLinesChanged = 0
                    };
                }

                // TODO: Inject from DI
                var applicator =
                    new WorkspaceEditApplicator(
                        _loggerFactory.CreateLogger<WorkspaceEditApplicator>());
                var applicatorResult =
                    await applicator.ApplyAsync(workspaceEdit, cancellationToken);

                if (applicatorResult.HasErrors)
                {
                    _logger.LogError("Failed to apply rename changes: {Errors}",
                        string.Join(", ", applicatorResult.Errors));

                    return new RenameSymbolSuccess
                    {
                        Success = false,
                        Errors = applicatorResult.Errors,
                        ChangedFiles = [],
                        TotalEditsApplied = 0,
                        TotalFilesChanged = 0,
                        TotalLinesChanged = 0
                    };
                }

                _logger.LogInformation(
                    "Successfully renamed symbol: {TotalFilesChanged} files, {TotalEditsApplied} edits, {TotalLinesChanged} lines",
                    applicatorResult.TotalFilesChanged, applicatorResult.TotalEditsApplied,
                    applicatorResult.TotalLinesChanged);

                return new RenameSymbolSuccess
                {
                    Success = true,
                    Errors = [],
                    ChangedFiles = applicatorResult.FilesChanged.Select(r => r.FilePath),
                    TotalFilesChanged = applicatorResult.TotalFilesChanged,
                    TotalEditsApplied = applicatorResult.TotalEditsApplied,
                    TotalLinesChanged = applicatorResult.TotalLinesChanged
                };
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RenameSymbol] Error occurred while processing rename symbol request for {FilePath}:{Line}:{Character} -> {NewName}",
                request.FilePath, request.Position.Line, request.Position.Character,
                request.NewName);

            return new ApplicationServiceError
            {
                Message = "Failed to rename symbol",
                Exception = ex
            };
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down LSP server gracefully");

        try
        {
            if (_languageServer is not null)
            {
                _logger.LogDebug("Sending LSP shutdown request");
                await _languageServer.ShutdownAsync(cancellationToken);

                _logger.LogDebug("Sending LSP exit notification");
                await _languageServer.ExitAsync(cancellationToken);
            }

            if (_process is { HasExited: false })
            {
                _logger.LogDebug("Waiting for LSP server process to exit");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                try
                {
                    await _process.WaitForExitAsync(cts.Token);
                    _logger.LogInformation("LSP server process exited gracefully");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning(
                        "LSP server process did not exit within timeout, killing process");
                    if (!_process.HasExited) _process.Kill(true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LSP server shutdown");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _rpc?.Dispose();

            if (_process is { HasExited: false })
            {
                _process.StandardInput.Close();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                try
                {
                    await _process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!_process.HasExited) _process.Kill(true);
                }
            }
        }
        catch
        {
            // Ignore dispose errors.
        }

        GC.SuppressFinalize(this);
    }
}
