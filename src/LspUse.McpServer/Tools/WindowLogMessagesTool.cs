using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class WindowLogMessagesTool
{
    [McpServerTool(Name = "get_window_log_messages", Title = "GetWindowLogMessages",
        UseStructuredContent = true)]
    [Description(
        "Retrieves LSP server status messages and logs, including high level messages like 'opened solution in 3s' to check LSP status")]
    public static async Task<object> GetWindowLogMessagesAsync(IApplicationService service,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(WindowLogMessagesTool));

        logger.LogInformation("[WindowLogMessagesTool] Getting window log messages");

        var request = new WindowLogRequest();
        var result = await service.GetWindowLogMessagesAsync(request, cancellationToken);

        return result.Match(
            success =>
            {
                logger.LogInformation("[WindowLogMessagesTool] Retrieved {Count} log messages",
                    success.LogMessages.Count());

                return new
                {
                    logMessages = success.LogMessages.Select(msg => new
                    {
                        message = msg.Message,
                        messageType = msg.MessageType.ToString()
                    })
                        .ToArray(),
                    totalCount = success.LogMessages.Count()
                };
            },
            error =>
            {
                logger.LogError("[WindowLogMessagesTool] Error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for window log messages error");
                }
                throw new InvalidOperationException($"Window log messages operation failed: {error.Message}", error.Exception);
            }
        );
    }
}
