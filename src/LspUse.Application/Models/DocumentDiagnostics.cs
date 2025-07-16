using System.Text.Json.Serialization;

namespace LspUse.Application.Models;

public record DocumentDiagnosticsRequest
{
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }
}

public record DocumentDiagnostic
{
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("start_line")]
    public required uint StartLine { get; init; }

    [JsonPropertyName("start_character")]
    public required uint StartCharacter { get; init; }

    [JsonPropertyName("end_line")]
    public required uint EndLine { get; init; }

    [JsonPropertyName("end_character")]
    public required uint EndCharacter { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("code_description")]
    public string? CodeDescription { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    // Helper property for sorting - not serialized
    [JsonIgnore]
    public int SeverityOrder => Severity switch
    {
        "Error" => 1,
        "Warning" => 2,
        "Information" => 3,
        "Hint" => 4,
        _ => 5
    };
}
