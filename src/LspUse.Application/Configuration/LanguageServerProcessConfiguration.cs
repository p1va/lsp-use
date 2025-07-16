namespace LspUse.Application.Configuration;

/// <summary>
/// Configuration describing how a language server process has to be launched
/// and which workspace the client should open.  Exactly <em>one</em> of the
/// workspace selectors – <see cref="SolutionPath"/> or <see cref="ProjectPaths"/>
/// – must be provided (one-of semantics).
/// </summary>
public record LanguageServerProcessConfiguration
{
    public required string Command { get; init; }
    public required IEnumerable<string>? Arguments { get; init; }
    public required string WorkspacePath { get; init; }
    public string? SolutionPath { get; init; }
    public IReadOnlyList<string>? ProjectPaths { get; init; }

    public override string ToString() => $"{Command} {string.Join(' ', Arguments ?? [])} Workspace Path: {WorkspacePath} Solution Path: {SolutionPath} Project Paths: {string.Join(' ', ProjectPaths ?? [])}";
}
