namespace LspUse.Application.Models;

public enum ErrorCode
{
    Unknown = 0,

    /// <summary>
    /// The requested operation requires the workspace to be fully loaded but
    /// it is still in the process of loading. Clients should wait and retry
    /// once the <c>workspace/projectInitializationComplete</c> notification is
    /// received.
    /// </summary>
    WorkspaceLoadInProgress = 1
}
