using System.IO.Pipelines;
using System.Text.Json.Nodes;
using LspUse.Application;
using LspUse.Application.Configuration;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace LspUse.Mcp.IntegrationTests;

public class WindowLogMessagesToolTests
{
    private static async Task<(IMcpClient Client, IMcpServer Server, CancellationTokenSource Cts)>
        StartInMemoryServerAsync()
    {
        var pipeClientToServer = new Pipe();
        var pipeServerToClient = new Pipe();

        var services = new ServiceCollection();
        services.AddLogging();

        // Configure ApplicationService dependencies for testing
        var testConfig = new LanguageServerProcessConfiguration
        {
            Command = "dotnet",
            Arguments = new[]
            {
                "test.dll"
            },
            WorkspacePath = "/tmp"
        };
        services.AddSingleton(Options.Create(testConfig));

        // Add notification handlers
        services.AddSingleton<ILspNotificationHandler, WindowNotificationHandler>()
            .AddSingleton<ILspNotificationHandler, DiagnosticsNotificationHandler>()
            .AddSingleton<ILspNotificationHandler, WorkspaceNotificationHandler>()
            .AddSingleton<ILspNotificationHandler, ClientCapabilityRegistrationHandler>();

        // Add ApplicationService
        services.AddSingleton<IApplicationService, ApplicationService>();

        // Configure in-memory MCP server using the same API version as the production code.
        // Tools are discovered via reflection from the assembly that contains WindowLogMessagesTool.
        services.AddMcpServer()
            .WithStreamServerTransport(pipeClientToServer.Reader.AsStream(),
                pipeServerToClient.Writer.AsStream())
            // First parameter is optional JsonSerializerOptions (pass null), second is the assembly.
            .WithToolsFromAssembly(typeof(WindowLogMessagesTool).Assembly);

        var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<IMcpServer>();

        var cts = new CancellationTokenSource();
        _ = server.RunAsync(cts.Token); // fire-and-forget background task

        var client = await McpClientFactory.CreateAsync(
            new StreamClientTransport(pipeClientToServer.Writer.AsStream(),
                pipeServerToClient.Reader.AsStream()));

        return (client, server, cts);
    }

    [Fact]
    public async Task Tool_is_advertised_with_schema()
    {
        var (client, server, cts) = await StartInMemoryServerAsync();

        await using var _ = client;

        var tools = await client.ListToolsAsync();
        var windowLogTool = Assert.Single(tools, t => t.Name == "get_window_log_messages");

        // Schema must describe the structured output
        Assert.True(windowLogTool.ReturnJsonSchema.HasValue);
        var schema = windowLogTool.ReturnJsonSchema.Value;
        Assert.Equal("object", schema.GetProperty("type").GetString());

        cts.Cancel();
    }

    [Fact]
    public async Task Tool_returns_window_log_messages()
    {
        var (client, _, cts) = await StartInMemoryServerAsync();
        _ = client;

        var result = await client.CallToolAsync("get_window_log_messages",
            new Dictionary<string, object?>());

        Assert.NotNull(result.StructuredContent);

        // The MCP response wraps our tool result in a "result" property
        var content = result.StructuredContent.AsObject();
        Assert.True(content.TryGetPropertyValue("result", out var resultNode));
        Assert.NotNull(resultNode);

        var resultObj = resultNode.AsObject();
        Assert.True(resultObj.TryGetPropertyValue("logMessages", out var logMessages));
        Assert.True(resultObj.TryGetPropertyValue("totalCount", out var totalCount));

        // logMessages should be an array
        Assert.NotNull(logMessages);
        Assert.True(logMessages is JsonArray);

        // totalCount should be a number
        Assert.NotNull(totalCount);
        var countValue = totalCount.AsValue();
        Assert.True(countValue.TryGetValue<int>(out var count));
        Assert.True(count >= 0);

        cts.Cancel();
    }
}
