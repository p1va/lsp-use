using System.Collections.Concurrent;
using LspUse.LanguageServerClient.Models;
using StreamJsonRpc;

namespace LspUse.LanguageServerClient.Handlers;

/// <summary>
/// Handles <c>client/registerCapability</c> requests coming from the language server.
/// Registrations are stored in a thread-safe <see cref="ConcurrentQueue{T}"/> for later
/// consumption by the rest of the application or for test assertions.
/// </summary>
public sealed class ClientCapabilityRegistrationHandler : ILspNotificationHandler
{
    public Task RegistrationCompleted => _registrationCompletedSource.Task;

    public ConcurrentQueue<Registration> Registrations { get; } = new();

    private readonly TaskCompletionSource _registrationCompletedSource = new();

    /// <summary>
    /// Receives a <c>client/registerCapability</c> request.
    /// The method name must match the JSON-RPC method name that Roslyn sends.
    /// </summary>
    /// <param name="parameters">The parameters of the capability registration.</param>
    [JsonRpcMethod("client/registerCapability", UseSingleObjectParameterDeserialization = true)]
    public void OnRegisterCapability(ClientCapabilityRegistrationParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var registration in parameters.Registrations)
            Registrations.Enqueue(registration);

        _registrationCompletedSource.TrySetResult();
    }
}
