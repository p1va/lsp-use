using System.Text.Json.Serialization;

namespace LspUse.Application.Models;

public record GoToRequest
{
    public required string FilePath { get; init; }
    public required EditorPosition Position { get; init; }
}

public record GoToSuccess
{
    public required IEnumerable<SymbolLocation> Locations { get; init; }
}
