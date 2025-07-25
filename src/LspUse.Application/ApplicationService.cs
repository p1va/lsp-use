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
    private readonly LanguageIdMapper _languageIdMapper;
    private readonly LspConfigurationService _lspConfigurationService;

    // TODO: This needs to be wrapped in a process monitor
    private Process? _process;
    private JsonRpc? _rpc;
    private JsonRpcLspClient? _languageServer;
    private System.Text.Json.Nodes.JsonNode? _serverCapabilities;

    private JsonRpcLspClient LanguageServer =>
        _languageServer ?? throw new InvalidOperationException("LSP client not initialized");

    private bool IsWorkspaceReady()
    {
        // Only wait for workspace loading if C# specific files (.sln or .csproj) are provided
        if (!IsCSharpWorkspace())
            return true;

        var handler = _lspNotificationHandlers
            .OfType<WorkspaceNotificationHandler>()
            .FirstOrDefault();

        // If the handler is not present we assume the workspace does not need
        // to signal readiness (non-Roslyn LS) and therefore treat it as ready.
        return handler is null || handler.WorkspaceInitialization.IsCompleted;
    }

    private static ApplicationServiceError WorkspaceLoadingError() =>
        new()
        {
            Message = "Workspace is still loading",
            ErrorCode = ErrorCode.WorkspaceLoadInProgress,
        };

    private bool IsCSharpWorkspace() =>
        !string.IsNullOrWhiteSpace(_config.SolutionPath) || _config.ProjectPaths?.Any() == true;

    public ApplicationService(IOptions<LanguageServerProcessConfiguration> options,
        IEnumerable<ILspNotificationHandler> handlers,
        ILogger<ApplicationService> logger,
        ILoggerFactory loggerFactory,
        LanguageIdMapper languageIdMapper,
        LspConfigurationService lspConfigurationService)
    {
        ArgumentNullException.ThrowIfNull(options);

        _config = options.Value;
        _lspNotificationHandlers = handlers ?? [];
        _logger = logger;
        _loggerFactory = loggerFactory;
        _languageIdMapper = languageIdMapper;
        _lspConfigurationService = lspConfigurationService;
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
            WorkingDirectory = _config.WorkspacePath,
        };

        foreach (var arg in _config.Arguments ?? [])
            processStartInfo.ArgumentList.Add(arg);

        // Add environment variables from configuration
        if (_config.Environment != null)
        {
            foreach (var envVar in _config.Environment)
            {
                processStartInfo.Environment.Add(envVar.Key, envVar.Value);
            }
        }

        // TBD whether this is needed on linux
        processStartInfo.Environment.Add("DOTNET_USE_POLLING_FILE_WATCHER", "true");

        _process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
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
                _process.StandardOutput.BaseStream,
                jsonFormatter
            )
        );

        foreach (var h in _lspNotificationHandlers)
            _rpc.AddLocalRpcTarget(h);

        // Enable trace logging for LSP communication
        _rpc.TraceSource.Switch.Level = SourceLevels.All;
        _rpc.TraceSource.Listeners.Add(new LoggerTraceListener(_logger));

        _rpc.Disconnected += (sender, args) => _logger.LogError(args.Exception,
            "DISCONNECTED: {Description} {Reason}",
            args.Description,
            args.Reason
        );

        _rpc.StartListening();

        _languageServer = new JsonRpcLspClient(_rpc);

        var workspaceFullPath = Path.GetFullPath(_config.WorkspacePath);

        _logger.LogInformation("Sending Initialize request to LSP: {WorkspacePath}",
            workspaceFullPath
        );

        var initializeRequest = new InitializeParams
        {
            ProcessId = Environment.ProcessId,
            RootUri = new Uri(workspaceFullPath),
            WorkspaceFolders =
            [
                new WorkspaceFolder
                {
                    Name = workspaceFullPath,
                    Uri = new Uri(workspaceFullPath),
                },
            ],
            Capabilities = new ClientCapabilities
            {
                Workspace = new WorkspaceClientCapabilities
                {
                    Diagnostic = null,
                },
                TextDocument = new TextDocumentClientCapabilities
                {
                    PublishDiagnostics = new PublishDiagnosticsTextDocumentSetting
                    {
                        RelatedInformation = true,
                        VersionSupport = true,
                        CodeDescriptionSupport = true,
                        DataSupport = true,
                    },
                    Diagnostic = new DiagnosticTextDocumentSetting
                    {
                        DynamicRegistration = true,
                        RelatedDocumentSupport = true,
                    },
                },
            },
        };

        // These are the capabilities supported by the server
        var serverCapabilities = await _languageServer
            .InitializeAsync(initializeRequest, cancellationToken);

        // Store server capabilities for later use (e.g., for extracting diagnostic providers)
        _serverCapabilities = serverCapabilities;

        _logger.LogDebug("LSP Server replied with capabilities {@ServerCapabilities}",
            serverCapabilities
        );

        await _languageServer.InitializedAsync(new
        {
        },
            cancellationToken
        );

        _logger.LogDebug("Sent Initialized notifcation");

        if (!string.IsNullOrWhiteSpace(_config.SolutionPath))
        {
            var solutionFullPath = Path.GetFullPath(_config.SolutionPath);
            _logger.LogInformation("Opening solution: {SolutionPath}", solutionFullPath);

            await _languageServer.NotifyAsync("solution/open",
                new
                {
                    solution = new Uri(solutionFullPath),
                },
                cancellationToken
            );
        }
        else if (_config.ProjectPaths is { Count: > 0, })
        {
            _logger.LogInformation("Opening projects: {ProjectPaths}",
                string.Join(", ", _config.ProjectPaths)
            );
            var uris = _config
                .ProjectPaths.Select(p => new Uri(Path.GetFullPath(p)))
                .ToArray();
            await _languageServer.NotifyAsync("project/open",
                new
                {
                    projects = uris,
                },
                cancellationToken
            );
        }

        _logger.LogDebug("Initialization completed");
    }

    public async Task WaitForWorkspaceReadyAsync(CancellationToken cancellationToken = default)
    {
        // Only wait for workspace loading if C# specific files (.sln or .csproj) are provided
        if (!IsCSharpWorkspace())
        {
            _logger.LogInformation("Non-C# workspace detected, skipping workspace loading wait");

            return;
        }

        _logger.LogInformation("Waiting for workspace to load");

        var handler = _lspNotificationHandlers
            .OfType<WorkspaceNotificationHandler>()
            .FirstOrDefault();

        if (handler is not null)
            await handler.WorkspaceInitialization;

        _logger.LogInformation("Successully loaded workspace");
    }

    public async Task<OneOf<FindReferencesSuccess, ApplicationServiceError>> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(FindReferencesAsync), request);

        try
        {
            return await ExecuteWithFileLifecycleAsync(request.FilePath,
                async (fileUri) =>
                {
                    var references = await LanguageServer
                        .ReferencesAsync(new DocumentClientRequest
                        {
                            Document = fileUri,
                            Position = request.Position.ToZeroBased(),
                        },
                            cancellationToken
                        );

                    var locations = references.Select(x => x.ToSymbolLocation());
                    var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                    return new FindReferencesSuccess
                    {
                        Value = enrichedLocations,
                    };
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[FindReferences] Error occurred while processing find references request for {FilePath}:{Line}:{Character}",
                request.FilePath,
                request.Position.Line,
                request.Position.Character
            );

            return new ApplicationServiceError
            {
                Message = "Failed to find references",
                Exception = ex,
            };
        }
    }

    public async Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToDefinitionAsync(
        GoToRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GoToDefinitionAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath,
                async (fileUri) =>
                {
                    var definitions = await LanguageServer.DefinitionAsync(new DocumentClientRequest
                    {
                        Document = fileUri,
                        Position = request.Position.ToZeroBased(),
                    },
                        cancellationToken
                    );

                    var locations = definitions.Select(x => x.ToSymbolLocation());
                    var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                    return new GoToSuccess
                    {
                        Locations = enrichedLocations,
                    };
                },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GoToDefinition] Error occurred while processing go to definition request for {FilePath}:{Line}:{Character}",
                request.FilePath,
                request.Position.Line,
                request.Position.Character
            );

            return new ApplicationServiceError
            {
                Message = "Failed to go to definition",
                Exception = ex,
            };
        }
    }

    public async Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToTypeDefinitionAsync(
        GoToRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GoToTypeDefinitionAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath,
                async (fileUri) =>
                {
                    var typeDefinitions = await LanguageServer.TypeDefinitionAsync(
                        new DocumentClientRequest
                        {
                            Document = fileUri,
                            Position = request.Position.ToZeroBased(),
                        },
                        cancellationToken
                    );

                    var locations = typeDefinitions.Select(x => x.ToSymbolLocation());
                    var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                    return new GoToSuccess
                    {
                        Locations = enrichedLocations,
                    };
                },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GoToTypeDefinition] Error occurred while processing go to type definition request for {FilePath}:{Line}:{Character}",
                request.FilePath,
                request.Position.Line,
                request.Position.Character
            );

            return new ApplicationServiceError
            {
                Message = "Failed to go to type definition",
                Exception = ex,
            };
        }
    }

    public async Task<OneOf<GoToSuccess, ApplicationServiceError>> GoToImplementationAsync(
        GoToRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GoToImplementationAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath,
                async (fileUri) =>
                {
                    var implementations = await LanguageServer.ImplementationAsync(
                        new DocumentClientRequest
                        {
                            Document = fileUri,
                            Position = request.Position.ToZeroBased(),
                        },
                        cancellationToken
                    );

                    var locations = implementations.Select(x => x.ToSymbolLocation());
                    var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);

                    return new GoToSuccess
                    {
                        Locations = enrichedLocations,
                    };
                },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GoToImplementation] Error occurred while processing go to implementation request for {FilePath}:{Line}:{Character}",
                request.FilePath,
                request.Position.Line,
                request.Position.Character
            );

            return new ApplicationServiceError
            {
                Message = "Failed to go to implementation",
                Exception = ex,
            };
        }
    }

    public async Task<OneOf<CompletionSuccess, ApplicationServiceError>> CompletionAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[Completion] File:{FilePath} Position:{Line}:{Character}",
            request.FilePath,
            request.Position.Line,
            request.Position.Character
        );

        _logger.LogTrace("[Process] Pid:{Pid} Exited:{HasExited}",
            _process?.Id,
            _process?.HasExited
        );

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath,
                async (fileUri) =>
                {
                    var completionList = await LanguageServer.CompletionAsync(
                        new DocumentClientRequest
                        {
                            Document = fileUri,
                            Position = request.Position.ToZeroBased(),
                        },
                        cancellationToken
                    );

                    var count = completionList?.Items?.Length ?? 0;

                    return new CompletionSuccess
                    {
                        Items = count > 0 ? completionList?.Items ?? [] : [],
                        IsIncomplete = completionList?.IsIncomplete ?? false,
                    };
                },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Completion] Error occurred while processing completion request for {FilePath}:{Line}:{Character}",
                request.FilePath,
                request.Position.Line,
                request.Position.Character
            );

            return new ApplicationServiceError
            {
                Message = "Failed to get completion suggestions",
                Exception = ex,
            };
        }
    }

    public async Task<OneOf<HoverSuccess, ApplicationServiceError>> HoverAsync(HoverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[Hover] File:{FilePath} Position:{Line}:{Character}",
            request.FilePath,
            request.Position.Line,
            request.Position.Character
        );

        _logger.LogTrace("[Process] Pid:{Pid} Exited:{HasExited}",
            _process?.Id,
            _process?.HasExited
        );

        try
        {
            EnsureFileExists(request.FilePath, out var absoluteFileUri);

            await OpenFileOnLspAsync(absoluteFileUri, cancellationToken);
            
            // First, get document symbols to find what symbol was clicked
            DocumentSymbol? clickedSymbol = null;
            try
            {
                var documentSymbols = await LanguageServer.DocumentSymbolAsync(
                    new DocumentSymbolParams
                    {
                        TextDocument = absoluteFileUri.ToDocumentIdentifier(),
                    },
                    cancellationToken
                );
                var symbols = documentSymbols
                    ?.Where(x => x.Location != null) // Filter out symbols without locations
                    .Select(x => new DocumentSymbol
                    {
                        Name = x.Name,
                        ContainerName = x.ContainerName,
                        Kind = x.Kind.ToString(),
                        Location = x.Location!.ToSymbolLocation(),
                        Depth = 0 // We don't need depth calculation for position lookup
                    })
                    .ToList() ?? [];
                
                clickedSymbol = FindSymbolAtPosition(symbols, request.Position);
                _logger.LogInformation("[Hover] Found symbol at position: {SymbolName} ({SymbolKind})", 
                    clickedSymbol?.Name ?? "None", 
                    clickedSymbol?.Kind ?? "Unknown");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Hover] Failed to get document symbols, continuing with hover only");
            }
            
            // Then get hover information
            var hover = await LanguageServer.HoverAsync(new DocumentClientRequest
            {
                Document = absoluteFileUri,
                Position = request.Position.ToZeroBased(),
            },
                cancellationToken
            );

            _logger.LogInformation("[Hover] Content:{Content}", hover?.Contents?.Value);

            await CloseFileOnLspAsync(absoluteFileUri, cancellationToken);

            return new HoverSuccess
            {
                Value = hover?.Contents?.Value ?? string.Empty,
                Symbol = clickedSymbol,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Hover] Error occurred while processing hover request for {FilePath}:{Line}:{Character}",
                request.FilePath,
                request.Position.Line,
                request.Position.Character
            );

            return new ApplicationServiceError
            {
                Message = "Failed to get hover information",
                Exception = ex,
            };
        }
    }

    public async Task<OneOf<SearchSymbolSuccess, ApplicationServiceError>> SearchSymbolAsync(
        SearchSymbolRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        try
        {
            var matchingSymbols = await LanguageServer.WorkspaceSymbolAsync(
                new WorkspaceSymbolParams
                {
                    Query = request.Query,
                },
                cancellationToken
            );

            var documentSymbols = matchingSymbols
                .Select(s => new DocumentSymbol
                {
                    Name = s.Name ?? string.Empty,
                    Kind = s.Kind?.ToString() ?? "Unknown",
                    ContainerName = s.ContainerName,
                    Location = s.Location?.ToSymbolLocation(),
                }
                )
                .ToList();

            // Extract locations, enrich them, and create a lookup
            var locations = documentSymbols
                .Where(s => s.Location != null)
                .Select(s => s.Location!);
            var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);
            var enrichedLookup = enrichedLocations.ToLookup(loc =>
                $"{loc.FilePath}:{loc.StartLine}:{loc.StartCharacter}:{loc.EndLine}:{loc.EndCharacter}"
            );

            // Update document symbols with enriched locations
            var enrichedSymbols = documentSymbols.Select(symbol =>
                {
                    if (symbol.Location == null)
                        return symbol;

                    var key =
                        $"{symbol.Location.FilePath}:{symbol.Location.StartLine}:{symbol.Location.StartCharacter}:{symbol.Location.EndLine}:{symbol.Location.EndCharacter}";
                    var enrichedLocation = enrichedLookup[key]
                        .FirstOrDefault();

                    return symbol with
                    {
                        Location = enrichedLocation ?? symbol.Location,
                    };
                }
            );

            return new SearchSymbolSuccess
            {
                Value = enrichedSymbols,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[SearchSymbol] Error occurred while processing search symbol request for query: {Query}",
                request.Query
            );

            return new ApplicationServiceError
            {
                Message = "Failed to search symbols",
                Exception = ex,
            };
        }
    }

    public Task<OneOf<WindowLog, ApplicationServiceError>> GetWindowLogMessagesAsync(
        WindowLogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("[GetWindowLogMessages] Retrieving window log messages");

        _logger.LogTrace("[Process] Pid:{Pid} Exited:{HasExited}",
            _process?.Id,
            _process?.HasExited
        );

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
                    OneOf<WindowLog, ApplicationServiceError>.FromT0(new WindowLog(empty))
                );
            }

            var logMessages = windowNotificationHandler
                .LogMessages.Select(x =>
                    new WindowLogMessage(x.Message ?? string.Empty, x.MessageType)
                )
                .ToList();

            _logger.LogInformation("[GetWindowLogMessages] Retrieved {Count} log messages",
                logMessages.Count
            );

            return Task.FromResult(
                OneOf<WindowLog, ApplicationServiceError>.FromT0(new WindowLog(logMessages))
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GetWindowLogMessages] Error occurred while retrieving window log messages"
            );

            return Task.FromResult(OneOf<WindowLog, ApplicationServiceError>.FromT1(
                    new ApplicationServiceError
                    {
                        Message = "Failed to get window log messages",
                        Exception = ex,
                    }
                )
            );
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
                LanguageId = _languageIdMapper.MapFileToLanguageId(fileUri.LocalPath),
                Text = content,
            },
        },
            cancellationToken
        );

        _logger.LogDebug("File {FilePath} opened on LSP", fileUri.LocalPath);
    }

    private async Task CloseFileOnLspAsync(Uri fileUri, CancellationToken cancellationToken)
    {
        //TODO: Do we need to check for file existance again? No don't think so

        await LanguageServer.DidCloseAsync(new DidCloseTextDocumentParams
        {
            TextDocument = fileUri.ToDocumentIdentifier(),
        },
            cancellationToken
        );

        _logger.LogDebug("File {FilePath} closed on LSP", fileUri.LocalPath);
    }

    private async Task<T> ExecuteWithFileLifecycleAsync<T>(string filePath,
        Func<Uri, Task<T>> operation,
        CancellationToken cancellationToken)
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
        GetSymbolsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        try
        {
            // Get symbol filtering settings from the LSP profile
            var symbolsSettings = await GetSymbolsSettingsAsync();

            var result = await ExecuteWithFileLifecycleAsync(request.FilePath,
                async (fileUri) =>
                {
                    var documentSymbols = await LanguageServer.DocumentSymbolAsync(
                        new DocumentSymbolParams
                        {
                            TextDocument = fileUri.ToDocumentIdentifier(),
                        },
                        cancellationToken
                    );

                    var symbols = documentSymbols
                        ?.Select(x => new DocumentSymbol
                        {
                            Name = x.Name,
                            ContainerName = x.ContainerName,
                            Kind = x.Kind.ToString(),
                            Location = x.Location?.ToSymbolLocation(),
                        }
                        )
                        .ToList() ?? [];

                    // Extract locations, enrich them, and create a lookup
                    var locations = symbols
                        .Where(s => s.Location != null)
                        .Select(s => s.Location!);
                    var enrichedLocations = await locations.EnrichWithTextAsync(cancellationToken);
                    var enrichedLookup = enrichedLocations.ToLookup(loc =>
                        $"{loc.FilePath}:{loc.StartLine}:{loc.StartCharacter}:{loc.EndLine}:{loc.EndCharacter}"
                    );

                    // Update document symbols with enriched locations
                    var enrichedSymbols = symbols.Select(symbol =>
                        {
                            if (symbol.Location == null)
                                return symbol;

                            var key =
                                $"{symbol.Location.FilePath}:{symbol.Location.StartLine}:{symbol.Location.StartCharacter}:{symbol.Location.EndLine}:{symbol.Location.EndCharacter}";
                            var enrichedLocation = enrichedLookup[key]
                                .FirstOrDefault();

                            return symbol with
                            {
                                Location = enrichedLocation ?? symbol.Location,
                            };
                        }
                    );

                    // Calculate depth for each symbol
                    var symbolsWithDepth = CalculateSymbolDepths(enrichedSymbols);

                    // Apply symbol filtering based on configuration
                    var filteredSymbols = symbolsWithDepth.AsEnumerable();

                    // Determine effective max depth (request override takes precedence)
                    var effectiveMaxDepth = request.MaxDepth ?? symbolsSettings.MaxDepth;

                    // Filter by depth if specified
                    if (effectiveMaxDepth.HasValue)
                        filteredSymbols = filteredSymbols.Where(s => s.Depth <= effectiveMaxDepth.Value);

                    // Filter by symbol kinds
                    if (symbolsSettings.Kinds != null && symbolsSettings.Kinds.Length > 0)
                    {
                        filteredSymbols = filteredSymbols.Where(s =>
                            symbolsSettings.Kinds.Contains(s.Kind, StringComparer.OrdinalIgnoreCase)
                        );
                    }

                    return new GetSymbolsSuccess
                    {
                        Symbols = filteredSymbols,
                    };
                },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GetDocumentSymbols] Error occurred while processing get document symbols request for {FilePath}",
                request.FilePath
            );

            return new ApplicationServiceError
            {
                Message = "Failed to get document symbols",
                Exception = ex,
            };
        }
    }

    private static IEnumerable<DocumentSymbol> CalculateSymbolDepths(IEnumerable<DocumentSymbol> symbols)
    {
        var symbolList = symbols.ToList();
        var containerToDepthMap = new Dictionary<string, int>();

        // Build a map of symbol name to its container for lookup
        var symbolToContainerMap = symbolList
            .Where(s => !string.IsNullOrEmpty(s.ContainerName))
            .ToLookup(s => s.Name, s => s.ContainerName!);

        int CalculateDepth(string? containerName, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(containerName))
                return 0;

            if (visited.Contains(containerName))
                return 0; // Prevent infinite recursion in case of circular references

            if (containerToDepthMap.TryGetValue(containerName, out var cachedDepth))
                return cachedDepth + 1;

            visited.Add(containerName);

            // Find the container's own container
            var parentContainer = symbolToContainerMap[containerName].FirstOrDefault();
            var depth = CalculateDepth(parentContainer, visited) + 1;

            visited.Remove(containerName);
            containerToDepthMap[containerName] = depth - 1; // Cache the container's own depth

            return depth;
        }

        // Calculate depth for each symbol
        return symbolList.Select(symbol => symbol with
        {
            Depth = CalculateDepth(symbol.ContainerName, new HashSet<string>())
        });
    }

    public async Task<OneOf<IEnumerable<DocumentDiagnostic>, ApplicationServiceError>>
        GetDocumentDiagnosticsAsync(DocumentDiagnosticsRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(GetDocumentDiagnosticsAsync), request);

        try
        {
            // Determine diagnostic strategy based on the chosen LSP profile
            var diagnosticSettings = await GetDiagnosticStrategyAsync();

            _logger.LogDebug("Using diagnostic strategy: {Strategy} for file: {FilePath}",
                diagnosticSettings.Strategy,
                request.FilePath
            );

            var result = await ExecuteWithFileLifecycleAsync(request.FilePath,
                async fileUri =>
                {
                    List<Diagnostic> diagnostics;

                    switch (diagnosticSettings.Strategy)
                    {
                        case DiagnosticStrategy.Pull:
                            diagnostics = await GetPullDiagnosticsAsync(fileUri, cancellationToken);

                            break;

                        case DiagnosticStrategy.Push:
                            diagnostics = await GetPushDiagnosticsAsync(fileUri,
                                diagnosticSettings.WaitTimeoutMs,
                                cancellationToken
                            );

                            break;

                        default:
                            _logger.LogWarning(
                                "Unknown diagnostic strategy: {Strategy}, falling back to pull",
                                diagnosticSettings.Strategy
                            );
                            diagnostics = await GetPullDiagnosticsAsync(fileUri, cancellationToken);

                            break;
                    }

                    var documentDiagnostics = ConvertToDocumentDiagnostics(diagnostics);

                    // Enrich diagnostics with text content
                    var enrichedDiagnostics = await documentDiagnostics.EnrichWithTextAsync(
                        request.FilePath,
                        cancellationToken
                    );

                    // Sort by severity (Error → Warning → Information → Hint) then by line number
                    return enrichedDiagnostics
                        .OrderBy(d => d.SeverityOrder)
                        .ThenBy(d => d.StartLine)
                        .ThenBy(d => d.StartCharacter)
                        .ToArray();
                },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GetDocumentDiagnosticsAsync] Error occurred while processing get document diagnostics request for {FilePath}",
                request.FilePath
            );

            return new ApplicationServiceError
            {
                Message = "Failed to get document diagnostics",
                Exception = ex,
            };
        }
    }

    public async Task<OneOf<RenameSymbolSuccess, ApplicationServiceError>> RenameSymbolAsync(
        RenameSymbolRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWorkspaceReady())
            return WorkspaceLoadingError();

        _logger.LogInformation("[{Name}] {@Request}", nameof(RenameSymbolAsync), request);

        try
        {
            var result = await ExecuteWithFileLifecycleAsync(request.FilePath,
                async (fileUri) =>
                {
                    var workspaceEdit = await LanguageServer.RenameAsync(new RenameParams
                    {
                        TextDocument = fileUri.ToDocumentIdentifier(),
                        Position = request.Position.ToZeroBased(),
                        NewName = request.NewName,
                    },
                        cancellationToken
                    );

                    if (workspaceEdit == null)
                    {
                        _logger.LogWarning("LSP returned null workspace edit for rename operation");

                        return new RenameSymbolSuccess
                        {
                            Success = false,
                            Errors = ["LSP returned no changes for rename operation",],
                            ChangedFiles = [],
                            TotalEditsApplied = 0,
                            TotalFilesChanged = 0,
                            TotalLinesChanged = 0,
                        };
                    }

                    // TODO: Inject from DI
                    var applicator = new WorkspaceEditApplicator(_loggerFactory
                        .CreateLogger<WorkspaceEditApplicator>()
                    );
                    var applicatorResult = await applicator.ApplyAsync(workspaceEdit,
                        cancellationToken
                    );

                    if (applicatorResult.HasErrors)
                    {
                        _logger.LogError("Failed to apply rename changes: {Errors}",
                            string.Join(", ", applicatorResult.Errors)
                        );

                        return new RenameSymbolSuccess
                        {
                            Success = false,
                            Errors = applicatorResult.Errors,
                            ChangedFiles = [],
                            TotalEditsApplied = 0,
                            TotalFilesChanged = 0,
                            TotalLinesChanged = 0,
                        };
                    }

                    _logger.LogInformation(
                        "Successfully renamed symbol: {TotalFilesChanged} files, {TotalEditsApplied} edits, {TotalLinesChanged} lines",
                        applicatorResult.TotalFilesChanged,
                        applicatorResult.TotalEditsApplied,
                        applicatorResult.TotalLinesChanged
                    );

                    return new RenameSymbolSuccess
                    {
                        Success = true,
                        Errors = [],
                        ChangedFiles = applicatorResult.FilesChanged.Select(r => r.FilePath),
                        TotalFilesChanged = applicatorResult.TotalFilesChanged,
                        TotalEditsApplied = applicatorResult.TotalEditsApplied,
                        TotalLinesChanged = applicatorResult.TotalLinesChanged,
                    };
                },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RenameSymbol] Error occurred while processing rename symbol request for {FilePath}:{Line}:{Character} -> {NewName}",
                request.FilePath,
                request.Position.Line,
                request.Position.Character,
                request.NewName
            );

            return new ApplicationServiceError
            {
                Message = "Failed to rename symbol",
                Exception = ex,
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

            if (_process is { HasExited: false, })
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
                        "LSP server process did not exit within timeout, killing process"
                    );
                    if (!_process.HasExited)
                        _process.Kill(true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LSP server shutdown");
        }
    }

    /// <summary>
    /// Gets the DefaultNotificationHandler to access unhandled notifications.
    /// </summary>
    /// <returns>The DefaultNotificationHandler instance, or null if not found</returns>
    public DefaultNotificationHandler? GetDefaultNotificationHandler() =>
        _lspNotificationHandlers
            .OfType<DefaultNotificationHandler>()
            .FirstOrDefault();

    /// <summary>
    /// Gets the DefaultRequestHandler to access unhandled requests.
    /// </summary>
    /// <returns>The DefaultRequestHandler instance, or null if not found</returns>
    public DefaultRequestHandler? GetDefaultRequestHandler() =>
        _lspNotificationHandlers
            .OfType<DefaultRequestHandler>()
            .FirstOrDefault();

    public async ValueTask DisposeAsync()
    {
        try
        {
            _rpc?.Dispose();

            if (_process is { HasExited: false, })
            {
                _process.StandardInput.Close();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                try
                {
                    await _process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!_process.HasExited)
                        _process.Kill(true);
                }
            }
        }
        catch
        {
            // Ignore dispose errors.
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Determines the diagnostic strategy to use for the chosen LSP profile.
    /// </summary>
    private async Task<DiagnosticsSettings> GetDiagnosticStrategyAsync()
    {
        try
        {
            // Use the chosen LSP profile name from configuration instead of searching by extension
            var profileName = _config.ProfileName;

            if (string.IsNullOrWhiteSpace(profileName))
            {
                _logger.LogDebug("No LSP profile name configured, using pull strategy defaults");

                return DiagnosticsSettings.PullDefaults;
            }

            // Get the chosen LSP profile resolver
            var resolver = await _lspConfigurationService.CreateResolverAsync();
            var profile = resolver.GetProfile(profileName);

            if (profile?.Diagnostics != null)
            {
                _logger.LogDebug("Using diagnostic settings from chosen LSP {LspName}: {Strategy}",
                    profileName,
                    profile.Diagnostics.Strategy
                );

                return profile.Diagnostics;
            }

            _logger.LogDebug(
                "No diagnostic settings found for chosen LSP {LspName}, using pull strategy defaults",
                profileName
            );

            return DiagnosticsSettings.PullDefaults;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error determining diagnostic strategy for chosen LSP profile, using pull strategy"
            );

            return DiagnosticsSettings.PullDefaults;
        }
    }

    /// <summary>
    /// Determines the symbol filtering settings to use for the chosen LSP profile.
    /// </summary>
    private async Task<SymbolsSettings> GetSymbolsSettingsAsync()
    {
        try
        {
            // Use the chosen LSP profile name from configuration instead of searching by extension
            var profileName = _config.ProfileName;

            if (string.IsNullOrWhiteSpace(profileName))
            {
                _logger.LogDebug("No LSP profile name configured, using default symbol settings");

                return SymbolsSettings.Default;
            }

            // Get the chosen LSP profile resolver
            var resolver = await _lspConfigurationService.CreateResolverAsync();
            var profile = resolver.GetProfile(profileName);

            if (profile?.Symbols != null)
            {
                _logger.LogDebug(
                    "Using symbol settings from chosen LSP {LspName}: MaxDepth={MaxDepth}, Kinds={Kinds}",
                    profileName,
                    profile.Symbols.MaxDepth?.ToString() ?? "unlimited",
                    profile.Symbols.Kinds != null ? string.Join(",", profile.Symbols.Kinds) : "all"
                );

                return profile.Symbols;
            }

            _logger.LogDebug(
                "No symbol settings found for chosen LSP {LspName}, using default settings",
                profileName
            );

            return SymbolsSettings.Default;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error determining symbol settings for chosen LSP profile, using default settings"
            );

            return SymbolsSettings.Default;
        }
    }

    /// <summary>
    /// Extracts diagnostic provider identifiers from server capabilities.
    /// This handles LSP servers like Pyright that provide diagnostic providers 
    /// in server capabilities rather than through dynamic registration.
    /// </summary>
    private List<string> GetDiagnosticProvidersFromServerCapabilities()
    {
        var providers = new List<string>();

        if (_serverCapabilities is null)
        {
            _logger.LogDebug(
                "[GetDiagnosticProvidersFromServerCapabilities] No server capabilities available"
            );

            return providers;
        }

        try
        {
            // Look for diagnosticProvider in server capabilities
            var capabilities = _serverCapabilities["capabilities"];
            var diagnosticProvider = capabilities?["diagnosticProvider"];

            if (diagnosticProvider is not null)
            {
                // Extract the identifier if present
                var identifier = diagnosticProvider["identifier"]
                    ?.GetValue<string>();

                if (!string.IsNullOrEmpty(identifier))
                {
                    providers.Add(identifier);
                    _logger.LogDebug(
                        "[GetDiagnosticProvidersFromServerCapabilities] Found diagnostic provider: {Identifier}",
                        identifier
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[GetDiagnosticProvidersFromServerCapabilities] Failed to extract diagnostic providers from server capabilities"
            );
        }

        return providers;
    }

    /// <summary>
    /// Gets diagnostics using the pull strategy (request-response).
    /// </summary>
    private async Task<List<Diagnostic>> GetPullDiagnosticsAsync(Uri fileUri,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        var diagnosticProviderIds = new List<string>();

        // Step 1: Check if we have diagnostic providers from server capabilities (e.g., Pyright)
        var serverCapabilitiesProviders = GetDiagnosticProvidersFromServerCapabilities();
        diagnosticProviderIds.AddRange(serverCapabilitiesProviders);

        // Step 2: Check for dynamic registration providers (e.g., C#)
        var clientCapabilityRegistrationHandler = _lspNotificationHandlers
            .OfType<ClientCapabilityRegistrationHandler>()
            .FirstOrDefault();

        if (clientCapabilityRegistrationHandler != null)
        {
            // If registrations are available, add them to the list
            if (clientCapabilityRegistrationHandler.RegistrationCompleted.IsCompleted ||
                clientCapabilityRegistrationHandler.Registrations.Any())
            {
                var registrations = clientCapabilityRegistrationHandler.Registrations.ToArray();
                var textDocumentDiagnosticRegistrations = registrations
                    .Where(r => r.Method == "textDocument/diagnostic")
                    .ToArray();

                foreach (var registration in textDocumentDiagnosticRegistrations)
                {
                    var identifier = registration.RegisterOptions?.Identifier;
                    if (!string.IsNullOrEmpty(identifier) &&
                        !diagnosticProviderIds.Contains(identifier))
                    {
                        diagnosticProviderIds.Add(identifier);
                    }
                }
            }
        }

        // Step 3: If no diagnostic providers found from either source, return empty
        if (!diagnosticProviderIds.Any())
        {
            _logger.LogWarning(
                "[GetPullDiagnosticsAsync] No diagnostic providers available from server capabilities or dynamic registration"
            );

            return [];
        }

        // Step 4: Fetch diagnostics for each provider
        foreach (var identifier in diagnosticProviderIds)
        {
            _logger.LogDebug(
                "[GetPullDiagnosticsAsync] Fetching diagnostics for identifier: {Identifier}",
                identifier
            );

            try
            {
                var response = await LanguageServer.DiagnosticAsync(new TextDocumentDiagnosticParams
                {
                    TextDocument = fileUri.ToDocumentIdentifier(),
                    Identifier = identifier,
                },
                    cancellationToken
                );

                if (response?.Items != null)
                    diagnostics.AddRange(response.Items);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[GetPullDiagnosticsAsync] Failed to fetch diagnostics for identifier: {Identifier}",
                    identifier
                );
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Gets diagnostics using the push strategy (from notifications).
    /// </summary>
    private async Task<List<Diagnostic>> GetPushDiagnosticsAsync(Uri fileUri,
        int waitTimeoutMs,
        CancellationToken cancellationToken)
    {
        var diagnosticsHandler = _lspNotificationHandlers
            .OfType<DiagnosticsNotificationHandler>()
            .FirstOrDefault();

        if (diagnosticsHandler is null)
        {
            _logger.LogWarning("[GetPushDiagnosticsAsync] DiagnosticsNotificationHandler not found"
            );

            return [];
        }

        // Normalize the URI to ensure consistent comparison with LSP-provided URIs
        var normalizedUri = NormalizeFileUri(fileUri);

        // Always wait for diagnostics to arrive via push notifications to avoid returning stale data
        _logger.LogDebug("Waiting {TimeoutMs}ms for push diagnostics on file: {FileUri}",
            waitTimeoutMs,
            normalizedUri
        );

        await Task.Delay(waitTimeoutMs, cancellationToken);

        var newDiagnostics = FindDiagnosticsByUri(diagnosticsHandler.LatestDiagnostics,
            normalizedUri
        );

        _logger.LogDebug("New push diagnostics for file: {FileUri}", normalizedUri);

        return newDiagnostics?.Diagnostics?.ToList() ?? [];
    }

    /// <summary>
    /// Converts LSP Diagnostic objects to DocumentDiagnostic objects.
    /// </summary>
    private static List<DocumentDiagnostic> ConvertToDocumentDiagnostics(
        IEnumerable<Diagnostic> diagnostics)
    {
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
                _ => "Unknown",
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
                CodeDescription = diagnostic.CodeDescription?.Href,
            }
            );
        }

        return documentDiagnostics;
    }

    /// <summary>
    /// Normalizes a file URI to ensure consistent comparison with LSP-provided URIs.
    /// </summary>
    private static Uri NormalizeFileUri(Uri uri)
    {
        // Ensure absolute URI with proper file scheme
        if (!uri.IsAbsoluteUri)
        {
            var fullPath = Path.GetFullPath(uri.ToString());

            return new Uri(fullPath);
        }

        // Convert to absolute path and back to URI to normalize path separators and casing
        if (uri.IsFile)
        {
            var normalizedPath = Path.GetFullPath(uri.LocalPath);

            return new Uri(normalizedPath);
        }

        return uri;
    }

    /// <summary>
    /// Finds diagnostics by URI, handling potential URI format differences between client and LSP.
    /// </summary>
    private DiagnosticNotification? FindDiagnosticsByUri(
        System.Collections.Concurrent.ConcurrentDictionary<Uri, DiagnosticNotification> diagnostics,
        Uri targetUri)
    {
        // First try direct lookup
        if (diagnostics.TryGetValue(targetUri, out var directMatch))
            return directMatch;

        // If direct lookup fails, try to find by normalized path comparison
        var targetPath = targetUri.IsFile
            ? Path.GetFullPath(targetUri.LocalPath)
            : targetUri.ToString();

        foreach (var kvp in diagnostics)
        {
            var candidateUri = kvp.Key;
            var candidatePath = candidateUri.IsFile
                ? Path.GetFullPath(candidateUri.LocalPath)
                : candidateUri.ToString();

            // Compare normalized paths (case-insensitive on Windows, case-sensitive on Unix)
            if (string.Equals(targetPath,
                    candidatePath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal
                ))
            {
                _logger.LogDebug(
                    "[FindDiagnosticsByUri] Found diagnostics using path normalization. Target: {TargetUri}, Found: {FoundUri}",
                    targetUri,
                    candidateUri
                );

                return kvp.Value;
            }
        }

        _logger.LogDebug(
            "[FindDiagnosticsByUri] No diagnostics found for URI: {TargetUri}. Available URIs: {AvailableUris}",
            targetUri,
            string.Join(", ", diagnostics.Keys)
        );

        return null;
    }

    private static DocumentSymbol? FindSymbolAtPosition(IEnumerable<DocumentSymbol> symbols, EditorPosition position)
    {
        // Find all symbols that contain the given position
        var matchingSymbols = symbols
            .Where(symbol => symbol.Location != null && IsPositionInSymbol(symbol.Location, position))
            .ToList();

        if (!matchingSymbols.Any())
            return null;

        // Return the symbol with the smallest range (most specific/innermost)
        return matchingSymbols
            .OrderBy(symbol => CalculateSymbolRange(symbol.Location!))
            .First();
    }

    private static int CalculateSymbolRange(SymbolLocation location)
    {
        var startLine = location.StartLine ?? 0;
        var endLine = location.EndLine ?? 0;
        var startChar = location.StartCharacter ?? 0;
        var endChar = location.EndCharacter ?? 0;

        // Calculate range size: line difference * large number + character difference
        // This ensures symbols on fewer lines are preferred, and within same lines, smaller char ranges win
        return (int)((endLine - startLine) * 10000 + (endChar - startChar));
    }

    private static bool IsPositionInSymbol(SymbolLocation location, EditorPosition position)
    {
        var startLine = location.StartLine ?? 0;
        var endLine = location.EndLine ?? 0;
        var startChar = location.StartCharacter ?? 0;
        var endChar = location.EndCharacter ?? 0;

        // Check if position is within the symbol's range
        if (position.Line < startLine || position.Line > endLine)
            return false;

        if (position.Line == startLine && position.Character < startChar)
            return false;

        if (position.Line == endLine && position.Character > endChar)
            return false;

        return true;
    }
}
