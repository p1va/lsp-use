namespace LspUse.LanguageServerClient.Models;

using System.Text.Json.Serialization;

public record TextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public required Uri Uri { get; init; }
}

public record TextDocumentItem
{
    [JsonPropertyName("uri")]
    public required Uri Uri { get; init; }

    [JsonPropertyName("languageId")]
    public required string LanguageId { get; init; }

    [JsonPropertyName("version")]
    public required int Version { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

public record DidOpenTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentItem TextDocument { get; init; }
}

public record DidCloseTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
}
