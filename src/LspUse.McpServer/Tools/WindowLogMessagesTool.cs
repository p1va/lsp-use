using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class WindowLogMessagesTool
{
    [McpServerTool(Name = "get_window_log_messages",
        Title = "GetWindowLogMessages",
        UseStructuredContent = false
    )]
    [Description(
        "Retrieves LSP server status messages and logs, including high level messages like 'opened solution in 3s' to check LSP status"
    )]
    public static async Task<IEnumerable<TextContentBlock>> GetWindowLogMessagesAsync(
        IApplicationService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(WindowLogMessagesTool));

        logger.LogInformation("[WindowLogMessagesTool] Getting window log messages");

        var request = new WindowLogRequest();
        var result = await service.GetWindowLogMessagesAsync(request, cancellationToken);

        return result.Match<IEnumerable<TextContentBlock>>(success =>
            {
                var logMessages = success.LogMessages.ToList();

                if (!logMessages.Any())
                {
                    return [new TextContentBlock { Text = "No log messages available" }];
                }

                // Create summary
                var messageCounts = logMessages
                    .GroupBy(m => m.MessageType)
                    .ToDictionary(g => g.Key, g => g.Count());

                var totalMessages = logMessages.Count;
                var summaryParts = messageCounts
                    .OrderBy(kvp => (int)kvp.Key)  // Order by enum value
                    .Select(kvp => $"{kvp.Value} {kvp.Key.ToString().ToLower()}{(kvp.Value == 1 ? "" : "s")}")
                    .ToList();

                var summary = new TextContentBlock
                {
                    Text = $"Found {totalMessages} log {(totalMessages == 1 ? "message" : "messages")}: {string.Join(", ", summaryParts)}\n"
                };

                // Format individual messages
                var messageBlocks = logMessages.Select(msg => new TextContentBlock
                {
                    Text = FormatLogMessage(msg),
                });

                return [summary, .. messageBlocks];
            },
            error =>
            {
                logger.LogError("[WindowLogMessagesTool] Error: {Message} ({ErrorCode})",
                    error.Message,
                    error.ErrorCode
                );

                if (error.Exception != null)
                {
                    logger.LogError(error.Exception,
                        "Underlying exception for window log messages error"
                    );
                }

                throw new InvalidOperationException(
                    $"Window log messages operation failed: {error.Message}",
                    error.Exception
                );
            }
        );
    }

    private static string FormatLogMessage(WindowLogMessage msg)
    {
        var messageType = msg.MessageType.ToString().ToUpper();
        var formattedMessage = msg.Message.Trim();

        // Extract source/context information if available (format: [source] message)
        if (formattedMessage.StartsWith('[') && formattedMessage.Contains(']'))
        {
            var closingBracket = formattedMessage.IndexOf(']');
            if (closingBracket > 1)
            {
                var source = formattedMessage.Substring(1, closingBracket - 1);
                var content = formattedMessage.Substring(closingBracket + 1).Trim();
                return $"[{messageType}] {source}: {content}";
            }
        }

        return $"[{messageType}] {formattedMessage}";
    }
}
