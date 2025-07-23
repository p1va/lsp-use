using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Models;
using Xunit.Abstractions;

namespace LspUse.TestHarness;

public class PythonLspTests(ITestOutputHelper output)
{
    [Fact(DisplayName = "didOpen + wait for diagnostics")]
    public async Task PythonDiagnosticsPush()
    {
        // Use a file that's actually part of the project instead of TestSources
        var fileUri = new Uri("/home/truelayer/Repo/tool-api/main.py");

        await using var ctx = await PythonLspTestHelpers.StartAsync(output);

        // Open the file with an intentional error
        var text = await File.ReadAllTextAsync(fileUri.LocalPath);

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = fileUri,
                    LanguageId = "python",
                    Version = 1,
                    Text = text,
                },
            }
        );

        await Task.Delay(3_000);

        var response = await ctx.Client.DiagnosticAsync(new TextDocumentDiagnosticParams
            {
                TextDocument = fileUri.ToDocumentIdentifier(),
                Identifier = "pyright",
            }
        );

        output.WriteLine("--- Diagnostics ---");

        foreach (var c in response.Items)
            output.WriteLine($"--- [{c.Severity}] {c.Code}: {c.Message}");

        output.WriteLine("--- Window Messages ---");

        foreach (var message in ctx.Window.LogMessages)
            output.WriteLine($"[{message.MessageType}] {message.Message}");

        output.WriteLine("--- Registrations ---");

        foreach (var reg in ctx.CapabilityRegistration.Registrations)
            output.WriteLine($"[{reg.Id}] {reg.Method}");
    }

    [Fact(DisplayName = "hover")]
    public async Task PythonHover()
    {
        // Use a file that's actually part of the project instead of TestSources
        var fileUri = new Uri("/home/truelayer/Repo/tool-api/main.py");

        await using var ctx = await PythonLspTestHelpers.StartAsync(output);

        // Open the file with an intentional error
        var text = await File.ReadAllTextAsync(fileUri.LocalPath);

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = fileUri,
                    LanguageId = "python",
                    Version = 1,
                    Text = text,
                },
            }
        );

        await Task.Delay(3_000);

        var response = await ctx.Client.HoverAsync(new DocumentClientRequest
            {
                Document = fileUri,
                Position = (154u, 8u).ToZeroBasedPosition(),
            }
        );

        output.WriteLine("--- Hover ---");
        output.WriteLine($"[{response.Contents.Kind}] {response.Contents.Value}");

        output.WriteLine("--- Window Messages ---");

        foreach (var message in ctx.Window.LogMessages)
            output.WriteLine($"[{message.MessageType}] {message.Message}");
    }
}
