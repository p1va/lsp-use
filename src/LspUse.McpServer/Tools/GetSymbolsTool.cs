using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class GetSymbolsTool
{
    [McpServerTool(Name = "get_symbols", Title = "GetSymbols", UseStructuredContent = true)]
    [Description(
        "Parses the symbols from a specified code file. Useful for understanding the structure and contents of large files without requiring position coordinates.")]
    public static async Task<IEnumerable<DocumentSymbol>> GetDocumentSymbolsAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description("The path of the file to extract symbols from")] string file,
        CancellationToken cancellationToken,
        [Description("Optional maximum depth of symbols to return. 0 = only top-level symbols, 1 = top-level + first nested level, etc. If not specified, uses LSP profile default.")] int? maxDepth = null)
    {
        var logger = loggerFactory.CreateLogger("DocumentSymbolsTool");

        logger.LogInformation("MCP DocumentSymbolsTool called for {FilePath}", file);

        var result = await service.GetDocumentSymbolsAsync(new GetSymbolsRequest
        {
            FilePath = file,
            MaxDepth = maxDepth
        }, cancellationToken);

        return result.Match<IEnumerable<DocumentSymbol>>(
            success =>
            {
                logger.LogInformation("MCP DocumentSymbolsTool returning {Count} symbols", success.Symbols.Count());
                return success.Symbols.ToList() ?? [];
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
}
