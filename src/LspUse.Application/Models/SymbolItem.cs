using System.Text.Json.Serialization;

namespace LspUse.Application.Models;

public record DocumentSymbol
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("container")]
    public string? ContainerName { get; init; }

    [JsonPropertyName("location")]
    public SymbolLocation? Location { get; set; }
}
