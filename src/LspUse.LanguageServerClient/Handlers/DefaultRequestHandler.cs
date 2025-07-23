using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json.Nodes;

namespace LspUse.LanguageServerClient.Handlers;

/// <summary>
/// Records information about an unhandled request method call from the server.
/// </summary>
public record UnhandledRequest
{
    public string MethodName { get; init; } = string.Empty;
    public int ArgumentCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public object?[]? Arguments { get; init; }
    public object? Response { get; init; }
}

/// <summary>
/// Default request handler that uses DynamicObject to catch all unhandled LSP server-to-client requests.
/// This class implements TryInvokeMember to intercept request method calls that don't have specific handlers
/// and provides appropriate default responses to prevent JSON-RPC "method not found" errors.
/// </summary>
public sealed class DefaultRequestHandler : DynamicObject, ILspNotificationHandler
{
    /// <summary>
    /// Gets a thread-safe collection of all unhandled requests that have been caught.
    /// Useful for debugging and monitoring which LSP requests are not being handled.
    /// </summary>
    public ConcurrentQueue<UnhandledRequest> UnhandledRequests { get; } = new();

    /// <summary>
    /// Overrides TryInvokeMember to catch all request method calls that don't have explicit handlers.
    /// This acts as a catch-all for any LSP server-to-client requests that aren't handled by specific handlers.
    /// Returns appropriate default responses based on the request type.
    /// </summary>
    /// <param name="binder">Provides information about the dynamic operation</param>
    /// <param name="args">The arguments passed to the method</param>
    /// <param name="result">The result of the method invocation</param>
    /// <returns>Always returns true to indicate the method was handled</returns>
    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        var methodName = binder.Name;

        // Provide default responses for common LSP server-to-client requests
        result = methodName switch
        {
            // workspace/configuration - return empty configuration
            "workspace/configuration" => JsonNode.Parse("[]"),

            // window/showMessageRequest - return null (no action taken)
            "window/showMessageRequest" => null,

            // workspace/applyEdit - return success with no changes applied
            "workspace/applyEdit" => JsonNode.Parse("""{"applied": false, "failureReason": "Not implemented"}"""),

            // client/registerCapability - return empty object (success)
            "client/registerCapability" => JsonNode.Parse("{}"),

            // client/unregisterCapability - return empty object (success)  
            "client/unregisterCapability" => JsonNode.Parse("{}"),

            // Default for unknown requests - return null
            _ => null
        };

        // Record the unhandled request for debugging purposes
        var request = new UnhandledRequest
        {
            MethodName = methodName,
            ArgumentCount = args?.Length ?? 0,
            Arguments = args,
            Response = result
        };

        UnhandledRequests.Enqueue(request);

        // Use Debug.WriteLine for simple logging without external dependencies
        System.Diagnostics.Debug.WriteLine($"[DefaultRequestHandler] Handled unhandled request: {methodName} with {args?.Length ?? 0} arguments, returned: {result}");

        return true; // Indicate that the request was handled
    }
}
