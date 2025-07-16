using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = true)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<SymbolLocation>> FindReferencesAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string file, [Description(ToolArgDescLine)] uint line,
        [Description(ToolArgDescCharacter)] uint character, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("FindReferencesTool");

        logger.LogInformation("MCP FindReferencesTool called for {FilePath} at {Line}:{Character}",
            file, line, character);

        try
        {
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

            logger.LogInformation("MCP FindReferencesTool returning {Count} references",
                result.Value.Count());

            return result.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error in MCP FindReferencesTool for {FilePath} at {Line}:{Character}", file, line,
                character);

            throw;
        }
    }
}
