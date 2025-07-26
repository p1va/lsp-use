using System.ComponentModel;
using System.Text;
using LspUse.Application;
using LspUse.Application.Models;
using LspUse.LanguageServerClient.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OneOf;

namespace LspUse.McpServer.Tools;

[McpServerToolType]
public static class CompletionTool
{
    [McpServerTool(Name = "completion", Title = "Completion", UseStructuredContent = false)]
    [Description(
        "Gets code completion suggestions at a specific position in a file. Returns a list of possible completions including variables, methods, classes, and other language constructs available at the cursor position. Useful for exploring what's available in scope or after an object/class reference."
    )]
    public static async Task<IEnumerable<TextContentBlock>> CompletionAsync(
        IApplicationService service,
        ILoggerFactory loggerFactory,
        [Description("The path of the file to get completions for")]
        string file,
        [Description("The line number where completions are requested (1-based)")]
        uint line,
        [Description("The character position on the line where completions are requested (1-based)"
        )]
        uint character,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CompletionTool");

        logger.LogInformation("MCP CompletionTool called for {FilePath} at {Line}:{Character}",
            file,
            line,
            character
        );

        var result = await service.CompletionAsync(new CompletionRequest
        {
            FilePath = file,
            Position = new EditorPosition
            {
                Line = line,
                Character = character,
            },
        },
            cancellationToken
        );

        return result.Match<IEnumerable<TextContentBlock>>(success =>
            {
                var completionItems = success.Items.ToList();
                logger.LogInformation("MCP CompletionTool returning {Count} completion items",
                    completionItems.Count
                );

                if (!completionItems.Any())
                {
                    return BuildCompletionResultBlocks(completionItems, success.DebugContext, file);
                }

                return BuildCompletionResultBlocks(completionItems, success.DebugContext, file);
            },
            error =>
            {
                logger.LogError("MCP CompletionTool error: {Message} ({ErrorCode})",
                    error.Message,
                    error.ErrorCode
                );
                if (error.Exception != null)
                    logger.LogError(error.Exception, "Underlying exception for completion error");

                throw new InvalidOperationException($"Completion operation failed: {error.Message}",
                    error.Exception
                );
            }
        );
    }

    private static IEnumerable<TextContentBlock> BuildCompletionResultBlocks(
        List<CompletionItem> completionItems,
        string debugContext,
        string file)
    {
        // Group by completion item kind
        var groupedCompletions = completionItems
            .GroupBy(item => item.Kind ?? CompletionItemKind.Text)
            .OrderBy(group => (int)group.Key) // Order by enum value for consistency
            .ToList();

        // Consolidated summary block matching other tools format
        var totalCompletions = completionItems.Count;
        var kindCounts = groupedCompletions
            .Select(g => $"{g.Count()} {GetPluralizedKind(g.Key, g.Count())}")
            .ToList();

        var relativeFile = GetRelativeFilePath(file);
        var breakdownText = totalCompletions > 0 
            ? $"Breakdown: {string.Join(", ", kindCounts)}"
            : "No completions found";

        var summary = $"Found {totalCompletions} completion{(totalCompletions != 1 ? "s" : "")} in file: {relativeFile}\n{debugContext}\n{breakdownText}";

        yield return new TextContentBlock { Text = summary };

        // Create blocks for each completion kind
        foreach (var kindGroup in groupedCompletions)
        {
            var kindName = GetKindDisplayName(kindGroup.Key);
            var items = kindGroup
                .OrderBy(item => item.Label)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"{kindName} ({items.Count}):");

            foreach (var item in items)
                sb.AppendLine($"  {item.Label}");

            yield return new TextContentBlock
            {
                Text = sb
                    .ToString()
                    .TrimEnd(),
            };
        }
    }

    private static string GetKindDisplayName(CompletionItemKind kind) =>
        kind switch
        {
            CompletionItemKind.Method => "Methods",
            CompletionItemKind.Function => "Functions",
            CompletionItemKind.Constructor => "Constructors",
            CompletionItemKind.Field => "Fields",
            CompletionItemKind.Variable => "Variables",
            CompletionItemKind.Class => "Classes",
            CompletionItemKind.Interface => "Interfaces",
            CompletionItemKind.Module => "Modules",
            CompletionItemKind.Property => "Properties",
            CompletionItemKind.Unit => "Units",
            CompletionItemKind.Value => "Values",
            CompletionItemKind.Enum => "Enums",
            CompletionItemKind.Keyword => "Keywords",
            CompletionItemKind.Snippet => "Snippets",
            CompletionItemKind.Color => "Colors",
            CompletionItemKind.File => "Files",
            CompletionItemKind.Reference => "References",
            CompletionItemKind.Folder => "Folders",
            CompletionItemKind.EnumMember => "Enum Members",
            CompletionItemKind.Constant => "Constants",
            CompletionItemKind.Struct => "Structs",
            CompletionItemKind.Event => "Events",
            CompletionItemKind.Operator => "Operators",
            CompletionItemKind.TypeParameter => "Type Parameters",
            CompletionItemKind.Text => "Text",
            _ => kind.ToString(),
        };

    private static string GetPluralizedKind(CompletionItemKind kind, int count)
    {
        if (count == 1)
        {
            return kind switch
            {
                CompletionItemKind.Class => "class",
                CompletionItemKind.Property => "property",
                CompletionItemKind.Method => "method",
                CompletionItemKind.Field => "field",
                CompletionItemKind.Variable => "variable",
                CompletionItemKind.Interface => "interface",
                CompletionItemKind.Enum => "enum",
                _ => kind
                    .ToString()
                    .ToLower(),
            };
        }

        return kind switch
        {
            CompletionItemKind.Class => "classes",
            CompletionItemKind.Property => "properties",
            CompletionItemKind.Method => "methods",
            CompletionItemKind.Field => "fields",
            CompletionItemKind.Variable => "variables",
            CompletionItemKind.Interface => "interfaces",
            CompletionItemKind.Enum => "enums",
            _ => kind
                .ToString()
                .ToLower() + "s",
        };
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
}
