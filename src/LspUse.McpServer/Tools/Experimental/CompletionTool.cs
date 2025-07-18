using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class CompletionTool
{
    [McpServerTool(Name = "completion", Title = "Completion", UseStructuredContent = true)]
    [Description(
        "Gets code completion suggestions at a specific position in a file. Returns a list of possible completions including variables, methods, classes, and other language constructs available at the cursor position. Useful for exploring what's available in scope or after an object/class reference.")]
    public static async Task<object> CompletionAsync(IApplicationService service,
        ILoggerFactory loggerFactory,
        [Description("The path of the file to get completions for")] string file,
        [Description("The line number where completions are requested (1-based)")] uint line,
        [Description(
            "The character position on the line where completions are requested (1-based)")]
        uint character, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CompletionTool");

        logger.LogInformation("MCP CompletionTool called for {FilePath} at {Line}:{Character}",
            file, line, character);

        var result = await service.CompletionAsync(new CompletionRequest
        {
            FilePath = file,
            Position = new EditorPosition
            {
                Line = line,
                Character = character
            }
        }, cancellationToken);

        return result.Match(
            success =>
            {
                logger.LogInformation("MCP CompletionTool returning {Count} completion items",
                    success.Items.Count());

                return new
                {
                    Items = success.Items.Select(item => new
                    {
                        item.Label,
                        Kind = item.Kind?.ToString(),
                        KindValue = (int?)item.Kind
                    })
                        .ToArray(),
                    Metadata = new
                    {
                        success.IsIncomplete,
                        ItemCount = success.Items.Count(),
                        Position = new
                        {
                            Line = line,
                            Character = character
                        },
                        File = file
                    }
                };
            },
            error =>
            {
                logger.LogError("MCP CompletionTool error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for completion error");
                }
                throw new InvalidOperationException($"Completion operation failed: {error.Message}", error.Exception);
            }
        );
    }
}
