namespace LspUse.LanguageServerClient.Models;

using System.Text.Json.Serialization;

public record TextDocumentPositionParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }

    [JsonPropertyName("position")]
    public required ZeroBasedPosition Position { get; init; }
}

public record Hover
{
    [JsonPropertyName("contents")]
    public HoverContents? Contents { get; init; }
}

public record HoverContents
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

public enum SymbolKind
{
    File = 1,
    Module = 2,
    Namespace = 3,
    Package = 4,
    Class = 5,
    Method = 6,
    Property = 7,
    Field = 8,
    Constructor = 9,
    Enum = 10,
    Interface = 11,
    Function = 12,
    Variable = 13,
    Constant = 14,
    String = 15,
    Number = 16,
    Boolean = 17,
    Array = 18,
    Object = 19,
    Key = 20,
    Null = 21,
    EnumMember = 22,
    Struct = 23,
    Event = 24,
    Operator = 25,
    TypeParameter = 26
}

public record SymbolInformation
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("kind")]
    public required SymbolKind Kind { get; init; }

    [JsonPropertyName("containerName")]
    public string? ContainerName { get; init; }

    [JsonPropertyName("location")]
    public Location? Location { get; init; }
}
