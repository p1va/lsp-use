namespace LspUse.LanguageServerClient.Models;

using System.Text.Json.Serialization;

public enum CompletionTriggerKind { Invoked = 1 }

public record CompletionContext
{
    [JsonPropertyName("triggerKind")]
    public required CompletionTriggerKind TriggerKind { get; init; }
}

public record CompletionParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }

    [JsonPropertyName("position")]
    public required ZeroBasedPosition Position { get; init; }

    [JsonPropertyName("context")]
    public required CompletionContext Context { get; init; }
}

public record DocumentSymbolParams
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
}

public record CompletionItem
{
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("kind")]
    public CompletionItemKind? Kind { get; init; }
}

public record CompletionList
{
    [JsonPropertyName("isIncomplete")]
    public bool IsIncomplete { get; init; }

    [JsonPropertyName("items")]
    public CompletionItem[]? Items { get; init; }
}

public enum CompletionItemKind
{
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
    Color = 16,
    File = 17,
    Reference = 18,
    Folder = 19,
    EnumMember = 20,
    Constant = 21,
    Struct = 22,
    Event = 23,
    Operator = 24,
    TypeParameter = 25,
}

