namespace LspUse.Application.Models;

public record WorkspaceEditResult
{
    public required FileChangeResult[] FilesChanged { get; init; } = [];
    public required string[] Errors { get; init; } = [];
    public bool HasErrors => Errors.Length > 0;
    public int TotalFilesChanged => FilesChanged.Length;
    public int TotalEditsApplied => FilesChanged.Sum(f => f.EditsApplied);
    public int TotalLinesChanged => FilesChanged.Sum(f => f.LinesChanged);
}

public record FileChangeResult
{
    public required Uri FilePath { get; init; }
    public required int EditsApplied { get; init; }
    public required int LinesChanged { get; init; }
}
