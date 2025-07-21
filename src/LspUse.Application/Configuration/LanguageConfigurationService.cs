using Microsoft.Extensions.Logging;

namespace LspUse.Application.Configuration;

/// <summary>
/// Service responsible for orchestrating language server configuration loading
/// and providing configured LanguageProfileResolver instances.
/// This service contains pure business logic and is agnostic to the storage mechanism.
/// </summary>
public class LanguageConfigurationService
{
    private readonly ILanguageConfigurationLoader _configurationLoader;
    private readonly ILogger<LanguageConfigurationService> _logger;

    public LanguageConfigurationService(
        ILanguageConfigurationLoader configurationLoader,
        ILogger<LanguageConfigurationService> logger)
    {
        _configurationLoader = configurationLoader;
        _logger = logger;
    }

    /// <summary>
    /// Creates a language profile resolver with the complete configuration hierarchy.
    /// Priority order: Custom Profiles > Package Defaults > Built-in Profiles
    /// </summary>
    /// <returns>A configured LanguageProfileResolver</returns>
    public async Task<LanguageProfileResolver> CreateResolverAsync()
    {
        // Load all configuration sources
        var builtInProfiles = _configurationLoader.GetBuiltInProfiles();
        var packageDefaults = await _configurationLoader.LoadPackageDefaultsAsync();
        var customProfiles = await _configurationLoader.LoadCustomProfilesAsync();

        _logger.LogDebug("Loaded {BuiltInCount} built-in, {PackageCount} package default, {CustomCount} custom language profiles", 
            builtInProfiles.Count, packageDefaults.Count, customProfiles.Count);

        // Apply configuration hierarchy: Built-in < Package Defaults < Custom
        var mergedProfiles = new Dictionary<string, LanguageProfile>(builtInProfiles);
        
        // Package defaults override built-ins
        foreach (var (key, profile) in packageDefaults)
        {
            mergedProfiles[key] = profile;
        }

        return new LanguageProfileResolver(mergedProfiles, customProfiles);
    }
}