using Microsoft.Extensions.Logging;

namespace LspUse.Application.Configuration;

/// <summary>
/// Service responsible for orchestrating LSP server configuration loading
/// and providing configured LspProfileResolver instances.
/// This service contains pure business logic and is agnostic to the storage mechanism.
/// </summary>
public class LspConfigurationService
{
    private readonly ILspConfigurationLoader _configurationLoader;
    private readonly ILogger<LspConfigurationService> _logger;

    public LspConfigurationService(
        ILspConfigurationLoader configurationLoader,
        ILogger<LspConfigurationService> logger)
    {
        _configurationLoader = configurationLoader;
        _logger = logger;
    }

    /// <summary>
    /// Creates an LSP profile resolver with the complete configuration hierarchy.
    /// Priority order: Custom Profiles > Package Defaults > Built-in Profiles
    /// </summary>
    /// <returns>A configured LspProfileResolver</returns>
    public async Task<LspProfileResolver> CreateResolverAsync()
    {
        // Load all configuration sources
        var builtInProfiles = _configurationLoader.GetBuiltInProfiles();
        var packageDefaults = await _configurationLoader.LoadPackageDefaultsAsync();
        var customProfiles = await _configurationLoader.LoadCustomProfilesAsync();

        _logger.LogDebug("Loaded {BuiltInCount} built-in, {PackageCount} package default, {CustomCount} custom LSP profiles",
            builtInProfiles.Count, packageDefaults.Count, customProfiles.Count);

        // Apply configuration hierarchy: Built-in < Package Defaults < Custom
        var mergedProfiles = new Dictionary<string, LspProfile>(builtInProfiles);

        // Package defaults override built-ins
        foreach (var (key, profile) in packageDefaults)
        {
            mergedProfiles[key] = profile;
        }

        return new LspProfileResolver(mergedProfiles, customProfiles);
    }
}
