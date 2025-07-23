using LspUse.LanguageServerClient.Models;
using Xunit.Abstractions;

namespace LspUse.TestHarness;

public class TypescriptLspTests
{
    private readonly ITestOutputHelper _output;

    public TypescriptLspTests(ITestOutputHelper output) => _output = output;

    [Fact(DisplayName = "didOpen + wait for diagnostics")]
    public async Task TypescriptDiagnosticsPush()
    {
        // Use a file that's actually part of the project instead of TestSources
        var fileUri = new Uri("/path/to/file.ts");

        await using var ctx = await TypescriptLspTestHelpers.StartAsync(_output);

        // Open the file with an intentional error
        var text = await File.ReadAllTextAsync(fileUri.LocalPath);

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = fileUri,
                LanguageId = "typescript",
                Version = 1,
                Text = text
            }
        });

        await Task.Delay(3_000);

        _output.WriteLine($"Received {ctx.Diagnostics.LatestDiagnostics.Count} diagnostic(s):");

        foreach (var d in ctx.Diagnostics.LatestDiagnostics)
        {
            _output.WriteLine($"--- {d.Value.Diagnostics?.Count() ?? 0} Diagnostics in file {d.Value.Uri}");

            foreach (var c in d.Value?.Diagnostics ?? [])
            {
                _output.WriteLine($"--- [{c.Severity}] {c.Code}: {c.Message}");
            }
        }


    }
}
