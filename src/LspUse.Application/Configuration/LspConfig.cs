namespace LspUse.Application.Configuration;

/// <summary>
/// Root configuration object for YAML-based LSP server configurations.
/// This represents the structure of the lsps.yaml configuration file.
/// </summary>
public record LspConfig
{
    /// <summary>
    /// Dictionary of LSP profiles, keyed by LSP name.
    /// The key serves as the LSP identifier used with --lsp flag.
    /// 
    /// Example YAML:
    /// lsps:
    ///   typescript:
    ///     command: "typescript-language-server --stdio"
    ///     extensions: { ".ts": "typescript", ".tsx": "typescriptreact" }
    ///   pyright:
    ///     command: "pyright-langserver --stdio"
    ///     extensions: { ".py": "python" }
    /// </summary>
    public Dictionary<string, LspProfile> Lsps { get; init; } = new();
}
