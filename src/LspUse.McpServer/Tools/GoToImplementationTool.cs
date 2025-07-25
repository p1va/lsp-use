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
public static class GoToImplementationTool
{
    private const string ToolName = "go_to_implementation";
    private const string ToolTitle = "GoToImplementation";

    private const string ToolDescription =
        "Navigates to the implementation of a symbol starting from its location. Returns the source location where the symbol's implementation lives";

    private const string ToolArgDescFilePath =
        "The path of the file which contains the symbol for which implementation are to be searched";

    private const string ToolArgDescLine =
        "The line number at which the symbol is located (1-based)";

    private const string ToolArgDescCharacter =
        "The character number at which the symbol is located (1-based)";

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = false)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<TextContentBlock>> GoToImplementationAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string file, [Description(ToolArgDescLine)] uint line,
        [Description(ToolArgDescCharacter)] uint character,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(ToolName);

        logger.LogInformation("{File} at {Line}:{Character}", file, line, character);

        var result = await service.GoToImplementationAsync(new GoToRequest
        {
            FilePath = file,
            Position = new EditorPosition
            {
                Line = line,
                Character = character
            }
        }, cancellationToken);

        return result.Match<IEnumerable<TextContentBlock>>(
            success =>
            {
                var implementations = success.Locations;
                logger.LogInformation("MCP GoToImplementationTool returning {Count} locations", implementations.Count());
                
                if (!implementations.Any())
                {
                    return [new TextContentBlock { Text = $"Found 0 implementations for symbol at {GetRelativeFilePath(file)}:{line}:{character}" }];
                }

                return BuildImplementationsResultText(implementations, file, line, character);
            },
            error =>
            {
                logger.LogError("MCP GoToImplementationTool error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for go to implementation error");
                }
                throw new InvalidOperationException($"Go to implementation operation failed: {error.Message}", error.Exception);
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

    private static IEnumerable<TextContentBlock> BuildImplementationsResultText(
        IEnumerable<SymbolLocation> implementations, string originalFile, uint originalLine, uint originalCharacter)
    {
        var implementationList = implementations.ToList();
        var fileGroups = implementationList.GroupBy(i => i.FilePath?.ToString() ?? "Unknown")
                                           .OrderBy(g => GetRelativeFilePath(g.Key))
                                           .ToList();

        // Summary
        var fileCount = fileGroups.Count;
        var implementationCount = implementationList.Count;
        var summary = $"Found {implementationCount} implementation{(implementationCount != 1 ? "s" : "")} for symbol at {GetRelativeFilePath(originalFile)}:{originalLine}:{originalCharacter} across {fileCount} file{(fileCount != 1 ? "s" : "")}:";

        yield return new TextContentBlock { Text = summary };

        // File groups
        foreach (var fileGroup in fileGroups)
        {
            var filePath = GetRelativeFilePath(fileGroup.Key);
            var implementationsInFile = fileGroup.OrderBy(i => i.StartLine).ToList();
            
            var fileHeader = $"{filePath} ({implementationsInFile.Count} implementation{(implementationsInFile.Count != 1 ? "s" : "")})";
            
            var sb = new StringBuilder(fileHeader);
            
            foreach (var implementation in implementationsInFile)
            {
                var line = implementation.StartLine ?? 0; // Already 1-based
                var character = implementation.StartCharacter ?? 0; // Already 1-based
                
                sb.AppendLine();
                sb.Append($"  {line}:{character}");
                
                if (!string.IsNullOrWhiteSpace(implementation.Text))
                {
                    sb.Append($" - {implementation.Text.Trim()}");
                }
            }
            
            yield return new TextContentBlock { Text = sb.ToString() };
        }
    }
}
