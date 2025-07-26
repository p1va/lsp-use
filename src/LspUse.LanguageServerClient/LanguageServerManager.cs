using System.Diagnostics;
using LspUse.LanguageServerClient.Json;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace LspUse.LanguageServerClient;

public class LanguageServerManager : ILanguageServerManager
{
    private readonly ILanguageServerProcess _process;
    private readonly ILogger<LanguageServerManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IEnumerable<IRpcLocalTarget> _localTargets;
    private ILspClient? _client;

    public ILspClient Client =>
        _client ??
        throw new InvalidOperationException(
            "Start needs to be invoked before the client can be accessed"
        );

    public LanguageServerManager(ILanguageServerProcess process,
        ILogger<LanguageServerManager> logger,
        ILoggerFactory loggerFactory,
        IEnumerable<IRpcLocalTarget> localTargets)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(localTargets);

        _process = process;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _localTargets = localTargets;
    }

    public void Start()
    {
        // Spawn the language server process
        _process.Start();

        // Create a client to communicatw with it
        _client = new JsonRpcLspClient(_process.StandardInput,
            _process.StandardOutput,
            _loggerFactory.CreateLogger<JsonRpcLspClient>(),
            _localTargets
        );
    }

    // TODO: The manager should have a Stop command?
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _process.DisposeAsync();

            if (_client is not null)
                await _client.DisposeAsync();
        }
        catch (Exception)
        {
        }
    }
}
