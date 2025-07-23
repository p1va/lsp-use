using Microsoft.Extensions.Logging;

namespace LspUse.Application.Configuration;

/// <summary>
/// Maps file extensions to LSP language identifiers using configured language profiles.
/// Provides fallback to a comprehensive set of built-in extension mappings based on the LSP specification.
/// </summary>
public class LanguageIdMapper
{
    private readonly ILogger<LanguageIdMapper> _logger;
    private readonly LspProfileResolver _resolver;

    /// <summary>
    /// Default extension-to-language-id mapping based on LSP specification.
    /// This serves as fallback when no specific language profile is configured.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultExtensionMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // C# and .NET
        { ".cs", "csharp" },
        { ".csx", "csharp" },
        { ".cake", "csharp" },
        { ".fs", "fsharp" },
        { ".fsx", "fsharp" },
        { ".vb", "vb" },
        
        // JavaScript/TypeScript ecosystem
        { ".js", "javascript" },
        { ".mjs", "javascript" },
        { ".jsx", "javascriptreact" },
        { ".ts", "typescript" },
        { ".tsx", "typescriptreact" },
        { ".json", "json" },
        
        // Web technologies
        { ".html", "html" },
        { ".htm", "html" },
        { ".css", "css" },
        { ".scss", "scss" },
        { ".sass", "sass" },
        { ".less", "less" },
        
        // Systems programming
        { ".c", "c" },
        { ".h", "c" },
        { ".cpp", "cpp" },
        { ".cc", "cpp" },
        { ".cxx", "cpp" },
        { ".hpp", "cpp" },
        { ".hxx", "cpp" },
        { ".rust", "rust" },
        { ".rs", "rust" },
        { ".go", "go" },
        
        // JVM languages
        { ".java", "java" },
        { ".scala", "scala" },
        { ".groovy", "groovy" },
        { ".gradle", "groovy" },
        
        // Dynamic languages
        { ".py", "python" },
        { ".pyx", "python" },
        { ".pyi", "python" },
        { ".rb", "ruby" },
        { ".php", "php" },
        { ".php3", "php" },
        { ".php4", "php" },
        { ".php5", "php" },
        { ".phtml", "php" },
        
        // Functional languages
        { ".hs", "haskell" },
        { ".lhs", "haskell" },
        { ".ex", "elixir" },
        { ".exs", "elixir" },
        { ".erl", "erlang" },
        { ".hrl", "erlang" },
        { ".clj", "clojure" },
        { ".cljs", "clojure" },
        { ".cljc", "clojure" },
        
        // Mobile development
        { ".swift", "swift" },
        { ".m", "objective-c" },
        { ".mm", "objective-cpp" },
        { ".kt", "kotlin" },
        { ".kts", "kotlin" },
        { ".dart", "dart" },
        
        // Scripting and config
        { ".sh", "shellscript" },
        { ".bash", "shellscript" },
        { ".zsh", "shellscript" },
        { ".fish", "shellscript" },
        { ".ps1", "powershell" },
        { ".psm1", "powershell" },
        { ".psd1", "powershell" },
        { ".bat", "bat" },
        { ".cmd", "bat" },
        
        // Data and markup
        { ".xml", "xml" },
        { ".xsl", "xsl" },
        { ".xslt", "xsl" },
        { ".yaml", "yaml" },
        { ".yml", "yaml" },
        { ".toml", "toml" },
        { ".ini", "ini" },
        { ".cfg", "ini" },
        { ".conf", "ini" },
        
        // Documentation
        { ".md", "markdown" },
        { ".markdown", "markdown" },
        { ".tex", "tex" },
        { ".latex", "latex" },
        { ".bib", "bibtex" },
        
        // Database
        { ".sql", "sql" },
        
        // R and data science
        { ".r", "r" },
        { ".rmd", "r" },
        
        // Other languages
        { ".lua", "lua" },
        { ".perl", "perl" },
        { ".pl", "perl" },
        { ".pm", "perl" },
        { ".coffee", "coffeescript" },
        { ".dockerfile", "dockerfile" },
        { ".containerfile", "dockerfile" },
        
        // Template languages
        { ".hbs", "handlebars" },
        { ".handlebars", "handlebars" },
        { ".pug", "pug" },
        { ".jade", "pug" },
        
        // Shader languages
        { ".hlsl", "shaderlab" },
        { ".shader", "shaderlab" },
        { ".cginc", "shaderlab" }
    };

    public LanguageIdMapper(LspProfileResolver resolver, ILogger<LanguageIdMapper> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Maps a file path to its corresponding LSP language identifier.
    /// </summary>
    /// <param name="filePath">The file path to map</param>
    /// <returns>The LSP language identifier, or "plaintext" if no mapping is found</returns>
    public string MapFileToLanguageId(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            _logger.LogDebug("No file extension found for {FilePath}, defaulting to plaintext", filePath);
            return "plaintext";
        }

        // First, try to find a language profile that includes this extension
        var languageId = FindLanguageIdFromProfiles(extension);
        if (languageId != null)
        {
            _logger.LogDebug("Mapped {Extension} to {LanguageId} via language profile", extension, languageId);
            return languageId;
        }

        // Fallback to default mappings
        if (DefaultExtensionMappings.TryGetValue(extension, out var defaultLanguageId))
        {
            _logger.LogDebug("Mapped {Extension} to {LanguageId} via default mapping", extension, defaultLanguageId);
            return defaultLanguageId;
        }

        _logger.LogDebug("No mapping found for {Extension}, defaulting to plaintext", extension);
        return "plaintext";
    }

    /// <summary>
    /// Attempts to find a language ID from configured language profiles.
    /// </summary>
    /// <param name="extension">The file extension to search for</param>
    /// <returns>The language ID if found, null otherwise</returns>
    private string? FindLanguageIdFromProfiles(string extension)
    {
        foreach (var lspName in _resolver.GetAvailableLsps())
        {
            var profile = _resolver.GetProfile(lspName);
            if (profile?.SupportsExtension(extension) == true)
            {
                return profile.GetLanguageIdForExtension(extension, lspName);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all supported file extensions and their corresponding language IDs.
    /// </summary>
    /// <returns>A dictionary mapping extensions to language IDs</returns>
    public Dictionary<string, string> GetAllMappings()
    {
        var mappings = new Dictionary<string, string>(DefaultExtensionMappings, StringComparer.OrdinalIgnoreCase);

        // Override with profile-specific mappings
        foreach (var lspName in _resolver.GetAvailableLsps())
        {
            var profile = _resolver.GetProfile(lspName);
            if (profile != null)
            {
                // Handle new format: extension-to-language-id dictionary
                if (profile.Extensions != null)
                {
                    foreach (var (extension, languageId) in profile.Extensions)
                    {
                        mappings[extension] = languageId;
                    }
                }

                // Handle legacy format: extension array with single language ID
                if (profile.LegacyExtensions != null)
                {
                    var languageId = profile.LanguageId ?? lspName;
                    foreach (var extension in profile.LegacyExtensions)
                    {
                        mappings[extension] = languageId;
                    }
                }
            }
        }

        return mappings;
    }
}
