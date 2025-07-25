using System.ComponentModel;
using System.Text;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class FindReferencesTool
{
    private const string ToolName = "find_references";
    private const string ToolTitle = "FindReferences";

    private const string ToolDescription =
        "Finds all references of a symbol starting from its location in the C# codebase. Returns both the symbol declaration and all usages across the solution.";

    private const string ToolArgDescFilePath =
        "The path of the file which contains the symbol for which references are to be searched";

    private const string ToolArgDescLine =
        "The line number at which the symbol is located (1-based)";

    private const string ToolArgDescCharacter =
        "The character number at which the symbol is located (1-based)";

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = false)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<TextContentBlock>> FindReferencesAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string file, [Description(ToolArgDescLine)] uint line,
        [Description(ToolArgDescCharacter)] uint character, 
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger("FindReferencesTool");

        logger.LogInformation("MCP FindReferencesTool called for {FilePath} at {Line}:{Character}",
            file, line, character);

        var result = await service.FindReferencesAsync(new FindReferencesRequest
        {
            FilePath = file,
            Position = new EditorPosition
            {
                Line = line,
                Character = character
            },
            IncludeDeclaration = true
        }, cancellationToken);

        return result.Match<IEnumerable<TextContentBlock>>(
            success =>
            {
                var references = success.Value;
                logger.LogInformation("MCP FindReferencesTool returning {Count} references", references.Count());
                
                if (!references.Any())
                {
                    return [new TextContentBlock { Text = $"Found 0 references for symbol at {GetRelativeFilePath(file)}:{line}:{character}" }];
                }

                return BuildReferencesResultText(references, file, line, character);
            },
            error =>
            {
                logger.LogError("MCP FindReferencesTool error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for find references error");
                }
                throw new InvalidOperationException($"Find references operation failed: {error.Message}", error.Exception);
            }
        );
    }

    private static string GetRelativeFilePath(string filePath)
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var uri = new Uri(filePath);
            var localPath = uri.LocalPath;
            return Path.GetRelativePath(currentDirectory, localPath);
        }
        catch
        {
            return filePath;
        }
    }

    private static IEnumerable<TextContentBlock> BuildReferencesResultText(
        IEnumerable<SymbolLocation> references, string originalFile, uint originalLine, uint originalCharacter)
    {
        var referenceList = references.ToList();
        var fileGroups = referenceList.GroupBy(r => r.FilePath?.ToString() ?? "Unknown")
                                      .OrderBy(g => GetRelativeFilePath(g.Key))
                                      .ToList();

        // Summary
        var fileCount = fileGroups.Count;
        var referenceCount = referenceList.Count;
        var summary = $"Found {referenceCount} reference{(referenceCount != 1 ? "s" : "")} for symbol at {GetRelativeFilePath(originalFile)}:{originalLine}:{originalCharacter} across {fileCount} file{(fileCount != 1 ? "s" : "")}:";

        yield return new TextContentBlock { Text = summary };

        // File groups
        foreach (var fileGroup in fileGroups)
        {
            var filePath = GetRelativeFilePath(fileGroup.Key);
            var referencesInFile = fileGroup.OrderBy(r => r.StartLine).ToList();
            
            var fileHeader = $"{filePath} ({referencesInFile.Count} reference{(referencesInFile.Count != 1 ? "s" : "")})";
            
            var sb = new StringBuilder(fileHeader);
            
            foreach (var reference in referencesInFile)
            {
                var line = reference.StartLine ?? 0; // Already 1-based
                var character = reference.StartCharacter ?? 0; // Already 1-based
                
                sb.AppendLine();
                sb.Append($"  {line}:{character}");
                
                if (!string.IsNullOrWhiteSpace(reference.Text))
                {
                    sb.Append($" - {reference.Text.Trim()}");
                }
                
            }
            
            yield return new TextContentBlock { Text = sb.ToString() };
        }
    }
}
