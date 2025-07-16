using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class HoverTool
{
    [McpServerTool(Name = "hover", Title = "Hover", UseStructuredContent = true)]
    [Description(
        "Gets hover information for a symbol at a specific position in a file. Returns rich information including type signatures, documentation, and other symbol details.")]
    public static async Task<object?> HoverAsync(IApplicationService service,
        ILoggerFactory loggerFactory,
        [Description("The path of the file containing the symbol to get hover information for")]
        string file,
        [Description("The line number where the symbol is located (1-based)")] uint line,
        [Description("The character position on the line where the symbol is located (1-based)")]
        uint character, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("HoverTool");

        logger.LogInformation("MCP HoverTool called for {FilePath} at {Line}:{Character}", file,
            line, character);

        try
        {
            var result = await service.HoverAsync(new HoverRequest
            {
                FilePath = file,
                Position = new EditorPosition
                {
                    Line = line,
                    Character = character
                }
            }, cancellationToken);

            logger.LogInformation("MCP HoverTool returning hover content: {HasContent}",
                result.Value != null);

            return result.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MCP HoverTool for {FilePath} at {Line}:{Character}", file,
                line, character);

            throw;
        }
    }
}
