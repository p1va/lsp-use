using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = true)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<SymbolLocation>> GoToTypeDefinitionAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string file, [Description(ToolArgDescLine)] uint line,
        [Description(ToolArgDescCharacter)] uint character, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(ToolName);

        logger.LogInformation("{File} at {Line}:{Character}", file, line, character);

        try
        {
            var result = await service.GoToTypeDefinitionAsync(new GoToRequest
            {
                FilePath = file,
                Position = new EditorPosition
                {
                    Line = line,
                    Character = character
                }
            }, cancellationToken);

            return result.Locations;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error for {FilePath} at {Line}:{Character}", file, line,
                character);

            throw;
        }
    }
}
