using LspUse.Application.Models;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Models;

namespace LspUse.Application;

public static class Extensions
{
    public static TextDocumentPositionParams
        ToParams(this (Uri fileUri, EditorPosition position) x) =>
        new()
        {
            TextDocument = x.fileUri.ToDocumentIdentifier(),
            Position = x.position.ToZeroBased()
        };

    public static ZeroBasedPosition ToZeroBased(this EditorPosition position) =>
        new()
        {
            Line = position.Line - 1,
            Character = position.Character - 1
        };

    // TODO: Unit test to make sure in case of no range it returns null instead of 1
    public static SymbolLocation ToSymbolLocation(this Location position) =>
        new()
        {
            FilePath = position.Uri,
            StartLine = position.Range?.Start?.Line + 1,
            StartCharacter = position.Range?.Start?.Character + 1,
            EndLine = position.Range?.End?.Line + 1,
            EndCharacter = position.Range?.End?.Character + 1
        };

    /// <summary>
    /// Enriches a collection of SymbolLocation objects with the actual text content at their positions.
    /// Groups by file path for efficient file reading.
    /// </summary>
    public static async Task<IEnumerable<SymbolLocation>> EnrichWithTextAsync(
        this IEnumerable<SymbolLocation> locations,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SymbolLocation>();

        // Group by file path to minimize file reads
        var groupedByFile = locations
            .Where(loc => loc.FilePath != null)
            .GroupBy(loc => loc.FilePath!)
            .ToList();

        foreach (var fileGroup in groupedByFile)
        {
            var filePath = fileGroup.Key;
            var fileLocations = fileGroup.ToList();

            try
            {
                // Skip files that don't exist or are not local files
                if (!filePath.IsFile || !File.Exists(filePath.LocalPath))
                {
                    // Add locations without text for missing files
                    results.AddRange(fileLocations);
                    continue;
                }

                // Read file content once for all locations in this file
                var fileContent = await File.ReadAllTextAsync(filePath.LocalPath, cancellationToken);
                var lines = fileContent.Split('\n');

                foreach (var location in fileLocations)
                {
                    var extractedText = ExtractTextFromLocation(lines, location);
                    results.Add(location with { Text = extractedText });
                }
            }
            catch (Exception)
            {
                // If any error occurs, add locations without text
                results.AddRange(fileLocations);
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts text content from file lines based on SymbolLocation coordinates.
    /// Handles both single-line and multi-line selections.
    /// </summary>
    private static string? ExtractTextFromLocation(string[] lines, SymbolLocation location)
    {
        if (location.StartLine == null || location.StartCharacter == null ||
            location.EndLine == null || location.EndCharacter == null)
        {
            return null;
        }

        var startLine = (int)location.StartLine.Value - 1; // Convert to 0-based
        _ = (int)location.StartCharacter.Value - 1; // Convert to 0-based
        var endLine = (int)location.EndLine.Value - 1; // Convert to 0-based
        _ = (int)location.EndCharacter.Value - 1; // Convert to 0-based

        // Validate bounds
        if (startLine < 0 || startLine >= lines.Length ||
            endLine < 0 || endLine >= lines.Length)
        {
            return null;
        }

        try
        {
            // Single line selection - return the full line trimmed
            if (startLine == endLine)
            {
                var line = lines[startLine];
                if (startLine < 0 || startLine >= lines.Length)
                {
                    return null;
                }
                return line.Trim();
            }

            // Multi-line selection - collect all lines and concatenate with spaces
            var result = new List<string>();

            // Collect all lines from start to end
            for (var i = startLine; i <= endLine && i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    result.Add(line);
                }
            }

            var fullText = string.Join(" ", result);
            
            // Apply truncation for symbols that span too many lines or are too long
            const int maxLines = 3;
            const int maxCharacters = 200;
            var lineCount = endLine - startLine + 1;
            
            if (lineCount > maxLines || fullText.Length > maxCharacters)
            {
                // For large spans, return just the first few meaningful lines
                var truncatedResult = new List<string>();
                var charCount = 0;
                
                for (var i = startLine; i <= endLine && i < lines.Length && truncatedResult.Count < maxLines; i++)
                {
                    var line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        if (charCount + line.Length > maxCharacters && truncatedResult.Count > 0)
                        {
                            break;
                        }
                        truncatedResult.Add(line);
                        charCount += line.Length + 1; // +1 for space
                    }
                }
                
                var truncatedText = string.Join(" ", truncatedResult);
                return truncatedText + (lineCount > maxLines || fullText.Length > maxCharacters ? "..." : "");
            }

            return fullText;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Enriches a collection of DocumentDiagnostic objects with the actual text content at their positions.
    /// Groups by file path for efficient file reading.
    /// </summary>
    public static async Task<IEnumerable<DocumentDiagnostic>> EnrichWithTextAsync(
        this IEnumerable<DocumentDiagnostic> diagnostics,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var diagnosticsList = diagnostics.ToList();
        if (!diagnosticsList.Any())
            return diagnosticsList;

        try
        {
            // Read the file content once
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var lines = content.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

            // Enrich each diagnostic with text content
            return diagnosticsList.Select(diagnostic =>
            {
                var text = ExtractTextFromDiagnostic(lines, diagnostic);
                return diagnostic with { Text = text };
            });
        }
        catch
        {
            // If file reading fails, return diagnostics without text enrichment
            return diagnosticsList;
        }
    }

    /// <summary>
    /// Extracts text content from a diagnostic position.
    /// Handles both single-line and multi-line selections.
    /// </summary>
    private static string? ExtractTextFromDiagnostic(string[] lines, DocumentDiagnostic diagnostic)
    {
        var startLine = (int)diagnostic.StartLine; // Already 0-based in our model
        _ = (int)diagnostic.StartCharacter; // Already 0-based in our model
        var endLine = (int)diagnostic.EndLine; // Already 0-based in our model
        _ = (int)diagnostic.EndCharacter; // Already 0-based in our model

        // Validate bounds
        if (startLine < 0 || startLine >= lines.Length ||
            endLine < 0 || endLine >= lines.Length)
        {
            return null;
        }

        try
        {
            // Single line selection - return the full line trimmed
            if (startLine == endLine)
            {
                var line = lines[startLine];
                return line.Trim();
            }

            // Multi-line selection - collect all lines and concatenate with spaces
            var result = new List<string>();

            // Collect all lines from start to end
            for (var i = startLine; i <= endLine && i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    result.Add(line);
                }
            }

            return string.Join(" ", result);
        }
        catch
        {
            return null;
        }
    }
}
