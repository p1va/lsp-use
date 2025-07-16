using LspUse.LanguageServerClient.Models;

namespace LspUse.Application.Models;

public record EditorPosition
{
    public required uint Line { get; init; }
    public required uint Character { get; init; }
}
