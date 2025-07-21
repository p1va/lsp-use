using LspUse.Application.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LspUse.McpServer.Infrastructure;

/// <summary>
/// Infrastructure implementation that loads language configurations from YAML files.
/// This class contains all the YAML-specific and file system logic, keeping the
/// Application layer clean and focused on business logic.
/// </summary>
public class YamlLanguageConfigurationLoader : ILanguageConfigurationLoader
{
    private readonly ILogger<YamlLanguageConfigurationLoader> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public YamlLanguageConfigurationLoader(ILogger<YamlLanguageConfigurationLoader> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, LanguageProfile>> LoadCustomProfilesAsync()
    {
        var userConfigPath = GetUserConfigPath();
        
        if (!File.Exists(userConfigPath))
        {
            _logger.LogDebug("User configuration file not found at {Path}", userConfigPath);
            return new Dictionary<string, LanguageProfile>();
        }

        try
        {
            var yamlContent = await File.ReadAllTextAsync(userConfigPath);
            var userConfig = _yamlDeserializer.Deserialize<LanguageConfig>(yamlContent);
            
            if (userConfig?.Languages == null)
            {
                _logger.LogWarning("User configuration file exists but contains no languages section");
                return new Dictionary<string, LanguageProfile>();
            }

            _logger.LogInformation("Loaded user configuration from {Path}", userConfigPath);
            return userConfig.Languages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user configuration from {Path}", userConfigPath);
            return new Dictionary<string, LanguageProfile>();
        }
    }

    /// <inheritdoc />
    public Dictionary<string, LanguageProfile> GetBuiltInProfiles()
    {
        // Built-in profiles that are always available
        return new Dictionary<string, LanguageProfile>
        {
            ["csharp"] = new()
            {
                Command = "Microsoft.CodeAnalysis.LanguageServer --logLevel=Information --stdio",
                Extensions = [".cs", ".csproj", ".sln"],
                WorkspaceFiles = ["*.sln", "*.csproj"]
            }
        };
    }

    /// <inheritdoc />
    public Task<Dictionary<string, LanguageProfile>> LoadPackageDefaultsAsync()
    {
        // In the future, this will load embedded YAML resources from specific language packages
        // For now, return empty - package defaults will be added when we create LspUse.Csharp, LspUse.Typescript packages
        try
        {
            var packageDefaultsYaml = GetPackageDefaultsYaml();
            if (string.IsNullOrWhiteSpace(packageDefaultsYaml))
            {
                return Task.FromResult(new Dictionary<string, LanguageProfile>());
            }

            var packageConfig = _yamlDeserializer.Deserialize<LanguageConfig>(packageDefaultsYaml);
            if (packageConfig?.Languages != null)
            {
                _logger.LogDebug("Loaded {Count} package default profiles", packageConfig.Languages.Count);
                return Task.FromResult(packageConfig.Languages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse package default configuration, skipping");
        }

        return Task.FromResult(new Dictionary<string, LanguageProfile>());
    }

    /// <summary>
    /// Gets the standard user configuration file path.
    /// </summary>
    private static string GetUserConfigPath()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "lsp-use");
        
        return Path.Combine(configDir, "languages.yaml");
    }

    /// <summary>
    /// Gets package-specific default YAML configuration.
    /// This will be overridden in language-specific packages to load embedded resources.
    /// </summary>
    protected virtual string? GetPackageDefaultsYaml()
    {
        // Base implementation returns null - language-specific packages can override this
        // to return embedded YAML resources
        return null;
    }

    /// <summary>
    /// Creates the user configuration directory if it doesn't exist.
    /// </summary>
    public static void EnsureUserConfigDirectoryExists()
    {
        var configDir = Path.GetDirectoryName(GetUserConfigPath())!;
        Directory.CreateDirectory(configDir);
    }
}