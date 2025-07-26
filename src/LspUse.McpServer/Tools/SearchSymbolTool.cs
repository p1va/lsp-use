using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class SearchSymbolTool
{
    private const string ToolName = "search_symbols";
    private const string ToolTitle = "SearchSymbols";

    private const string ToolDescription =
        "Searches for symbols (classes, methods, fields, etc.) across the entire codebase by name. Useful for finding where specific symbols are defined when you know part of their name but not their exact location.";

    private const string ToolArgDescFilePath =
        "The search query to find symbols. Supports partial string matching";

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = false)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<TextContentBlock>> SearchSymbolsAsync(
        IApplicationService service,
        ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string query,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(SearchSymbolTool));

        logger.LogInformation("MCP SearchSymbolsTool called with query: {Query}", query);

        var result = await service.SearchSymbolAsync(new SearchSymbolRequest
        {
            Query = query,
        },
            cancellationToken
        );

        return result.Match<IEnumerable<TextContentBlock>>(success =>
            {
                var symbolList = success.Value.ToList();
                logger.LogInformation("MCP SearchSymbolsTool returning {Count} symbols",
                    symbolList.Count
                );

                var totalMatches = symbolList.Count;
                var symbolsByFile = success.Value.GroupBy(x => x.Location?.FilePath).ToList();
                var totalFiles = symbolsByFile.Count;

                var resultsText = totalMatches == 1 ? "result" : "results";
                var summaryText = totalMatches == 0
                    ? $"Found 0 results for \"{query}\""
                    : $"Found {totalMatches} {resultsText} for \"{query}\" across {totalFiles} {(totalFiles == 1 ? "file" : "files")}:\n";

                var summary = new TextContentBlock
                {
                    Text = summaryText
                };

                var resultBlocks = symbolsByFile
                    .Select(file => new TextContentBlock
                    {
                        Text = BuildFileResultsText(file),
                    });

                return [summary, .. resultBlocks];
            },
            error =>
            {
                logger.LogError("MCP SearchSymbolsTool error: {Message} ({ErrorCode})",
                    error.Message,
                    error.ErrorCode
                );

                if (error.Exception != null)
                {
                    logger.LogError(error.Exception,
                        "Underlying exception for search symbols error"
                    );
                }

                throw new InvalidOperationException(
                    $"Search symbols operation failed: {error.Message}",
                    error.Exception
                );
            }
        );
    }

    private static string BuildFileResultsText(IGrouping<Uri?, DocumentSymbol> file)
    {
        var fileHeader = $"{file.Count()} {(file.Count() == 1 ? "result" : "results")} in file: {GetRelativeFilePath(file.Key)}";
        var symbolEntries = file.Select(FormatSymbolEntry);

        return string.Join("\n", [fileHeader, .. symbolEntries]);
    }

    private static string FormatSymbolEntry(DocumentSymbol symbol) =>
        $"""
        {symbol.Name} @{symbol.Location?.StartLine}:{symbol.Location?.StartCharacter} ({symbol.Kind})
            Code: {symbol.Location?.Text}
        """;

    private static string GetRelativeFilePath(Uri? fileUri)
    {
        if (fileUri == null)
            return "<unknown>";

        try
        {
            var currentDir = Directory.GetCurrentDirectory();

            if (fileUri.Scheme == "file")
            {
                var localPath = fileUri.LocalPath;
                if (localPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetRelativePath(currentDir, localPath);
                }
            }
        }
        catch
        {
            // Fall back to original path if any error occurs
        }

        return fileUri.ToString();
    }
}
