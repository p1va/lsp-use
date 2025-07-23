namespace LspUse.Application.Configuration;

/// <summary>
/// Contract for loading LSP server configuration profiles from various sources.
/// This abstraction allows the Application layer to remain agnostic about the specific
/// storage mechanism (YAML files, JSON, database, etc.).
/// </summary>
public interface ILspConfigurationLoader
{
    /// <summary>
    /// Loads custom LSP profiles defined by the user.
    /// These profiles can override built-in profiles or define entirely new LSP configurations.
    /// </summary>
    /// <returns>Dictionary of custom LSP profiles, keyed by LSP name</returns>
    Task<Dictionary<string, LspProfile>> LoadCustomProfilesAsync();

    /// <summary>
    /// Gets the built-in LSP profiles that are embedded with the application.
    /// These serve as default configurations when no custom profiles are provided.
    /// </summary>
    /// <returns>Dictionary of built-in LSP profiles, keyed by LSP name</returns>
    Dictionary<string, LspProfile> GetBuiltInProfiles();

    /// <summary>
    /// Loads package-specific default profiles (e.g., from embedded resources in LSP-specific packages).
    /// This allows different package variants to include their own default configurations.
    /// </summary>
    /// <returns>Dictionary of package default profiles, keyed by LSP name</returns>
    Task<Dictionary<string, LspProfile>> LoadPackageDefaultsAsync();
}
