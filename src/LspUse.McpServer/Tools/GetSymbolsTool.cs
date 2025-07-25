using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class GetSymbolsTool
{
    [McpServerTool(Name = "get_symbols", Title = "GetSymbols", UseStructuredContent = false)]
    [Description(
        "Parses the symbols from a specified code file. Useful for understanding the structure and contents of large files without requiring position coordinates.")]
    public static async Task<IEnumerable<TextContentBlock>> GetDocumentSymbolsAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description("The path of the file to extract symbols from")] string file,
        CancellationToken cancellationToken,
        [Description("Optional maximum depth of symbols to return. 0 = only top-level symbols, 1 = top-level + first nested level, etc. If not specified, uses LSP profile default.")] int? maxDepth = null,
        [Description("Whether to include code snippets for each symbol. When true, shows the actual source code for better context.")] bool showCode = false)
    {
        var logger = loggerFactory.CreateLogger("DocumentSymbolsTool");

        logger.LogInformation("MCP DocumentSymbolsTool called for {FilePath}", file);

        var result = await service.GetDocumentSymbolsAsync(new GetSymbolsRequest
        {
            FilePath = file,
            MaxDepth = maxDepth
        }, cancellationToken);

        return result.Match<IEnumerable<TextContentBlock>>(
            success =>
            {
                var symbols = success.Symbols.ToList();
                logger.LogInformation("MCP DocumentSymbolsTool returning {Count} symbols", symbols.Count);
                
                if (!symbols.Any())
                {
                    return [new TextContentBlock { Text = $"No symbols found in file: {GetRelativeFilePath(file)}" }];
                }

                // Create summary
                var symbolCounts = symbols
                    .GroupBy(s => s.Kind)
                    .ToDictionary(g => g.Key, g => g.Count());

                var totalSymbols = symbols.Count;
                var summaryParts = symbolCounts
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Value} {GetPluralizedKind(kvp.Key, kvp.Value)}")
                    .ToList();

                var summary = new TextContentBlock
                {
                    Text = $"Found {totalSymbols} {(totalSymbols == 1 ? "symbol" : "symbols")} in file: {GetRelativeFilePath(file)}\nSymbol breakdown: {string.Join(", ", summaryParts)}\n"
                };

                // Group symbols by container and format hierarchically
                var formattedBlocks = BuildHierarchicalSymbolBlocks(symbols, showCode);

                return [summary, .. formattedBlocks];
            },
            error =>
            {
                logger.LogError("MCP DocumentSymbolsTool error: {Message} ({ErrorCode})", error.Message, error.ErrorCode);
                if (error.Exception != null)
                {
                    logger.LogError(error.Exception, "Underlying exception for document symbols error");
                }
                throw new InvalidOperationException($"Document symbols operation failed: {error.Message}", error.Exception);
            }
        );
    }

    private static IEnumerable<TextContentBlock> BuildHierarchicalSymbolBlocks(List<DocumentSymbol> symbols, bool showCode)
    {
        // Sort symbols by line number first to maintain source order
        var sortedSymbols = symbols.OrderBy(s => s.Location?.StartLine ?? 0).ToList();
        
        // Build the tree representation using depth for indentation
        var lines = new List<string>();
        
        foreach (var symbol in sortedSymbols)
        {
            var indent = new string(' ', symbol.Depth * 2); // 2 spaces per depth level
            var formattedSymbol = $"{indent}{symbol.Name} ({symbol.Kind}) @{symbol.Location?.StartLine}:{symbol.Location?.StartCharacter}";
            lines.Add(formattedSymbol);
            
            // Add code snippet if requested and available
            if (showCode && !string.IsNullOrEmpty(symbol.Location?.Text))
            {
                var codeIndent = new string(' ', symbol.Depth * 2 + 2); // Extra 2 spaces for code
                var codeSnippet = $"{codeIndent}Code: {symbol.Location.Text.Trim()}";
                lines.Add(codeSnippet);
            }
        }

        // Group lines into blocks to avoid overly long single blocks
        const int maxLinesPerBlock = 25;
        var blocks = new List<TextContentBlock>();
        
        for (int i = 0; i < lines.Count; i += maxLinesPerBlock)
        {
            var blockLines = lines.Skip(i).Take(maxLinesPerBlock);
            blocks.Add(new TextContentBlock { Text = string.Join("\n", blockLines) });
        }

        return blocks.Any() ? blocks : [new TextContentBlock { Text = "No symbols to display" }];
    }


    private static string GetRelativeFilePath(string filePath)
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            
            if (Path.IsPathRooted(filePath) && filePath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(currentDir, filePath);
            }
        }
        catch
        {
            // Fall back to original path if any error occurs
        }
        
        return filePath;
    }

    private static string GetPluralizedKind(string kind, int count)
    {
        if (count == 1)
            return kind.ToLower();

        return kind.ToLower() switch
        {
            "class" => "classes",
            "property" => "properties",
            _ => kind.ToLower() + "s"
        };
    }
}
