using LspUse.LanguageServerClient.Models;

namespace LspUse.Application.Models;

public record CompletionRequest
{
    public required string FilePath { get; init; }
    public required EditorPosition Position { get; init; }
}

public record CompletionSuccess
{
    // TODO: Refactor this as is coming from the client layer
    public required IEnumerable<CompletionItem> Items { get; init; }
    public bool IsIncomplete { get; init; }
}
