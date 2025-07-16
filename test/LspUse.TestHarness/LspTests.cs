using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Models;
using Xunit.Abstractions;

namespace LspUse.TestHarness;

public sealed class LspTests
{
    private readonly ITestOutputHelper _output;

    public LspTests(ITestOutputHelper output) => _output = output;

    [Fact(DisplayName = "textDocument/references")]
    public async Task ReferencesRequest()
    {
        var editorPosition = (Line: 39U, Characther: 15U);

        var fileUri = TestResource.DiagnosticsErrorTest;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        var response = await ctx.Client.ReferencesAsync(new DocumentClientRequest
        {
            Document = fileUri,
            Position = editorPosition.ToZeroBasedPosition()
        });

        foreach (var reference in response) _output.WriteLine(reference.ToString());
    }

    [Fact(DisplayName = "textDocument/completion")]
    public async Task CompletionRequest()
    {
        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        var editorPosition = (Line: 14u, Characther: 14u);

        var fileUri = TestResource.ApplicationService;

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                LanguageId = "csharp",
                Uri = fileUri,
                Text = await File.ReadAllTextAsync(fileUri.LocalPath),
                Version = 1
            }
        });

        var results = await ctx.Client.CompletionAsync(new DocumentClientRequest
        {
            Document = fileUri,
            Position = editorPosition.ToZeroBasedPosition()
        });

        foreach (var completion in results?.Items ?? []) _output.WriteLine(completion.ToString());
    }

    [Fact(DisplayName = "textDocument/definition")]
    public async Task DefinitionRequest()
    {
        var editorPosition = (Line: 8u, Characther: 23u);

        var fileUri = TestResource.JsonRpcTest;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        await ctx.Client.DefinitionAsync(new DocumentClientRequest
        {
            Document = fileUri,
            Position = editorPosition.ToZeroBasedPosition()
        });
    }

    [Fact(DisplayName = "didOpen + wait for diagnostics")]
    public async Task DiagnosticsPush()
    {
        // Use a file that's actually part of the project instead of TestSources
        var fileUri = TestResource.FileLoggerProvider;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        // Open the file with an intentional error
        var text = await File.ReadAllTextAsync(fileUri.LocalPath);

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = fileUri,
                LanguageId = "csharp",
                Version = 1,
                Text = text
            }
        });

        await Task.Delay(30_000);

        _output.WriteLine($"Received {ctx.Diagnostics.LatestDiagnostics.Count} diagnostic(s):");
        foreach (var d in ctx.Diagnostics.LatestDiagnostics)
            _output.WriteLine($"{d.Key}: {d.Value.Uri}");
    }

    [Fact(DisplayName = "textDocument/diagnostic")]
    public async Task PullDiagnosticsRequest()
    {
        // Use a file that's part of the actual project with real errors
        var fileUri = TestResource.DiagnosticsErrorTest;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        // open with errors
        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = fileUri,
                LanguageId = "csharp",
                Version = 1,
                Text = await File.ReadAllTextAsync(fileUri.LocalPath)
            }
        });

        // Give server time to analyze
        await Task.Delay(10_000);

        // pull diagnostics
        var response = await ctx.Client.DiagnosticAsync(new TextDocumentDiagnosticParams
        {
            TextDocument = fileUri.ToDocumentIdentifier(),
            Identifier = "pull-diagnostics-test"
        });

        _output.WriteLine($"Pull diagnostics response: {response?.ToString() ?? "null"}");

        // If pull diagnostics worked, we should get a response with items
        if (response?.Items != null && response.Items.Any())
        {
            _output.WriteLine($"Found {response.Items.Count()} diagnostic(s) via pull:");
            foreach (var diag in response.Items)
                _output.WriteLine($"  {diag.Severity}: {diag.Message} at {diag.Range}");
        }
    }

    [Fact(DisplayName = "textDocument/hover")]
    public async Task HoverRequest()
    {
        var editorPosition = (Line: 15u, Characther: 28u);

        var fileUri = TestResource.FindReferencesResult;

        //var fileUri = TestResource.DiagnosticsErrorTest;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                LanguageId = "csharp",
                Uri = fileUri,
                Text = await File.ReadAllTextAsync(fileUri.LocalPath),
                Version = 1
            }
        });

        var hover = await ctx.Client.HoverAsync(new DocumentClientRequest
        {
            Document = fileUri,
            Position = editorPosition.ToZeroBasedPosition()
        });

        var contents = hover?.Contents?.Value;
        _output.WriteLine($"Hover docs: {contents}");
    }

    [Fact(DisplayName = "definition -> didOpen -> documentSymbol on MAS file")]
    public async Task MasDocumentSymbolFlow()
    {
        var editorPosition = (Line: 8u, Characther: 28u);

        var fileUri = TestResource.JsonRpcTest;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        // 1. Ask Roslyn for the definition at the requested position ----
        var defLocations = await ctx.Client.DefinitionAsync(new DocumentClientRequest
        {
            Document = fileUri,
            Position = editorPosition.ToZeroBasedPosition()
        });

        Assert.NotEmpty(defLocations);
        var masUri = defLocations
                         .Select(l => l.Uri!)
                         .FirstOrDefault(u => u.LocalPath.Contains("/tmp/MetadataAsSource")) ??
                     defLocations[0].Uri!;

        // 2. Open the synthesized MAS document so documentSymbol will work
        var masLocalPath = masUri.LocalPath;

        Assert.True(File.Exists(masLocalPath), $"MAS file not found at {masLocalPath}");

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = masUri,
                LanguageId = "csharp",
                Version = 1,
                Text = await File.ReadAllTextAsync(masLocalPath)
            }
        });

        // 3. Request the symbol tree for that file -----------------------
        var symbols = await ctx.Client.DocumentSymbolAsync(new DocumentSymbolParams
        {
            TextDocument = masUri.ToDocumentIdentifier()
        });

        Assert.NotNull(symbols);
        Assert.NotEmpty(symbols);
        foreach (var s in symbols)
            _output.WriteLine($"{s.Name} – {s.Kind} (container: {s.ContainerName})");
    }

    [Fact(DisplayName = "workspace/symbol")]
    public async Task WorkspaceSymbol()
    {
        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        var response = await ctx.Client.WorkspaceSymbolAsync(new WorkspaceSymbolParams
        {
            Query = "_languageServer"
        });

        foreach (var symbol in response) _output.WriteLine(symbol?.ToString());
    }

    [Fact(DisplayName = "textDocument/symbol")]
    public async Task DocumentSymbol()
    {
        var fileUri = TestResource.ApplicationService;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = fileUri,
                LanguageId = "csharp",
                Version = 1,
                Text = await File.ReadAllTextAsync(fileUri.LocalPath)
            }
        });

        var response = await ctx.Client.DocumentSymbolAsync(new DocumentSymbolParams
        {
            TextDocument = fileUri.ToDocumentIdentifier()
        });
    }

    [Fact(DisplayName = "textDocument/typeDefinition")]
    public async Task TypeDefinition()
    {
        var editorPosition = (Line: 34u, Characther: 15u);

        var fileUri = TestResource.FindReferencesTool;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        var response = await ctx.Client.TypeDefinitionAsync(new DocumentClientRequest
        {
            Document = fileUri,
            Position = editorPosition.ToZeroBasedPosition()
        });
    }

    [Fact(DisplayName = "textDocument/implementation")]
    public async Task Implementation()
    {
        //var editorPosition = (Line: 11u, Characther: 33u);
        var editorPosition = (Line: 353u, Characther: 26u);

        var fileUri = TestResource.ApplicationService;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        var response = await ctx.Client.ImplementationAsync(new DocumentClientRequest
        {
            Document = fileUri,
            Position = editorPosition.ToZeroBasedPosition()
        });
    }

    [Fact(DisplayName = "workspace/diagnostic")]
    public async Task WorkspaceDiagnostics()
    {
        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        await ctx.CapabilityRegistration.RegistrationCompleted;

        foreach (var reg in ctx.CapabilityRegistration.Registrations)
            _output.WriteLine("Received diagnostics provider: " + reg);

        var ids = ctx.CapabilityRegistration.Registrations
            .Where(r => r.RegisterOptions?.WorkspaceDiagnostics is true)
            .Select(r => r.RegisterOptions?.Identifier);

        foreach (var id in ids)
        {
            _output.WriteLine("Fetching diagnostics for " + id);

            var response = await ctx.Rpc.InvokeWithParameterObjectAsync<object>(
                "workspace/diagnostic", new
                {
                    identifier = id,
                    previousResultIds = Array.Empty<object>()
                });
        }
    }

    [Fact(DisplayName = "textDocument/diagnostic")]
    public async Task TextDocumentDiagnostics()
    {
        // Use a file that's actually part of the project instead of TestSources
        //var fileUri = TestResource.FileLoggerProvider;
        var fileUri = TestResource.DiagnosticsErrorTest;

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        await ctx.CapabilityRegistration.RegistrationCompleted;

        var ids =
            ctx.CapabilityRegistration.Registrations.Select(r => r.RegisterOptions?.Identifier);

        // open with errors
        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = fileUri,
                LanguageId = "csharp",
                Version = 1,
                Text = await File.ReadAllTextAsync(fileUri.LocalPath)
            }
        });

        // Give server time to analyze
        // await Task.Delay(10_000);

        // pull diagnostics

        var diagnostics = new List<Diagnostic>();

        foreach (var id in ids)
        {
            _output.WriteLine("Fetching diagnostics for " + id);

            var response = await ctx.Client.DiagnosticAsync(new TextDocumentDiagnosticParams
            {
                TextDocument = fileUri.ToDocumentIdentifier(),
                Identifier = id
            });

            diagnostics.AddRange(response?.Items ?? []);
        }

        foreach (var diagnostic in diagnostics)
            _output.WriteLine(diagnostic.ToString());
    }

    [Fact(DisplayName = "textDocument/rename")]
    public async Task TextDocumentRename()
    {
        var fileUri = TestResource.ApplicationService;

        var editorPosition = (Line: 16u, Characther: 38u);

        await using var ctx = await LspTestHelpers.StartAndOpenSolutionAsync(_output);

        await ctx.Client.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = fileUri,
                LanguageId = "csharp",
                Version = 1,
                Text = await File.ReadAllTextAsync(fileUri.LocalPath)
            }
        });

        var prepareEdit = await ctx.Client.PrepareRenameAsync(new PrepareRenameParams
        {
            TextDocument = fileUri.ToDocumentIdentifier(),
            Position = editorPosition.ToZeroBasedPosition()
        });

        var workspaceEdit = await ctx.Client.RenameAsync(new RenameParams
        {
            TextDocument = fileUri.ToDocumentIdentifier(),
            Position = editorPosition.ToZeroBasedPosition(),
            NewName = "ITestService"
        });

        _output.WriteLine(
            $"Workspace edit contains {workspaceEdit.DocumentChanges.Count()} changes to the following docs");

        foreach (var change in workspaceEdit.DocumentChanges) _output.WriteLine(change.ToString());
    }
}
