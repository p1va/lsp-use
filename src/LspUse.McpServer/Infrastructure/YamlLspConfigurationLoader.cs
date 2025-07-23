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
                _logger.LogWarning(
                    "User configuration file exists but contains no languages section"
                );

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
        var lspDirectory = Path.Combine(AppContext.BaseDirectory, "lsp");
        var languageServerDllPath = Path.Combine(lspDirectory,
            "Microsoft.CodeAnalysis.LanguageServer"
        );

        // Built-in profiles that are always available
        return new Dictionary<string, LspProfile>
        {
            ["csharp"] = new()
            {
                Command =
                    $"{languageServerDllPath} --logLevel=Information --extensionLogDirectory=logs --stdio",
                Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        ".cs", "csharp"
                    },
                    {
                        ".csx", "csharp"
                    },
                    {
                        ".cake", "csharp"
                    },
                    {
                        ".cshtml", "razor"
                    },
                    {
                        ".razor", "razor"
                    },
                },
                WorkspaceFiles = ["*.sln", "*.csproj", "global.json",],
                Diagnostics = DiagnosticsSettings.PullDefaults,
            },
            ["typescript"] = new()
            {
                Command = "typescript-language-server --stdio",
                Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        ".ts", "typescript"
                    },
                    {
                        ".tsx", "typescriptreact"
                    },
                    {
                        ".js", "javascript"
                    },
                    {
                        ".jsx", "javascriptreact"
                    },
                },
                WorkspaceFiles = ["package.json", "tsconfig.json",],
                Diagnostics = DiagnosticsSettings.PushDefaults,
                Symbols = new SymbolsSettings
                {
                    MaxDepth = 0,
                    Kinds = ["Function", "Class", "Variable", "Enum", "Interface", "Module",],
                },
            },
            ["pyright"] = new()
            {
                Command = "pyright-langserver --stdio",
                Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        ".py", "python"
                    },
                },
                WorkspaceFiles = ["pyproject.toml", "requirements.txt",],
                Diagnostics = DiagnosticsSettings.PullDefaults,
                Symbols = new SymbolsSettings
                {
                    MaxDepth = 0,
                    Kinds = ["Function", "Class", "Variable",],
                },
            },
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
                return Task.FromResult(new Dictionary<string, LspProfile>());

            var packageConfig = _yamlDeserializer.Deserialize<LspConfig>(packageDefaultsYaml);

            if (packageConfig?.Lsps != null)
            {
                _logger.LogDebug("Loaded {Count} package default profiles",
                    packageConfig.Lsps.Count
                );

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
            "lsp-use"
        );

        return Path.Combine(configDir, "lsps.yaml");
    }

    /// <summary>
    /// Gets package-specific default YAML configuration.
    /// This will be overridden in language-specific packages to load embedded resources.
    /// </summary>
    protected virtual string? GetPackageDefaultsYaml() =>
        // Base implementation returns null - language-specific packages can override this
        // to return embedded YAML resources
        null;

    /// <summary>
    /// Creates the user configuration directory if it doesn't exist.
    /// </summary>
    public static void EnsureUserConfigDirectoryExists()
    {
        var configDir = Path.GetDirectoryName(GetUserConfigPath())!;
        Directory.CreateDirectory(configDir);
    }
}
