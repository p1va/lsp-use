using System.Text;

namespace LspUse.Application.Configuration;

/// <summary>
/// Represents an LSP server configuration profile that can be loaded from YAML
/// and used to configure LSP server processes for different development scenarios.
/// </summary>
public record LspProfile
{
    /// <summary>
    /// The command line to execute for this LSP server.
    /// This should include the executable and all arguments as a single string.
    /// Example: "typescript-language-server --stdio --log-level 4"
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Maps file extensions to their corresponding LSP language identifiers.
    /// This allows one LSP server to handle multiple file types with different language IDs.
    /// Example: { ".js": "javascript", ".jsx": "javascriptreact", ".ts": "typescript", ".tsx": "typescriptreact" }
    /// </summary>
    public Dictionary<string, string>? Extensions { get; init; }

    /// <summary>
    /// DEPRECATED: Use Extensions dictionary instead. 
    /// Legacy support for simple extension arrays - will use the profile name as language ID.
    /// </summary>
    public string[]? LegacyExtensions { get; init; }

    /// <summary>
    /// DEPRECATED: Use Extensions dictionary instead.
    /// Legacy single language ID - only used if Extensions dictionary is not provided.
    /// </summary>
    public string? LanguageId { get; init; }

    /// <summary>
    /// Workspace files that indicate this LSP server (used for auto-detection).
    /// Example: ["package.json", "tsconfig.json"]
    /// </summary>
    public string[]? WorkspaceFiles { get; init; }

    /// <summary>
    /// Diagnostic settings for this LSP profile.
    /// Controls how diagnostics are obtained from the LSP server.
    /// </summary>
    public DiagnosticsSettings? Diagnostics { get; init; }

    /// <summary>
    /// Gets the LSP language identifier for a specific file extension.
    /// </summary>
    /// <param name="extension">The file extension (e.g., ".ts", ".jsx")</param>
    /// <param name="profileName">The profile name to use as fallback language ID</param>
    /// <returns>The LSP language identifier, or null if extension is not supported</returns>
    public string? GetLanguageIdForExtension(string extension, string profileName)
    {
        // New format: extension-to-language-id dictionary
        if (Extensions != null && Extensions.TryGetValue(extension, out var languageId))
        {
            return languageId;
        }

        // Legacy format: simple extension array with single language ID
        if (LegacyExtensions != null && LegacyExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return LanguageId ?? profileName;
        }

        return null;
    }

    /// <summary>
    /// Gets all supported file extensions from this profile.
    /// </summary>
    /// <returns>All extensions supported by this profile</returns>
    public IEnumerable<string> GetAllExtensions()
    {
        var extensions = new List<string>();

        // New format
        if (Extensions != null)
        {
            extensions.AddRange(Extensions.Keys);
        }

        // Legacy format  
        if (LegacyExtensions != null)
        {
            extensions.AddRange(LegacyExtensions);
        }

        return extensions.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this profile supports the given file extension.
    /// </summary>
    /// <param name="extension">The file extension to check</param>
    /// <returns>True if the extension is supported, false otherwise</returns>
    public bool SupportsExtension(string extension)
    {
        if (Extensions != null && Extensions.ContainsKey(extension))
            return true;

        if (LegacyExtensions != null && LegacyExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Parses the unified command string into separate command and arguments.
    /// Uses a simple but effective parsing logic to handle quoted arguments.
    /// </summary>
    /// <returns>A tuple containing the command executable and its arguments.</returns>
    public (string Command, string[] Arguments) GetCommandAndArgs()
    {
        if (string.IsNullOrWhiteSpace(Command))
            throw new InvalidOperationException("Command cannot be null or empty");

        var tokens = ParseCommandLine(Command);
        if (tokens.Length == 0)
            throw new InvalidOperationException($"Invalid command string: '{Command}'");

        return (tokens[0], tokens.Skip(1).ToArray());
    }

    /// <summary>
    /// Simple command line parser that handles quoted arguments.
    /// </summary>
    private static string[] ParseCommandLine(string commandLine)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        bool inQuotes = false;
        bool escapeNext = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];

            if (escapeNext)
            {
                currentToken.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                continue;
            }

            currentToken.Append(c);
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens.ToArray();
    }
}

/// <summary>
/// Diagnostic strategy configuration for LSP servers.
/// Controls how diagnostics are obtained from the LSP server.
/// </summary>
public record DiagnosticsSettings
{
    /// <summary>
    /// The diagnostic strategy to use.
    /// - "pull": Request diagnostics on-demand (like C# language server)
    /// - "push": Server sends diagnostics via notifications (like TypeScript)
    /// </summary>
    public DiagnosticStrategy Strategy { get; init; } = DiagnosticStrategy.Pull;

    /// <summary>
    /// For push strategy: Maximum time to wait for diagnostics after file operations (in milliseconds).
    /// Default is 3000ms (3 seconds).
    /// </summary>
    public int WaitTimeoutMs { get; init; } = 3000;

    /// <summary>
    /// Gets the default diagnostics settings for the pull strategy.
    /// </summary>
    public static DiagnosticsSettings PullDefaults => new()
    {
        Strategy = DiagnosticStrategy.Pull
    };

    /// <summary>
    /// Gets the default diagnostics settings for the push strategy.
    /// </summary>
    public static DiagnosticsSettings PushDefaults => new()
    {
        Strategy = DiagnosticStrategy.Push,
        WaitTimeoutMs = 3000
    };
}

/// <summary>
/// Available diagnostic strategies for LSP servers.
/// </summary>
public enum DiagnosticStrategy
{
    /// <summary>
    /// Pull diagnostics: Request diagnostics on-demand using textDocument/diagnostic.
    /// Used by language servers like C# that implement pull diagnostics.
    /// </summary>
    Pull,

    /// <summary>
    /// Push diagnostics: Server sends diagnostics via textDocument/publishDiagnostics notifications.
    /// Used by language servers like TypeScript that push diagnostics automatically.
    /// </summary>
    Push
}