using LspUse.Application.Models;
using LspUse.LanguageServerClient.Models;
using Microsoft.Extensions.Logging;

namespace LspUse.Application;

public class WorkspaceEditApplicator
{
    private readonly ILogger<WorkspaceEditApplicator> _logger;

    public WorkspaceEditApplicator(ILogger<WorkspaceEditApplicator> logger) => _logger = logger;

    public async Task<WorkspaceEditResult> ApplyAsync(WorkspaceEdit workspaceEdit,
        CancellationToken cancellationToken = default)
    {
        if (workspaceEdit.DocumentChanges.All(change => change.TextDocument?.Uri is null))
        {
            _logger.LogInformation("No document changes to apply");

            return new WorkspaceEditResult
            {
                Errors = [],
                FilesChanged = []
            };
        }

        _logger.LogInformation("Applying workspace edit to {DocumentCount} documents",
            workspaceEdit.DocumentChanges.Count());

        var filesChanged = new List<FileChangeResult>();
        var errors = new List<string>();

        foreach (var documentChange in workspaceEdit.DocumentChanges.Where(c =>
                     c.TextDocument?.Uri is not null))
        {
            try
            {
                var fileResult = await ApplyDocumentChangeAsync(documentChange, cancellationToken);
                filesChanged.Add(fileResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply changes to document {Uri}",
                    documentChange.TextDocument?.Uri);
                errors.Add(
                    $"Failed to apply changes to {documentChange.TextDocument?.Uri}: {ex.Message}");
            }
        }

        return new WorkspaceEditResult
        {
            FilesChanged = filesChanged.ToArray(),
            Errors = errors.ToArray()
        };
    }

    private async Task<FileChangeResult> ApplyDocumentChangeAsync(DocumentChange documentChange,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentChange);
        ArgumentNullException.ThrowIfNull(documentChange.TextDocument);
        ArgumentNullException.ThrowIfNull(documentChange.TextDocument.Uri);
        ArgumentNullException.ThrowIfNull(documentChange.Edits);

        var uri = documentChange.TextDocument.Uri;

        _logger.LogDebug("Applying {EditCount} edits to file {FilePath}",
            documentChange.Edits.Count(), uri.LocalPath);

        if (!File.Exists(uri.LocalPath))
            throw new FileNotFoundException($"File not found: {uri.LocalPath}");

        var fileContent = await File.ReadAllTextAsync(uri.LocalPath, cancellationToken);

        // TODO: Fix nullable mess

        // Sort edits by position in reverse order (end to start) to avoid offset issues
        var sortedEdits = documentChange.Edits.OrderByDescending(e => e.Range!.Start!.Line)
            .ThenByDescending(e => e.Range!.Start!.Character)
            .ToArray();

        foreach (var edit in sortedEdits) fileContent = ApplyTextEdit(fileContent, edit);

        await File.WriteAllTextAsync(uri.LocalPath, fileContent, cancellationToken);

        return new FileChangeResult
        {
            FilePath = uri,
            EditsApplied = sortedEdits.Length,
            LinesChanged = CalculateLinesChanged(sortedEdits)
        };
    }

    private static string ApplyTextEdit(string content, TextEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(edit.Range);

        var lines = content.Split('\n');
        var startLine = edit.Range.Start!.Line;
        var startChar = edit.Range.Start.Character;
        var endLine = edit.Range.End!.Line;
        var endChar = edit.Range.End.Character;

        // Validate range bounds
        if (startLine >= lines.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(edit),
                $"Start line {startLine} is out of range");
        }

        if (endLine >= lines.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(edit),
                $"End line {endLine} is out of range");
        }

        if (startLine == endLine)
        {
            // Single line edit
            var line = lines[startLine];

            if (startChar > (uint)line.Length || endChar > (uint)line.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(edit),
                    $"Character position out of range on line {startLine}");
            }

            lines[startLine] = line.Substring(0, (int)startChar) + edit.NewText +
                               line.Substring((int)endChar);
        }
        else
        {
            // Multi-line edit
            var startLineText = lines[startLine];
            var endLineText = lines[endLine];

            if (startChar > (uint)startLineText.Length || endChar > (uint)endLineText.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(edit),
                    "Character position out of range");
            }

            // Create the new content by combining start of first line + newText + end of last line
            var newContent = startLineText.Substring(0, (int)startChar) + edit.NewText +
                             endLineText.Substring((int)endChar);
            var newLines = newContent.Split('\n');

            // Replace the range of lines with the new content
            var result =
                new List<string>(lines.Length - (int)(endLine - startLine) + newLines.Length);
            result.AddRange(lines.Take((int)startLine));
            result.AddRange(newLines);
            result.AddRange(lines.Skip((int)endLine + 1));

            lines = result.ToArray();
        }

        return string.Join('\n', lines);
    }

    private static int CalculateLinesChanged(TextEdit[] edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        // Calculate unique lines that were modified
        var linesModified = new HashSet<uint>();

        foreach (var edit in edits)
        {
            // TODO: Fix
            ArgumentNullException.ThrowIfNull(edit.Range);

            for (var line = edit.Range.Start!.Line; line <= edit.Range.End!.Line; line++)
                linesModified.Add(line);
        }

        return linesModified.Count;
    }
}
