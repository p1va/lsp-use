namespace LspUse.LanguageServerClient;

/// <summary>
/// Marker interface implemented by classes that expose JSON-RPC methods to
/// handle Language-Server notifications (e.g. <c>window/logMessage</c>,
/// <c>textDocument/publishDiagnostics</c>).  Implementations live in
/// <c>LspUse.Client.Handlers</c> and are wired into <see
/// cref="StreamJsonRpc.JsonRpc"/> by back-end classes.
/// </summary>
public interface IRpcLocalTarget { }
