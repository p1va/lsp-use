using System.Text.Json.Serialization;

namespace LspUse.LanguageServerClient.Models;

public record Registration
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("registerOptions")]
    public RegisterOptions? RegisterOptions { get; init; }
}

public record RegisterOptions
{
    [JsonPropertyName("workDoneProgress")]
    public bool? WorkDoneProgress { get; init; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; init; }

    [JsonPropertyName("interFileDependencies")]
    public bool? InterFileDependencies { get; init; }

    [JsonPropertyName("workspaceDiagnostics")]
    public bool? WorkspaceDiagnostics { get; init; }
}

public record ClientCapabilityRegistrationParams
{
    [JsonPropertyName("registrations")]
    public Registration[] Registrations { get; init; } = [];
}
