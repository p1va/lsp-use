using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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
    public static async Task<RenameSymbolResult> RenameSymbolAsync(
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

        try
        {
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

            return await applicationService.RenameSymbolAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during symbol rename operation");

            throw;
        }
    }
}
