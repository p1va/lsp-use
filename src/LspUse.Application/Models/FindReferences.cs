namespace LspUse.Application.Models;

public record FindReferencesRequest
{
    public required string FilePath { get; init; }
    public required EditorPosition Position { get; init; }
    public bool IncludeDeclaration { get; init; } = true;
}

public record FindReferencesSuccess
{
    public required IEnumerable<SymbolLocation> Value { get; init; }
}
