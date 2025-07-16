namespace LspUse.Application.Models;

public record SearchSymbolRequest
{
    public required string Query { get; init; }
}

public record SearchSymbolResponse
{
    public required IEnumerable<DocumentSymbol> Value { get; init; }
}
