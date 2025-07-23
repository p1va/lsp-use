using System.ComponentModel;
using LspUse.Application;
using LspUse.LanguageServerClient.Handlers;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LspUse.McpServer.Tools.Experimental;

/// <summary>
/// Result containing unhandled server-to-client requests information.
/// </summary>
public record GetUnhandledRequestsResult
{
    public UnhandledRequestInfo[] UnhandledRequests { get; init; } = [];
    public int TotalCount { get; init; }
}

/// <summary>
/// Information about an unhandled server-to-client request for MCP response.
/// </summary>
public record UnhandledRequestInfo
{
    public string MethodName { get; init; } = string.Empty;
    public int ArgumentCount { get; init; }
    public DateTime Timestamp { get; init; }
    public string[] ArgumentTypes { get; init; } = [];
    public string Response { get; init; } = string.Empty;
}

/// <summary>
/// MCP tool to retrieve information about unhandled LSP server-to-client requests caught by the DefaultRequestHandler.
/// This is useful for debugging and monitoring which LSP requests the server is sending that we're not specifically handling.
/// </summary>
[McpServerToolType]
public static class GetUnhandledRequestsTool
{
    private const string ToolName = "get_unhandled_requests";
    private const string ToolTitle = "GetUnhandledRequests";
    private const string ToolDescription = "Retrieves unhandled LSP server-to-client requests caught by the catch-all handler";
    private const string ToolArgDescLimit = "Maximum number of requests to return (default: 50)";

    /// <summary>
    /// Gets all unhandled server-to-client requests that have been caught by the DefaultRequestHandler.
    /// </summary>
    /// <param name="service">The application service</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="limit">Maximum number of requests to return (default: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of unhandled requests with details</returns>
    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = true)]
    [Description(ToolDescription)]
    public static Task<GetUnhandledRequestsResult> GetUnhandledRequestsAsync(
        IApplicationService service,
        ILoggerFactory loggerFactory,
        [Description(ToolArgDescLimit)] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(ToolName);

        logger.LogInformation("[GetUnhandledRequests] Retrieving unhandled requests with limit: {Limit}", limit);

        try
        {
            // Get the DefaultRequestHandler from the application service
            var defaultHandler = service.GetDefaultRequestHandler();

            if (defaultHandler == null)
            {
                logger.LogWarning("[GetUnhandledRequests] DefaultRequestHandler not found");
                return Task.FromResult(new GetUnhandledRequestsResult
                {
                    UnhandledRequests = [],
                    TotalCount = 0
                });
            }

            var requests = new List<UnhandledRequest>();
            var totalCount = defaultHandler.UnhandledRequests.Count;

            // Dequeue up to the limit
            var taken = 0;
            while (taken < limit && defaultHandler.UnhandledRequests.TryDequeue(out var request))
            {
                requests.Add(request);
                taken++;
            }

            var result = new GetUnhandledRequestsResult
            {
                UnhandledRequests = requests.Select(r => new UnhandledRequestInfo
                {
                    MethodName = r.MethodName,
                    ArgumentCount = r.ArgumentCount,
                    Timestamp = r.Timestamp,
                    ArgumentTypes = r.Arguments?.Select(arg => arg?.GetType().Name ?? "null").ToArray() ?? [],
                    Response = r.Response?.ToString() ?? "null"
                }).ToArray(),
                TotalCount = totalCount
            };

            logger.LogInformation("[GetUnhandledRequests] Retrieved {Count} unhandled requests", result.UnhandledRequests.Length);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GetUnhandledRequests] Error retrieving unhandled requests");
            throw;
        }
    }
}
