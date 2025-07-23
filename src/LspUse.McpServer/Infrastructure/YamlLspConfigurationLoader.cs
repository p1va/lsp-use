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
public class YamlLspConfigurationLoader : ILspConfigurationLoader
{
    private readonly ILogger<YamlLspConfigurationLoader> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public YamlLspConfigurationLoader(ILogger<YamlLspConfigurationLoader> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, LspProfile>> LoadCustomProfilesAsync()
    {
        var userConfigPath = GetUserConfigPath();
        
        if (!File.Exists(userConfigPath))
        {
            _logger.LogDebug("User configuration file not found at {Path}", userConfigPath);
            return new Dictionary<string, LspProfile>();
        }

        try
        {
            var yamlContent = await File.ReadAllTextAsync(userConfigPath);
            var userConfig = _yamlDeserializer.Deserialize<LspConfig>(yamlContent);
            
            if (userConfig?.Lsps == null)
            {
                _logger.LogWarning("User configuration file exists but contains no languages section");
                return new Dictionary<string, LspProfile>();
            }

            _logger.LogInformation("Loaded user configuration from {Path}", userConfigPath);
            return userConfig.Lsps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user configuration from {Path}", userConfigPath);
            return new Dictionary<string, LspProfile>();
        }
    }

    /// <inheritdoc />
    public Dictionary<string, LspProfile> GetBuiltInProfiles()
    {
        // Built-in profiles that are always available
        return new Dictionary<string, LspProfile>
        {
            ["csharp"] = new()
            {
                Command = "Microsoft.CodeAnalysis.LanguageServer --logLevel=Information --stdio",
                Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { ".cs", "csharp" },
                    { ".csx", "csharp" },
                    { ".cake", "csharp" },
                    { ".cshtml", "razor" },
                    { ".razor", "razor" }
                },
                WorkspaceFiles = ["*.sln", "*.csproj", "global.json"],
                Diagnostics = DiagnosticsSettings.PullDefaults
            }
        };
    }

    /// <inheritdoc />
    public Task<Dictionary<string, LspProfile>> LoadPackageDefaultsAsync()
    {
        // In the future, this will load embedded YAML resources from specific language packages
        // For now, return empty - package defaults will be added when we create LspUse.Csharp, LspUse.Typescript packages
        try
        {
            var packageDefaultsYaml = GetPackageDefaultsYaml();
            if (string.IsNullOrWhiteSpace(packageDefaultsYaml))
            {
                return Task.FromResult(new Dictionary<string, LspProfile>());
            }

            var packageConfig = _yamlDeserializer.Deserialize<LspConfig>(packageDefaultsYaml);
            if (packageConfig?.Lsps != null)
            {
                _logger.LogDebug("Loaded {Count} package default profiles", packageConfig.Lsps.Count);
                return Task.FromResult(packageConfig.Lsps);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse package default configuration, skipping");
        }

        return Task.FromResult(new Dictionary<string, LspProfile>());
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
        
        return Path.Combine(configDir, "lsps.yaml");
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