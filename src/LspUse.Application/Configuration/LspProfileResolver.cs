namespace LspUse.Application.Configuration;

/// <summary>
/// Resolves LSP profiles from multiple sources with a defined priority order.
/// Handles built-in profiles (embedded in packages) and user-defined profiles.
/// </summary>
public class LspProfileResolver
{
    private readonly Dictionary<string, LspProfile> _builtInProfiles;
    private readonly Dictionary<string, LspProfile> _customProfiles;

    /// <summary>
    /// Creates a new resolver with built-in and custom profiles.
    /// </summary>
    /// <param name="builtInProfiles">Built-in profiles (embedded in package or hardcoded)</param>
    /// <param name="customProfiles">User-defined profiles (from YAML config)</param>
    public LspProfileResolver(
        Dictionary<string, LspProfile> builtInProfiles,
        Dictionary<string, LspProfile> customProfiles)
    {
        _builtInProfiles = builtInProfiles ?? new();
        _customProfiles = customProfiles ?? new();
    }

    /// <summary>
    /// Gets an LSP profile by name, with custom profiles taking priority over built-in profiles.
    /// </summary>
    /// <param name="lspName">The LSP identifier (e.g., "typescript", "pyright", "omnisharp")</param>
    /// <returns>The LSP profile if found, null otherwise</returns>
    public LspProfile? GetProfile(string lspName)
    {
        // 1. Check custom profiles first (allows overriding built-ins)
        if (_customProfiles.TryGetValue(lspName, out var customProfile))
            return customProfile;

        // 2. Fallback to built-in profiles
        if (_builtInProfiles.TryGetValue(lspName, out var builtInProfile))
            return builtInProfile;

        return null;
    }

    /// <summary>
    /// Gets all available LSP names from both built-in and custom profiles.
    /// </summary>
    /// <returns>All available LSP identifiers</returns>
    public IEnumerable<string> GetAvailableLsps()
    {
        return _customProfiles.Keys.Concat(_builtInProfiles.Keys).Distinct().OrderBy(x => x);
    }

    /// <summary>
    /// Attempts to auto-detect an LSP based on files present in the workspace.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace to scan</param>
    /// <returns>The detected LSP name if found, null otherwise</returns>
    public string? AutoDetectLsp(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
            return null;

        var allProfiles = _customProfiles.Concat(_builtInProfiles)
            .Where(kvp => kvp.Value.WorkspaceFiles != null ||
                         kvp.Value.Extensions != null ||
                         kvp.Value.LegacyExtensions != null);

        foreach (var (lspName, profile) in allProfiles)
        {
            // Check for workspace files first (more specific indicators)
            if (profile.WorkspaceFiles != null)
            {
                foreach (var workspaceFile in profile.WorkspaceFiles)
                {
                    var matchingFiles = Directory.GetFiles(workspacePath, workspaceFile, SearchOption.TopDirectoryOnly);
                    if (matchingFiles.Length > 0)
                        return lspName;
                }
            }

            // Check for file extensions (broader indicators)
            var supportedExtensions = profile.GetAllExtensions();
            foreach (var extension in supportedExtensions)
            {
                var matchingFiles = Directory.GetFiles(workspacePath, $"*{extension}", SearchOption.AllDirectories);
                if (matchingFiles.Length > 0)
                    return lspName;
            }
        }

        return null;
    }
}
