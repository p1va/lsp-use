namespace LspUse.Application.Configuration;

/// <summary>
/// Root configuration object for YAML-based language server configurations.
/// This represents the structure of the languages.yaml configuration file.
/// </summary>
public record LanguageConfig
{
    /// <summary>
    /// Dictionary of language profiles, keyed by language name.
    /// The key serves as the language identifier used with --language flag.
    /// 
    /// Example YAML:
    /// languages:
    ///   typescript:
    ///     command: "typescript-language-server --stdio"
    ///     extensions: [".ts", ".tsx"]
    ///   custom-python:
    ///     command: "pylsp --stdio --verbose"
    ///     extensions: [".py"]
    /// </summary>
    public Dictionary<string, LanguageProfile> Languages { get; init; } = new();
}