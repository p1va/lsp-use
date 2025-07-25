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
public static class GoToDefinitionTool
{
    private const string ToolName = "go_to_definition";
    private const string ToolTitle = "GoToDefinition";

    private const string ToolDescription =
        "Navigates to the definition of a symbol starting from its location. Returns the source location where the symbol is defined";

    private const string ToolArgDescFilePath =
        "The path of the file which contains the symbol for which definition are to be searched";

    private const string ToolArgDescLine =
        "The line number at which the symbol is located (1-based)";

    private const string ToolArgDescCharacter =
        "The character number at which the symbol is located (1-based)";

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = false)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<TextContentBlock>> GoToDefinitionAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string file, [Description(ToolArgDescLine)] uint line,
        [Description(ToolArgDescCharacter)] uint character,
        [Description("Whether to include code snippets for each definition. When true, shows the actual source code for better context.")] bool showCode = false,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(ToolName);

        logger.LogInformation("{File} at {Line}:{Character}", file, line, character);

        var result = await service.GoToDefinitionAsync(new GoToRequest
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
                var definitions = success.Locations;
                logger.LogInformation("MCP GoToDefinitionTool returning {Count} locations", definitions.Count());
                
                if (!definitions.Any())
                {
                    return [new TextContentBlock { Text = $"Found 0 definitions for symbol at {GetRelativeFilePath(file)}:{line}:{character}" }];
                }

                return BuildDefinitionsResultText(definitions, file, line, character, showCode);
            },
            error =>
            {
                logger.LogError("MCP GoToDefinitionTool error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for go to definition error");
                }
                throw new InvalidOperationException($"Go to definition operation failed: {error.Message}", error.Exception);
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

    private static IEnumerable<TextContentBlock> BuildDefinitionsResultText(
        IEnumerable<SymbolLocation> definitions, string originalFile, uint originalLine, uint originalCharacter, bool showCode)
    {
        var definitionList = definitions.ToList();
        var fileGroups = definitionList.GroupBy(d => d.FilePath?.ToString() ?? "Unknown")
                                       .OrderBy(g => GetRelativeFilePath(g.Key))
                                       .ToList();

        // Summary
        var fileCount = fileGroups.Count;
        var definitionCount = definitionList.Count;
        var summary = $"Found {definitionCount} definition{(definitionCount != 1 ? "s" : "")} for symbol at {GetRelativeFilePath(originalFile)}:{originalLine}:{originalCharacter} across {fileCount} file{(fileCount != 1 ? "s" : "")}:";

        yield return new TextContentBlock { Text = summary };

        // File groups
        foreach (var fileGroup in fileGroups)
        {
            var filePath = GetRelativeFilePath(fileGroup.Key);
            var definitionsInFile = fileGroup.OrderBy(d => d.StartLine).ToList();
            
            var fileHeader = $"\n{filePath} ({definitionsInFile.Count} definition{(definitionsInFile.Count != 1 ? "s" : "")})";
            
            var sb = new StringBuilder(fileHeader);
            
            foreach (var definition in definitionsInFile)
            {
                var line = definition.StartLine ?? 0; // Already 1-based
                var character = definition.StartCharacter ?? 0; // Already 1-based
                
                sb.AppendLine();
                sb.Append($"  {line}:{character}");
                
                if (!string.IsNullOrWhiteSpace(definition.Text))
                {
                    sb.Append($" - {definition.Text.Trim()}");
                }
                
                if (showCode && !string.IsNullOrWhiteSpace(definition.Text))
                {
                    sb.AppendLine();
                    sb.Append($"    Code: {definition.Text.Trim()}");
                }
            }
            
            yield return new TextContentBlock { Text = sb.ToString() };
        }
    }
}
