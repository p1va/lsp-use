using System.ComponentModel;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class DocumentDiagnosticsTool
{
    [McpServerTool(Name = "get_diagnostics", Title = "GetDiagnostics", UseStructuredContent = true)]
    [Description(
        "Gets diagnostics for a specified file sorted by severity (Error → Warning → Information → Hint) and by line number")]
    public static async Task<IEnumerable<DocumentDiagnostic>> GetDocumentDiagnosticsAsync(
        IApplicationService service, ILoggerFactory loggerFactory,
        [Description("The path of the file to get diagnostics for")] string file,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DocumentDiagnosticsTool");

        logger.LogInformation("MCP DocumentDiagnosticsTool called for {FilePath}", file);

        try
        {
            var result = await service.GetDocumentDiagnosticsAsync(new DocumentDiagnosticsRequest
            {
                FilePath = file
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MCP DocumentDiagnosticsTool for {FilePath}", file);

            throw;
        }
    }
}
