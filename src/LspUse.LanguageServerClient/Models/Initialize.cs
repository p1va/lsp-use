namespace LspUse.LanguageServerClient.Models;

using System.Text.Json.Serialization;

public record InitializeParams
{
    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("rootUri")]
    public required Uri RootUri { get; init; }

    [JsonPropertyName("capabilities")]
    public required ClientCapabilities Capabilities { get; init; }
}

public record ClientCapabilities
{
    [JsonPropertyName("workspace")]
    public required WorkspaceClientCapabilities Workspace { get; init; }

    [JsonPropertyName("textDocument")]
    public required TextDocumentClientCapabilities TextDocument { get; init; }
}

public record WorkspaceClientCapabilities
{
    [JsonPropertyName("diagnostic")]
    public DiagnosticWorkspaceSetting? Diagnostic { get; init; }
}

public record DiagnosticWorkspaceSetting
{
    [JsonPropertyName("refreshSupport")]
    public required bool RefreshSupport { get; init; }

    [JsonPropertyName("workspaceDiagnostics")]
    public required bool WorkspaceDiagnostics { get; init; }
}

public record TextDocumentClientCapabilities
{
    [JsonPropertyName("diagnostic")]
    public DiagnosticTextDocumentSetting? Diagnostic { get; init; }

    [JsonPropertyName("synchronization")]
    public TextDocumentSynchronization? Synchronization { get; init; }

    [JsonPropertyName("publishDiagnostics")]
    public PublishDiagnosticsTextDocumentSetting? PublishDiagnostics { get; init; }
}

public record PublishDiagnosticsTextDocumentSetting
{
    [JsonPropertyName("relatedInformation")]
    public bool? RelatedInformation { get; init; }

    [JsonPropertyName("versionSupport")]
    public bool? VersionSupport { get; init; }

    [JsonPropertyName("codeDescriptionSupport")]
    public bool? CodeDescriptionSupport { get; init; }

    [JsonPropertyName("dataSupport")]
    public bool? DataSupport { get; init; }
}

public record DiagnosticTextDocumentSetting
{
    [JsonPropertyName("dynamicRegistration")]
    public required bool DynamicRegistration { get; init; }

    [JsonPropertyName("relatedDocumentSupport")]
    public required bool RelatedDocumentSupport { get; init; }
}

public record TextDocumentSynchronization
{
    [JsonPropertyName("dynamicRegistration")]
    public required bool DynamicRegistration { get; init; }

    [JsonPropertyName("relatedDocumentSupport")]
    public required bool RelatedDocumentSupport { get; init; }
}
