using LspUse.LanguageServerClient.Models;

namespace LspUse.LanguageServerClient;

public record DocumentClientRequest
{
    public required Uri Document { get; init; }
    public required ZeroBasedPosition Position { get; init; }
}
