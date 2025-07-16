namespace LspUse.LanguageServerClient.Models;

using System.Text.Json.Serialization;

public record ZeroBasedPosition
{
    [JsonPropertyName("line")]
    public uint Line { get; init; }

    [JsonPropertyName("character")]
    public uint Character { get; init; }
}

public record Range
{
    [JsonPropertyName("start")]
    public ZeroBasedPosition? Start { get; init; }

    [JsonPropertyName("end")]
    public ZeroBasedPosition? End { get; init; }
}

public record Location
{
    [JsonPropertyName("uri")]
    public Uri? Uri { get; init; }

    [JsonPropertyName("range")]
    public Range? Range { get; init; }
}
