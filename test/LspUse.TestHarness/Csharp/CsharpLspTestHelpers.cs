using System.Diagnostics;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace LspUse.TestHarness.Csharp;

internal static class CsharpLspTestHelpers
{
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
        var psi = new ProcessStartInfo
        {
            FileName = "/usr/bin/dotnet",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

        var windowHandler = new WindowNotificationHandler();
        var diagnosticsHandler =
            new DiagnosticsNotificationHandler(new NullLogger<DiagnosticsNotificationHandler>());
        var workspaceHandler = new WorkspaceNotificationHandler();
        var capabilityRegistrationHandler = new ClientCapabilityRegistrationHandler();

        var lsp = new JsonRpcLspClient(proc.StandardInput.BaseStream,
            proc.StandardOutput.BaseStream,
            NullLogger<JsonRpcLspClient>.Instance,
            [
                windowHandler,
                diagnosticsHandler,
                workspaceHandler,
                capabilityRegistrationHandler,
            ]
        );

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
                            relatedDocumentSupport = true,
                        },
                    },
                },
            }
        );

        // empty params per LSP
        await lsp.InitializedAsync(new
            {
            }
        );

        // Open solution ------------------------------------
        await lsp.NotifyAsync("solution/open",
            new
            {
                solution = new Uri(SolutionPath),
            }
        );

        // Wait until Roslyn reports projects loaded --------
        await workspaceHandler.WorkspaceInitialization;

        return new LspTestContext(windowHandler,
            diagnosticsHandler,
            workspaceHandler,
            capabilityRegistrationHandler,
            proc,
            lsp
        );
    }
}
