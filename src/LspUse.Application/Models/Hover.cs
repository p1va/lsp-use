namespace LspUse.Application.Models;

public record HoverRequest
{
    public required string FilePath { get; init; }
    public required EditorPosition Position { get; init; }
}

public record HoverSuccess
{
    public required string? Value { get; init; }
}
