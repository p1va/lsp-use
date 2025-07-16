using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DocumentSymbolsTool");

        logger.LogInformation("MCP DocumentSymbolsTool called for {FilePath}", file);

        try
        {
            var result = await service.GetDocumentSymbolsAsync(new GetSymbolsRequest
            {
                FilePath = file
            }, cancellationToken);

            return result.Symbols.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MCP DocumentSymbolsTool for {FilePath}", file);

            throw;
        }
    }
}
