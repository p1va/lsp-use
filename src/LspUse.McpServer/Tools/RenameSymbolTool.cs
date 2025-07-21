using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class RenameSymbolTool
{
    private const string ToolName = "rename_symbol";
    private const string ToolTitle = "RenameSymbol";

    private const string ToolDescription =
        "Renames a symbol across the entire codebase using LSP rename functionality";

    private const string ToolArgDescFilePath = "Path to the file containing the symbol to rename";
    private const string ToolArgDescLine = "Line number where the symbol is located (1-based)";
    private const string ToolArgDescCharacter = "Character position on the line (1-based)";
    private const string ToolArgDescNewName = "New name for the symbol";

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = true)]
    [Description(ToolDescription)]
    public static async Task<RenameSymbolSuccess> RenameSymbolAsync(
        IApplicationService applicationService, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string filePath,
        [Description(ToolArgDescLine)] uint line,
        [Description(ToolArgDescCharacter)] uint character,
        [Description(ToolArgDescNewName)] string newName,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(RenameSymbolTool));

        logger.LogInformation("Renaming symbol at {FilePath}:{Line}:{Character} to '{NewName}'",
            filePath, line, character, newName);

        var request = new RenameSymbolRequest
        {
            FilePath = filePath,
            Position = new EditorPosition
            {
                Line = line,
                Character = character
            },
            NewName = newName
        };

        var result = await applicationService.RenameSymbolAsync(request, cancellationToken);

        return result.Match(
            success =>
            {
                logger.LogInformation("Symbol rename completed successfully. Changed {FileCount} files with {TotalEdits} edits",
                    success.ChangedFiles.Count(), success.TotalEditsApplied);
                return success;
            },
            error =>
            {
                logger.LogError("Symbol rename error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for symbol rename error");
                }
                throw new InvalidOperationException($"Symbol rename operation failed: {error.Message}", error.Exception);
            }
        );
    }
}
