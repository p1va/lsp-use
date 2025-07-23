using System.ComponentModel;
using LspUse.Application;
using LspUse.LanguageServerClient.Handlers;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LspUse.McpServer.Tools.Experimental;

/// <summary>
/// Result containing unhandled notifications information.
/// </summary>
public record GetUnhandledNotificationsResult
{
    public UnhandledNotificationInfo[] UnhandledNotifications { get; init; } = [];
    public int TotalCount { get; init; }
}

/// <summary>
/// Information about an unhandled notification for MCP response.
/// </summary>
public record UnhandledNotificationInfo
{
    public string MethodName { get; init; } = string.Empty;
    public int ArgumentCount { get; init; }
    public DateTime Timestamp { get; init; }
    public string[] ArgumentTypes { get; init; } = [];
}

/// <summary>
/// MCP tool to retrieve information about unhandled LSP notifications caught by the DefaultNotificationHandler.
/// This is useful for debugging and monitoring which LSP notifications are being sent but not specifically handled.
/// </summary>
[McpServerToolType]
public static class GetUnhandledNotificationsTool
{
    private const string ToolName = "get_unhandled_notifications";
    private const string ToolTitle = "GetUnhandledNotifications";
    private const string ToolDescription = "Retrieves unhandled LSP notifications caught by the catch-all handler";
    private const string ToolArgDescLimit = "Maximum number of notifications to return (default: 50)";

    /// <summary>
    /// Gets all unhandled notifications that have been caught by the DefaultNotificationHandler.
    /// </summary>
    /// <param name="service">The application service</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="limit">Maximum number of notifications to return (default: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of unhandled notifications with details</returns>
    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = true)]
    [Description(ToolDescription)]
    public static Task<GetUnhandledNotificationsResult> GetUnhandledNotificationsAsync(
        IApplicationService service,
        ILoggerFactory loggerFactory,
        [Description(ToolArgDescLimit)] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(ToolName);

        logger.LogInformation("[GetUnhandledNotifications] Retrieving unhandled notifications with limit: {Limit}", limit);

        try
        {
            // Get the DefaultNotificationHandler from the application service
            var defaultHandler = service.GetDefaultNotificationHandler();

            if (defaultHandler == null)
            {
                logger.LogWarning("[GetUnhandledNotifications] DefaultNotificationHandler not found");
                return Task.FromResult(new GetUnhandledNotificationsResult
                {
                    UnhandledNotifications = [],
                    TotalCount = 0
                });
            }

            var notifications = new List<UnhandledNotification>();
            var totalCount = defaultHandler.UnhandledNotifications.Count;

            // Dequeue up to the limit
            var taken = 0;
            while (taken < limit && defaultHandler.UnhandledNotifications.TryDequeue(out var notification))
            {
                notifications.Add(notification);
                taken++;
            }

            var result = new GetUnhandledNotificationsResult
            {
                UnhandledNotifications = notifications.Select(n => new UnhandledNotificationInfo
                {
                    MethodName = n.MethodName,
                    ArgumentCount = n.ArgumentCount,
                    Timestamp = n.Timestamp,
                    ArgumentTypes = n.Arguments?.Select(arg => arg?.GetType().Name ?? "null").ToArray() ?? []
                }).ToArray(),
                TotalCount = totalCount
            };

            logger.LogInformation("[GetUnhandledNotifications] Retrieved {Count} unhandled notifications", result.UnhandledNotifications.Length);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GetUnhandledNotifications] Error retrieving unhandled notifications");
            throw;
        }
    }
}
