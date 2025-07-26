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
        _process.Start();

        var jsonFormatter = new SystemTextJsonFormatter();
        jsonFormatter.JsonSerializerOptions.Converters.Add(new AbsoluteUriJsonConverter());

        var messageHandler = new HeaderDelimitedMessageHandler(_process.StandardInput,
            _process.StandardOutput,
            jsonFormatter
        );

        var rpc = new JsonRpc(messageHandler);

        foreach (var target in _localTargets)
            rpc.AddLocalRpcTarget(target);

        // Enable trace logging for LSP communication
        rpc.TraceSource.Switch.Level = SourceLevels.All;
        rpc.TraceSource.Listeners.Add(new LoggerTraceListener(_logger));
        rpc.Disconnected += (sender, args) => _logger.LogError(args.Exception,
            "DISCONNECTED: {Description} {Reason}",
            args.Description,
            args.Reason
        );

        rpc.StartListening();

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
