using System.CommandLine;
using System.CommandLine.Help;

namespace LspUse.McpServer;

/// <summary>
/// A custom RootCommand that allows overriding the executable name shown in usage.
/// This is simplified version that just provides the essential RootCommand functionality.
/// </summary>
public class CustomRootCommand : Command
{
    /// <summary>
    /// Creates a new CustomRootCommand with a custom executable name for usage display.
    /// </summary>
    /// <param name="executableName">The name to show in usage (e.g., "lsp-use")</param>
    /// <param name="description">The description of the command, shown in help</param>
    public CustomRootCommand(string executableName, string description = "")
        : base(executableName, description)
    {
        // Add the same options that RootCommand adds by default
        Options.Add(new HelpOption());
        Options.Add(new VersionOption());
    }
}
