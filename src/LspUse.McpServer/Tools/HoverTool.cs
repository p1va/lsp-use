using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OneOf;

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

        var result = await service.HoverAsync(new HoverRequest
        {
            FilePath = file,
            Position = new EditorPosition
            {
                Line = line,
                Character = character
            }
        }, cancellationToken);

        return result.Match<object?>(
            success =>
            {
                logger.LogInformation("MCP HoverTool returning hover content: {HasContent}",
                    success.Value != null);
                return success.Value;
            },
            error =>
            {
                logger.LogError("MCP HoverTool error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for hover error");
                }
                throw new InvalidOperationException($"Hover operation failed: {error.Message}", error.Exception);
            }
        );
    }
}
