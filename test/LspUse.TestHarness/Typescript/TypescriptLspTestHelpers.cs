using System.Diagnostics;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.LanguageServerClient.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StreamJsonRpc;
using Xunit.Abstractions;

namespace LspUse.TestHarness;

internal static class TypescriptLspTestHelpers
{
    /// <summary>
    /// Starts the Roslyn LSP server, performs initialize/initialized, opens the
    /// solution and waits for workspace load to finish.
    /// The returned <see cref="LspTestContext"/> must be disposed by the caller.
    /// </summary>
    internal static async Task<LspTestContext> StartAsync(
        ITestOutputHelper outputHelper)
    {
        // Launch server process -----------------------------
        var psi = new ProcessStartInfo
        {
            FileName = "typescript-language-server",
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

        var lsp = new JsonRpcLspClient(proc.StandardInput.BaseStream,
            proc.StandardOutput.BaseStream,
            NullLogger<JsonRpcLspClient>.Instance,
            [
                windowHandler,
                diagnosticsHandler,
                workspaceHandler,
                capabilityRegistrationHandler,
                defaultNotificationHandler,
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
                rootUri = new Uri("/path/to/repo"),
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
