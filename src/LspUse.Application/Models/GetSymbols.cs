namespace LspUse.Application.Models;

public record GetSymbolsRequest
{
    public required string FilePath { get; init; }
}

public record GetSymbolsSuccess
{
    public required IEnumerable<DocumentSymbol> Symbols { get; init; }
}
