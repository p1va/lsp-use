using System.Diagnostics;
using System.Threading.Tasks;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using StreamJsonRpc;

namespace LspUse.TestHarness;

/// <summary>
/// Aggregates objects that live for the duration of a single LSP test
/// run and tears them down when disposed.
/// </summary>
internal sealed class LspTestContext : IAsyncDisposable
{
    public JsonRpc Rpc { get; }

    /// <summary>
    /// Strongly-typed LSP client facade that wraps <see cref="Rpc"/>.
    /// Tests should prefer this over sending raw JSON-RPC messages whenever
    /// possible as it exercises the same codepath used by production code.
    /// </summary>
    public ILspClient Client { get; }

    public WindowNotificationHandler Window { get; }
    public DiagnosticsNotificationHandler Diagnostics { get; }
    public WorkspaceNotificationHandler Workspace { get; }
    public ClientCapabilityRegistrationHandler CapabilityRegistration { get; }

    private readonly Process _lspProcess;

    public LspTestContext(JsonRpc rpc, WindowNotificationHandler window,
        DiagnosticsNotificationHandler diagnostics, WorkspaceNotificationHandler workspace,
        ClientCapabilityRegistrationHandler capabilityRegistration, Process lspProcess, ILspClient client)
    {
        Rpc = rpc;
        Window = window;
        Diagnostics = diagnostics;
        Workspace = workspace;
        CapabilityRegistration = capabilityRegistration;
        _lspProcess = lspProcess;
        Client = client;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Client.ShutdownAsync();
            await Client.ExitAsync();
            await Task.Delay(1_000);
        }
        catch
        {
            // ignored
        }

        try
        {
            Rpc.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            if (!_lspProcess.HasExited)
            {
                _lspProcess.Kill(true);
                await _lspProcess.WaitForExitAsync();
            }
        }
        catch
        {
            // ignored
        }

        _lspProcess.Dispose();
    }
}
