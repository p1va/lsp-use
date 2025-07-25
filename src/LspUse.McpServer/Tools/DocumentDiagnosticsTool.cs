using System.ComponentModel;
using System.Text;
using LspUse.Application;
using LspUse.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class DocumentDiagnosticsTool
{
    [McpServerTool(Name = "get_diagnostics", Title = "GetDiagnostics", UseStructuredContent = false)]
    [Description(
        "Gets diagnostics for a specified file sorted by severity (Error → Warning → Information → Hint) and by line number")]
    public static async Task<IEnumerable<TextContentBlock>> GetDocumentDiagnosticsAsync(
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

            return result.Match(
                success => BuildDiagnosticsResultBlocks(success, file),
                error => throw new InvalidOperationException($"Error getting document diagnostics: {error.Message}", error.Exception)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MCP DocumentDiagnosticsTool for {FilePath}", file);

            throw;
        }
    }

    private static string GetRelativeFilePath(string filePath)
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var uri = new Uri(filePath);
            var localPath = uri.LocalPath;
            return Path.GetRelativePath(currentDirectory, localPath);
        }
        catch
        {
            return filePath;
        }
    }

    private static IEnumerable<TextContentBlock> BuildDiagnosticsResultBlocks(IEnumerable<DocumentDiagnostic> diagnostics, string filePath)
    {
        var diagnosticList = diagnostics.ToList();
        var relativeFilePath = GetRelativeFilePath(filePath);

        // If no diagnostics, return single block
        if (!diagnosticList.Any())
        {
            return [new TextContentBlock { Text = $"No diagnostics found for {relativeFilePath}" }];
        }

        var blocks = new List<TextContentBlock>();

        // Summary block with counts by severity
        var severityGroups = diagnosticList
            .GroupBy(d => d.Severity)
            .OrderBy(g => g.First().SeverityOrder)
            .ToList();

        var summary = new StringBuilder();
        summary.AppendLine($"Found {diagnosticList.Count} diagnostic{(diagnosticList.Count != 1 ? "s" : "")} in {relativeFilePath}:");

        foreach (var group in severityGroups)
        {
            var count = group.Count();
            var severity = group.Key;
            summary.AppendLine($"  {count} {severity}{(count != 1 ? "s" : "")}");
        }

        blocks.Add(new TextContentBlock { Text = summary.ToString().TrimEnd() });

        // Group diagnostics by severity and create blocks
        foreach (var severityGroup in severityGroups)
        {
            var severity = severityGroup.Key;
            var diagnosticsInGroup = severityGroup.OrderBy(d => d.StartLine).ThenBy(d => d.StartCharacter).ToList();
            
            var severityBlock = new StringBuilder();
            severityBlock.AppendLine($"{severity}s ({diagnosticsInGroup.Count}):");

            foreach (var diagnostic in diagnosticsInGroup)
            {
                var position = FormatDiagnosticPosition(diagnostic);
                
                severityBlock.AppendLine($"  {position} {diagnostic.Message}");
                
                if (!string.IsNullOrEmpty(diagnostic.Code))
                {
                    var ruleInfo = diagnostic.Code;
                    if (!string.IsNullOrEmpty(diagnostic.CodeDescription))
                    {
                        ruleInfo += $" ({diagnostic.CodeDescription})";
                    }
                    severityBlock.AppendLine($"    Rule: {ruleInfo}");
                }

                if (!string.IsNullOrEmpty(diagnostic.Text))
                {
                    severityBlock.AppendLine($"    Code: {diagnostic.Text.Trim()}");
                }
            }

            blocks.Add(new TextContentBlock { Text = severityBlock.ToString().TrimEnd() });
        }

        return blocks;
    }

    private static string FormatDiagnosticPosition(DocumentDiagnostic diagnostic)
    {
        var startLine = diagnostic.StartLine;
        var startChar = diagnostic.StartCharacter;
        var endLine = diagnostic.EndLine;
        var endChar = diagnostic.EndCharacter;

        // Format similar to other tools: @line:char or @line:start-end for ranges
        if (startLine == endLine)
        {
            if (startChar == endChar)
            {
                return $"@{startLine}:{startChar}";
            }
            else
            {
                return $"@{startLine}:{startChar}-{endChar}";
            }
        }
        else
        {
            return $"@{startLine}:{startChar}-{endLine}:{endChar}";
        }
    }
}
