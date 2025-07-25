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
public static class HoverTool
{
    [McpServerTool(Name = "hover", Title = "Hover", UseStructuredContent = false)]
    [Description(
        "Gets hover information for a symbol at a specific position in a file. Returns rich information including type signatures, documentation, and other symbol details.")]
    public static async Task<IEnumerable<TextContentBlock>> HoverAsync(IApplicationService service,
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

        return result.Match<IEnumerable<TextContentBlock>>(
            success =>
            {
                logger.LogInformation("MCP HoverTool returning hover content: {HasContent}, Symbol: {SymbolName}",
                    success.Value != null, success.Symbol?.Name ?? "None");
                return BuildHoverResultBlocks(success, file, line, character);
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

    private static IEnumerable<TextContentBlock> BuildHoverResultBlocks(HoverSuccess success, string file, uint line, uint character)
    {
        var blocks = new List<TextContentBlock>();

        // First block: Symbol information (if available)
        if (success.Symbol != null)
        {
            var symbolInfo = new StringBuilder();
            
            // Format: Name (Kind) @line:character or @line:start-end if range spans
            var location = success.Symbol.Location;
            var positionInfo = "";
            
            if (location != null)
            {
                var startLine = location.StartLine ?? 0;
                var startChar = location.StartCharacter ?? 0;
                var endLine = location.EndLine ?? 0;
                var endChar = location.EndCharacter ?? 0;
                
                // Show range if it spans multiple characters or lines
                if (startLine != endLine || startChar != endChar)
                {
                    if (startLine == endLine)
                    {
                        positionInfo = $"@{startLine}:{startChar}-{endChar}";
                    }
                    else
                    {
                        positionInfo = $"@{startLine}:{startChar}-{endLine}:{endChar}";
                    }
                }
                else
                {
                    positionInfo = $"@{startLine}:{startChar}";
                }
            }
            else
            {
                positionInfo = $"@{line}:{character}";
            }
            
            symbolInfo.AppendLine($"{success.Symbol.Name} ({success.Symbol.Kind}) {positionInfo}");
            
            if (!string.IsNullOrEmpty(success.Symbol.ContainerName))
            {
                symbolInfo.AppendLine($"Container: {success.Symbol.ContainerName}");
            }

            blocks.Add(new TextContentBlock { Text = symbolInfo.ToString().TrimEnd() });
        }

        // Second block: Hover content (if available)
        if (!string.IsNullOrEmpty(success.Value))
        {
            // Clean up the hover content - replace \n with actual newlines and handle markdown
            var hoverContent = success.Value
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            blocks.Add(new TextContentBlock { Text = hoverContent });
        }

        // If neither symbol nor hover content is available, return a default message
        if (!blocks.Any())
        {
            blocks.Add(new TextContentBlock { Text = $"No hover information available for position {GetRelativeFilePath(file)}:{line}:{character}" });
        }

        return blocks;
    }
}
