namespace LspUse.Application.Configuration;

/// <summary>
/// Contract for loading language server configuration profiles from various sources.
/// This abstraction allows the Application layer to remain agnostic about the specific
/// storage mechanism (YAML files, JSON, database, etc.).
/// </summary>
public interface ILanguageConfigurationLoader
{
    /// <summary>
    /// Loads custom language profiles defined by the user.
    /// These profiles can override built-in profiles or define entirely new languages.
    /// </summary>
    /// <returns>Dictionary of custom language profiles, keyed by language name</returns>
    Task<Dictionary<string, LanguageProfile>> LoadCustomProfilesAsync();

    /// <summary>
    /// Gets the built-in language profiles that are embedded with the application.
    /// These serve as default configurations when no custom profiles are provided.
    /// </summary>
    /// <returns>Dictionary of built-in language profiles, keyed by language name</returns>
    Dictionary<string, LanguageProfile> GetBuiltInProfiles();

    /// <summary>
    /// Loads package-specific default profiles (e.g., from embedded resources in language-specific packages).
    /// This allows different package variants to include their own default configurations.
    /// </summary>
    /// <returns>Dictionary of package default profiles, keyed by language name</returns>
    Task<Dictionary<string, LanguageProfile>> LoadPackageDefaultsAsync();
}