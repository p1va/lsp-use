using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.LanguageServerClient.Json;
using LspUse.LanguageServerClient.Models;
using StreamJsonRpc;
using Xunit.Abstractions;

namespace LspUse.TestHarness;

internal static class LspTestHelpers
{
    private class ActionTraceListener(Action<string> onWriteLine) : TraceListener
    {
        public override void Write(string? _)
        {
        }

        public override void WriteLine(string? message) => onWriteLine(message ?? string.Empty);
    }

    private static readonly string RepositoryPath = TestResource.RepositoryRoot;
    private static readonly string SolutionPath = TestResource.SolutionFile;

    private const string LanguageServerDllPath =
        "/tmp/lsp-use/roslyn/microsoft.codeanalysis.languageserver.linux-x64/5.0.0-1.25353.13/content/LanguageServer/linux-x64/Microsoft.CodeAnalysis.LanguageServer.dll";

    /// <summary>
    /// Starts the Roslyn LSP server, performs initialize/initialized, opens the
    /// solution and waits for workspace load to finish.
    /// The returned <see cref="LspTestContext"/> must be disposed by the caller.
    /// </summary>
    internal static async Task<LspTestContext> StartAndOpenSolutionAsync(
        ITestOutputHelper outputHelper)
    {
        // Launch server process -----------------------------
        var psi = new ProcessStartInfo
        {
            FileName = "/usr/bin/dotnet",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.EnvironmentVariables.Add("DOTNET_USE_POLLING_FILE_WATCHER", "true");

        psi.ArgumentList.Add(LanguageServerDllPath);
        psi.ArgumentList.Add("--logLevel=Information");
        psi.ArgumentList.Add("--extensionLogDirectory=logs");
        psi.ArgumentList.Add("--stdio");

        var proc = Process.Start(psi) ??
                   throw new InvalidOperationException("Failed to spawn LSP server");
        proc.ErrorDataReceived += (_, e) => outputHelper.WriteLine("[stderr] " + e.Data);
        proc.BeginErrorReadLine();

        // JsonRpc wiring ------------------------------------
        // Use custom JSON options so that Uri values are serialised using their
        // absolute form ("file:///..."), which Roslyn LSP expects.
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.Converters.Add(new AbsoluteUriJsonConverter());

        var messageHandler = new HeaderDelimitedMessageHandler(proc.StandardInput.BaseStream,
            proc.StandardOutput.BaseStream, formatter);

        var windowHandler = new WindowNotificationHandler();
        var diagnosticsHandler = new DiagnosticsNotificationHandler();
        var workspaceHandler = new WorkspaceNotificationHandler();
        var capabilityRegistrationHandler = new ClientCapabilityRegistrationHandler();

        var rpc = new JsonRpc(messageHandler);
        rpc.AddLocalRpcTarget(windowHandler);
        rpc.AddLocalRpcTarget(diagnosticsHandler);
        rpc.AddLocalRpcTarget(workspaceHandler);
        rpc.AddLocalRpcTarget(capabilityRegistrationHandler);

        rpc.TraceSource.Switch.Level = SourceLevels.All;
        rpc.TraceSource.Listeners.Add(
            new ActionTraceListener(m => outputHelper.WriteLine("[trace] " + m)));

        rpc.StartListening();

        var lsp = new JsonRpcLspClient(rpc);

        // LSP initialize handshake -------------------------
        // We build an anonymous capabilities object so we can include the
        // pull-diagnostic capability (textDocument/diagnostic &
        // workspace/diagnostic) which does not exist in the 17.2 protocol
        // DTO package. Roslyn will read the JSON properties it recognises and
        // ignore the rest.

        var serverCapabilities = await lsp.InitializeAsync(new
        {
            processId = Environment.ProcessId,
            rootUri = new Uri(RepositoryPath),
            capabilities = new
            {
                workspace = new
                {
                },
                textDocument = new
                {
                    publishDiagnostics = new
                    {
                        relatedInformation = true,
                        versionSupport = true,
                        codeDescriptionSupport = true,
                        dataSupport = true,
                    },
                    diagnostic = new
                    {
                        dynamicRegistration = true,
                        relatedDocumentSupport = true
                    }
                }
            }
        });

        // empty params per LSP
        await lsp.InitializedAsync(new
        {
        });

        // Open solution ------------------------------------
        await lsp.NotifyAsync("solution/open", new
        {
            solution = new Uri(SolutionPath)
        });

        // Wait until Roslyn reports projects loaded --------
        await workspaceHandler.WorkspaceInitialization;

        return new LspTestContext(rpc, windowHandler, diagnosticsHandler, workspaceHandler,
            capabilityRegistrationHandler, proc, lsp);
    }
}
