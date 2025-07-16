namespace LspUse.LanguageServerClient.Models;

using System.Text.Json.Serialization;

public record WorkspaceSymbolParams
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }
}

public record WorkspaceSymbolResult
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("kind")]
    public SymbolKind? Kind { get; init; }

    [JsonPropertyName("containerName")]
    public string? ContainerName { get; init; }

    [JsonPropertyName("location")]
    public Location? Location { get; init; }
}
