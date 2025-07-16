using System.Text.Json.Serialization;

namespace LspUse.Application.Models;

public record SymbolLocation
{
    [JsonPropertyName("file_path")]
    [JsonConverter(typeof(FileUriConverter))]
    public required Uri? FilePath { get; init; }
    [JsonPropertyName("start_line")]
    public required uint? StartLine { get; init; }
    [JsonPropertyName("start_character")]
    public required uint? StartCharacter { get; init; }
    [JsonPropertyName("end_line")]
    public required uint? EndLine { get; init; }
    [JsonPropertyName("end_character")]
    public required uint? EndCharacter { get; init; }
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}
