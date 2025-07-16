namespace LspUse.LanguageServerClient.Models;

using System.Text.Json.Serialization;

public record ReferenceContext
{
    [JsonPropertyName("includeDeclaration")]
    public required bool IncludeDeclaration { get; init; }
}

public record ReferenceParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }

    [JsonPropertyName("position")]
    public required ZeroBasedPosition Position { get; init; }

    [JsonPropertyName("context")]
    public required ReferenceContext Context { get; init; }
}
