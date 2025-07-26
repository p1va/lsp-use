using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LspUse.LanguageServerClient;

public class LanguageServerProcess : ILanguageServerProcess
{
    private readonly IOptions<LanguageServerProcessConfiguration> _configuration;
    private readonly ILogger<LanguageServerProcess> _logger;
    private Process? _process;

    public Stream StandardInput =>
        _process?.StandardInput.BaseStream ?? throw new InvalidOperationException(
            "Process needs to be started successfully before accessing standard input."
        );

    public Stream StandardOutput =>
        _process?.StandardOutput.BaseStream ?? throw new InvalidOperationException(
            "Process needs to be started successfully before accessing standard output."
        );

    public LanguageServerProcess(IOptions<LanguageServerProcessConfiguration> options,
        ILogger<LanguageServerProcess> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = options;
        _logger = logger;
    }

    public void Start()
    {
        var config = _configuration.Value;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = config.Command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = config.WorkingDirectory,
        };

        foreach (var arg in config.Arguments ?? [])
            processStartInfo.ArgumentList.Add(arg);

        // Add environment variables from configuration
        if (config.Environment != null)
        {
            foreach (var envVar in config.Environment)
                processStartInfo.Environment.Add(envVar.Key, envVar.Value);
        }

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

        var hasProcStarted = _process.Start();

        if (hasProcStarted is false)
        {
            _logger.LogError("Failed to start process");

            throw new InvalidOperationException($"Failed to start process {config.Command}");
        }

        _process.BeginErrorReadLine();

        _logger.LogInformation("Process started with PID: {ProcessId}", _process.Id);
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
            return;

        if (_process.HasExited is false)
        {
            _process.StandardInput.Close();

            // Wait for a graceful exit with a timeout.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            try
            {
                await _process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!_process.HasExited)
                {
                    _logger.LogWarning(
                        "Process did not exit gracefully. Killing process {ProcessId}.",
                        _process.Id
                    );
                    _process.Kill(true);
                }
            }
        }

        _process.Dispose();
    }
}
