using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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

        try
        {
            var request = new WindowLogRequest();
            var result = await service.GetWindowLogMessagesAsync(request, cancellationToken);

            logger.LogInformation("[WindowLogMessagesTool] Retrieved {Count} log messages",
                result.LogMessages.Count());

            return new
            {
                logMessages = result.LogMessages.Select(msg => new
                {
                    message = msg.Message,
                    messageType = msg.MessageType.ToString()
                })
                    .ToArray(),
                totalCount = result.LogMessages.Count()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WindowLogMessagesTool] Error getting window log messages");

            throw;
        }
    }
}
