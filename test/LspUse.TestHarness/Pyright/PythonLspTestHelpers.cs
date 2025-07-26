using System.Diagnostics;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.LanguageServerClient.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StreamJsonRpc;
using Xunit.Abstractions;

namespace LspUse.TestHarness.Pyright;

internal static class PythonLspTestHelpers
{
    internal static async Task<LspTestContext> StartAsync(ITestOutputHelper outputHelper)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pyright-langserver",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        psi.ArgumentList.Add("--stdio");

        var proc = Process.Start(psi) ??
                   throw new InvalidOperationException("Failed to spawn LSP server");
        proc.ErrorDataReceived += (_, e) => outputHelper.WriteLine("[stderr] " + e.Data);
        proc.BeginErrorReadLine();
        
        var windowHandler = new WindowNotificationHandler();
        var diagnosticsHandler =
            new DiagnosticsNotificationHandler(NullLogger<DiagnosticsNotificationHandler>.Instance);
        var workspaceHandler = new WorkspaceNotificationHandler();
        var capabilityRegistrationHandler = new ClientCapabilityRegistrationHandler();
        var defaultNotificationHandler = new DefaultNotificationHandler();
        var defaultRequestHandler = new DefaultRequestHandler();

        var lsp = new JsonRpcLspClient(proc.StandardInput.BaseStream,
            proc.StandardOutput.BaseStream,
            NullLogger<JsonRpcLspClient>.Instance,
            [
                windowHandler,
                diagnosticsHandler,
                workspaceHandler,
                capabilityRegistrationHandler,
                defaultRequestHandler,
                defaultNotificationHandler,
            ]
        );
        const string repo = "/path/to/repo";

        var serverCapabilities = await lsp.InitializeAsync(new
            {
                processId = Environment.ProcessId,
                rootUri = new Uri(repo),
                workspaceFolders = new[]
                {
                    new
                    {
                        uri = new Uri(repo),
                        name = repo,
                    },
                },
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

        return new LspTestContext(windowHandler,
            diagnosticsHandler,
            workspaceHandler,
            capabilityRegistrationHandler,
            proc,
            lsp
        );
    }
}
