using System.Text.Json.Serialization;

namespace LspUse.LanguageServerClient.Models;

public record RenameParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }

    [JsonPropertyName("position")]
    public required ZeroBasedPosition Position { get; init; }

    [JsonPropertyName("newName")]
    public required string NewName { get; init; }
}

public record WorkspaceEdit
{
    [JsonPropertyName("documentChanges")]
    public IEnumerable<DocumentChange> DocumentChanges { get; init; } = [];

    [JsonPropertyName("changes")]
    public Dictionary<string, IEnumerable<TextEdit>>? Changes { get; init; }
}

public record DocumentChange
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier? TextDocument { get; init; }

    [JsonPropertyName("edits")]
    public IEnumerable<TextEdit>? Edits { get; init; }
}

public record TextEdit
{
    [JsonPropertyName("range")]
    public Range? Range { get; init; }

    [JsonPropertyName("newText")]
    public string? NewText { get; init; }
}

public record PrepareRenameParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }

    [JsonPropertyName("position")]
    public required ZeroBasedPosition Position { get; init; }
}

public record PrepareRenameResult
{
    [JsonPropertyName("range")]
    public Range? Range { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }
}
