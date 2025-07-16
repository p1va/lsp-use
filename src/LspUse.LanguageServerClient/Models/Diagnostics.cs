namespace LspUse.LanguageServerClient.Models;

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

    [JsonPropertyName("code")]
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


