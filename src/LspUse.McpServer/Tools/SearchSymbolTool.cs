using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
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

    [McpServerTool(Name = ToolName, Title = ToolTitle, UseStructuredContent = true)]
    [Description(ToolDescription)]
    public static async Task<IEnumerable<DocumentSymbol>> SearchSymbolsAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description(ToolArgDescFilePath)] string query, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(SearchSymbolTool));

        logger.LogInformation("MCP SearchSymbolsTool called with query: {Query}", query);

        try
        {
            var result = await service.SearchSymbolAsync(new SearchSymbolRequest
            {
                Query = query
            }, cancellationToken);

            var symbolList = result.Value.ToList();
            logger.LogInformation("MCP SearchSymbolsTool returning {Count} symbols",
                symbolList.Count);

            return result.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MCP SearchSymbolsTool for query: {Query}", query);

            throw;
        }
    }
}
