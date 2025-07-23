using System.Collections.Concurrent;
using System.Dynamic;

namespace LspUse.LanguageServerClient.Handlers;

/// <summary>
/// Records information about an unhandled notification method call.
/// </summary>
public record UnhandledNotification
{
    public string MethodName { get; init; } = string.Empty;
    public int ArgumentCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public object?[]? Arguments { get; init; }
}

/// <summary>
/// Default notification handler that uses DynamicObject to catch all unhandled LSP notifications.
/// This class implements TryInvokeMember to intercept any method calls that don't have specific handlers.
/// It can be registered with StreamJsonRpc using AddLocalRpcTarget to act as a catch-all.
/// </summary>
public sealed class DefaultNotificationHandler : DynamicObject, ILspNotificationHandler
{
    /// <summary>
    /// Gets a thread-safe collection of all unhandled notifications that have been caught.
    /// Useful for debugging and monitoring which LSP notifications are not being handled.
    /// </summary>
    public ConcurrentQueue<UnhandledNotification> UnhandledNotifications { get; } = new();

    /// <summary>
    /// Overrides TryInvokeMember to catch all method calls that don't have explicit handlers.
    /// This acts as a catch-all for any LSP notifications that aren't handled by specific handlers.
    /// </summary>
    /// <param name="binder">Provides information about the dynamic operation</param>
    /// <param name="args">The arguments passed to the method</param>
    /// <param name="result">The result of the method invocation</param>
    /// <returns>Always returns true to indicate the method was handled</returns>
    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        var methodName = binder.Name;

        // Record the unhandled notification for debugging purposes
        var notification = new UnhandledNotification
        {
            MethodName = methodName,
            ArgumentCount = args?.Length ?? 0,
            Arguments = args
        };

        UnhandledNotifications.Enqueue(notification);

        // Use Debug.WriteLine for simple logging without external dependencies
        System.Diagnostics.Debug.WriteLine($"[DefaultNotificationHandler] Caught unhandled method: {methodName} with {args?.Length ?? 0} arguments");

        // Return null for void methods (notifications typically don't return values)
        result = null;
        return true; // Indicate that the method call was handled
    }
}
