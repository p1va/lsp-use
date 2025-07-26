namespace LspUse.LanguageServerClient;

public record LanguageServerProcessConfiguration
{
    public required string Command { get; init; }
    public required IEnumerable<string>? Arguments { get; init; }
    public required string WorkingDirectory { get; init; }
    public Dictionary<string, string>? Environment { get; init; }
}
