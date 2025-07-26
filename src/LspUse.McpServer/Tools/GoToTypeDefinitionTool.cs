using System.ComponentModel;
using System.Text;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class GoToTypeDefinitionTool
{
    private const string ToolName = "go_to_type_definition";
    private const string ToolTitle = "GoToTypeDefinition";

    private const string ToolDescription =
        "Navigates to the definition of the type of a symbol starting from its location. Returns the source location where the symbol's own type is defined";

    private const string ToolArgDescFilePath =
        "The path of the file which contains the symbol for which type are to be searched";

    private const string ToolArgDescLine =
        "The line number at which the symbol is located (1-based)";

    private const string ToolArgDescCharacter =
        "The character number at which the symbol is located (1-based)";

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = false)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<TextContentBlock>> GoToTypeDefinitionAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string file, [Description(ToolArgDescLine)] uint line,
        [Description(ToolArgDescCharacter)] uint character,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(ToolName);

        logger.LogInformation("{File} at {Line}:{Character}", file, line, character);

        var result = await service.GoToTypeDefinitionAsync(new GoToRequest
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
                var typeDefinitions = success.Locations;
                logger.LogInformation("MCP GoToTypeDefinitionTool returning {Count} locations", typeDefinitions.Count());

                if (!typeDefinitions.Any())
                {
                    return [new TextContentBlock { Text = $"Found 0 type definitions for symbol at {GetRelativeFilePath(file)}:{line}:{character}" }];
                }

                return BuildTypeDefinitionsResultText(typeDefinitions, file, line, character);
            },
            error =>
            {
                logger.LogError("MCP GoToTypeDefinitionTool error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for go to type definition error");
                }
                throw new InvalidOperationException($"Go to type definition operation failed: {error.Message}", error.Exception);
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

    private static IEnumerable<TextContentBlock> BuildTypeDefinitionsResultText(
        IEnumerable<SymbolLocation> typeDefinitions, string originalFile, uint originalLine, uint originalCharacter)
    {
        var typeDefinitionList = typeDefinitions.ToList();
        var fileGroups = typeDefinitionList.GroupBy(t => t.FilePath?.ToString() ?? "Unknown")
                                           .OrderBy(g => GetRelativeFilePath(g.Key))
                                           .ToList();

        // Summary
        var fileCount = fileGroups.Count;
        var typeDefinitionCount = typeDefinitionList.Count;
        var summary = $"Found {typeDefinitionCount} type definition{(typeDefinitionCount != 1 ? "s" : "")} for symbol at {GetRelativeFilePath(originalFile)}:{originalLine}:{originalCharacter} across {fileCount} file{(fileCount != 1 ? "s" : "")}:";

        yield return new TextContentBlock { Text = summary };

        // File groups
        foreach (var fileGroup in fileGroups)
        {
            var filePath = GetRelativeFilePath(fileGroup.Key);
            var typeDefinitionsInFile = fileGroup.OrderBy(t => t.StartLine).ToList();

            var fileHeader = $"{filePath} ({typeDefinitionsInFile.Count} type definition{(typeDefinitionsInFile.Count != 1 ? "s" : "")})";

            var sb = new StringBuilder(fileHeader);

            foreach (var typeDefinition in typeDefinitionsInFile)
            {
                var line = typeDefinition.StartLine ?? 0; // Already 1-based
                var character = typeDefinition.StartCharacter ?? 0; // Already 1-based

                sb.AppendLine();
                sb.Append($"  {line}:{character}");

                if (!string.IsNullOrWhiteSpace(typeDefinition.Text))
                {
                    sb.Append($" - {typeDefinition.Text.Trim()}");
                }
            }

            yield return new TextContentBlock { Text = sb.ToString() };
        }
    }
}
