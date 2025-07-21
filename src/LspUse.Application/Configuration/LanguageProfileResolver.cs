namespace LspUse.Application.Configuration;

/// <summary>
/// Resolves language profiles from multiple sources with a defined priority order.
/// Handles built-in profiles (embedded in packages) and user-defined profiles.
/// </summary>
public class LanguageProfileResolver
{
    private readonly Dictionary<string, LanguageProfile> _builtInProfiles;
    private readonly Dictionary<string, LanguageProfile> _customProfiles;

    /// <summary>
    /// Creates a new resolver with built-in and custom profiles.
    /// </summary>
    /// <param name="builtInProfiles">Built-in profiles (embedded in package or hardcoded)</param>
    /// <param name="customProfiles">User-defined profiles (from YAML config)</param>
    public LanguageProfileResolver(
        Dictionary<string, LanguageProfile> builtInProfiles,
        Dictionary<string, LanguageProfile> customProfiles)
    {
        _builtInProfiles = builtInProfiles ?? new();
        _customProfiles = customProfiles ?? new();
    }

    /// <summary>
    /// Gets a language profile by name, with custom profiles taking priority over built-in profiles.
    /// </summary>
    /// <param name="languageName">The language identifier (e.g., "typescript", "custom-python")</param>
    /// <returns>The language profile if found, null otherwise</returns>
    public LanguageProfile? GetProfile(string languageName)
    {
        // 1. Check custom profiles first (allows overriding built-ins)
        if (_customProfiles.TryGetValue(languageName, out var customProfile))
            return customProfile;

        // 2. Fallback to built-in profiles
        if (_builtInProfiles.TryGetValue(languageName, out var builtInProfile))
            return builtInProfile;

        return null;
    }

    /// <summary>
    /// Gets all available language names from both built-in and custom profiles.
    /// </summary>
    /// <returns>All available language identifiers</returns>
    public IEnumerable<string> GetAvailableLanguages()
    {
        return _customProfiles.Keys.Concat(_builtInProfiles.Keys).Distinct().OrderBy(x => x);
    }

    /// <summary>
    /// Attempts to auto-detect a language based on files present in the workspace.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace to scan</param>
    /// <returns>The detected language name if found, null otherwise</returns>
    public string? AutoDetectLanguage(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
            return null;

        var allProfiles = _customProfiles.Concat(_builtInProfiles)
            .Where(kvp => kvp.Value.WorkspaceFiles != null || kvp.Value.Extensions != null);

        foreach (var (languageName, profile) in allProfiles)
        {
            // Check for workspace files first (more specific indicators)
            if (profile.WorkspaceFiles != null)
            {
                foreach (var workspaceFile in profile.WorkspaceFiles)
                {
                    var matchingFiles = Directory.GetFiles(workspacePath, workspaceFile, SearchOption.TopDirectoryOnly);
                    if (matchingFiles.Length > 0)
                        return languageName;
                }
            }

            // Check for file extensions (broader indicators)
            if (profile.Extensions != null)
            {
                foreach (var extension in profile.Extensions)
                {
                    var matchingFiles = Directory.GetFiles(workspacePath, $"*{extension}", SearchOption.AllDirectories);
                    if (matchingFiles.Length > 0)
                        return languageName;
                }
            }
        }

        return null;
    }
}