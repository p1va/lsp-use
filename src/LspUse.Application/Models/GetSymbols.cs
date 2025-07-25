namespace LspUse.Application.Models;

public record GetSymbolsRequest
{
    public required string FilePath { get; init; }

    /// <summary>
    /// Optional maximum depth override for this request.
    /// If null, uses the default depth from LSP profile configuration.
    /// 0 = only top-level symbols (no container)
    /// 1 = top-level + first level nested symbols, etc.
    /// </summary>
    public int? MaxDepth { get; init; }
}

public record GetSymbolsSuccess
{
    public required IEnumerable<DocumentSymbol> Symbols { get; init; }
}
