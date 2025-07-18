using System.Text.Json.Serialization;

namespace LspUse.Application.Models;

public record RenameSymbolRequest
{
    public required string FilePath { get; init; }
    public required EditorPosition Position { get; init; }
    public required string NewName { get; init; }
}

public record RenameSymbolSuccess
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }
    [JsonPropertyName("changed_files")]
    public required IEnumerable<Uri> ChangedFiles { get; init; } = [];
    [JsonPropertyName("total_files_changed")]
    public required int TotalFilesChanged { get; init; }
    [JsonPropertyName("total_edits_changed")]
    public required int TotalEditsApplied { get; init; }
    [JsonPropertyName("total_lines_changed")]
    public required int TotalLinesChanged { get; init; }
    [JsonPropertyName("errors")]
    public required IEnumerable<string> Errors { get; init; } = [];
}
