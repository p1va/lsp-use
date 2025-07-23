namespace LspUse.LanguageServerClient.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

public record DiagnosticNotification
{
    [JsonPropertyName("uri")]
    public Uri? Uri { get; init; }

    [JsonPropertyName("version")]
    public int? Version { get; init; }

    [JsonPropertyName("diagnostics")]
    public IEnumerable<Diagnostic>? Diagnostics { get; init; }
}

public record TextDocumentDiagnosticParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; init; }

    [JsonPropertyName("previousResultId")]
    public string? PreviousResultId { get; init; }

    // workDoneToken / partialResultToken are omitted for now – add when needed.
}

public record FullDocumentDiagnosticReport
{
    // This can either be full or unchanged
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("resultId")]
    public string? ResultId { get; init; }

    [JsonPropertyName("items")]
    public IEnumerable<Diagnostic>? Items { get; init; }
}

public record Diagnostic
{
    [JsonPropertyName("severity")]
    public DiagnosticSeverity? Severity { get; init; }

    [JsonPropertyName("range")]
    public Range? Range { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    // C# would return a string e.g. "CS1234"
    // Typescript would return a number 6133
    [JsonPropertyName("code")]
    [JsonConverter(typeof(DiagnosticCodeConverter))]
    public string? Code { get; init; }

    [JsonPropertyName("codeDescription")]
    public CodeDescription? CodeDescription { get; init; }

    [JsonPropertyName("tags")]
    public IEnumerable<int>? Tags { get; init; }
}

public record CodeDescription
{
    [JsonPropertyName("href")]
    public string? Href { get; init; }
}

public enum DiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
}

/// <summary>
/// Custom JSON converter that handles diagnostic codes which can be either strings (C#) or integers (TypeScript)
/// </summary>
public class DiagnosticCodeConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.GetInt32().ToString();
            case JsonTokenType.Null:
                return null;
            default:
                throw new JsonException($"Unexpected token type {reader.TokenType} for diagnostic code");
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}


