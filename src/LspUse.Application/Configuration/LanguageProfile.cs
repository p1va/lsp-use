using System.Text;

namespace LspUse.Application.Configuration;

/// <summary>
/// Represents a language server configuration profile that can be loaded from YAML
/// and used to configure language server processes for different programming languages.
/// </summary>
public record LanguageProfile
{
    /// <summary>
    /// The command line to execute for this language server.
    /// This should include the executable and all arguments as a single string.
    /// Example: "typescript-language-server --stdio --log-level 4"
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// File extensions associated with this language (used for auto-detection).
    /// Example: [".ts", ".tsx", ".js", ".jsx"]
    /// </summary>
    public string[]? Extensions { get; init; }

    /// <summary>
    /// Workspace files that indicate this language (used for auto-detection).
    /// Example: ["package.json", "tsconfig.json"]
    /// </summary>
    public string[]? WorkspaceFiles { get; init; }

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