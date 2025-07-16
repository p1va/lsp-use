using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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

        try
        {
            var result = await service.CompletionAsync(new CompletionRequest
            {
                FilePath = file,
                Position = new EditorPosition
                {
                    Line = line,
                    Character = character
                }
            }, cancellationToken);

            logger.LogInformation("MCP CompletionTool returning {Count} completion items",
                result.Items.Count());

            return new
            {
                Items = result.Items.Select(item => new
                {
                    item.Label,
                    Kind = item.Kind?.ToString(),
                    KindValue = (int?)item.Kind
                })
                    .ToArray(),
                Metadata = new
                {
                    result.IsIncomplete,
                    ItemCount = result.Items.Count(),
                    Position = new
                    {
                        Line = line,
                        Character = character
                    },
                    File = file
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MCP CompletionTool for {FilePath} at {Line}:{Character}",
                file, line, character);

            throw;
        }
    }
}
