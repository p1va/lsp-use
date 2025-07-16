using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LspUse.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace LspUse.Mcp.IntegrationTests;

public class GoToDefinitionToolTests
{
    private static async Task<(IMcpClient Client, IMcpServer Server, CancellationTokenSource Cts)> StartInMemoryServerAsync()
    {
        var pipeClientToServer = new Pipe();
        var pipeServerToClient = new Pipe();

        var services = new ServiceCollection();
        services.AddLogging();
        // Configure in-memory MCP server using the same API version as the production code.
        // Tools are discovered via reflection from the assembly that contains GoToDefinitionTool.

        services.AddMcpServer()
                .WithStreamServerTransport(pipeClientToServer.Reader.AsStream(), pipeServerToClient.Writer.AsStream())
                // First parameter is optional JsonSerializerOptions (pass null), second is the assembly.
                .WithToolsFromAssembly(toolAssembly: typeof(GoToDefinitionTool).Assembly);

        var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<IMcpServer>();

        var cts = new CancellationTokenSource();
        _ = server.RunAsync(cts.Token); // fire-and-forget background task

        var client = await McpClientFactory.CreateAsync(
            new StreamClientTransport(pipeClientToServer.Writer.AsStream(), pipeServerToClient.Reader.AsStream()));

        return (client, server, cts);
    }

    [Fact]
    public async Task Tool_is_advertised_with_schema()
    {
        var (client, server, cts) = await StartInMemoryServerAsync();

        await using var _ = client;

        var tools = await client.ListToolsAsync();
        var goToDef = Assert.Single(tools, t => t.Name == "go_to_definition");

        // Schema must describe the structured output
        Assert.True(goToDef.ReturnJsonSchema.HasValue);
        var schema = goToDef.ReturnJsonSchema.Value;
        Assert.Equal("object", schema.GetProperty("type").GetString());

        cts.Cancel();
    }

    [Fact]
    public async Task Tool_returns_structured_definition_locations()
    {
        var (client, _, cts) = await StartInMemoryServerAsync();
        _ = client;

        var result = await client.CallToolAsync("go_to_definition", new Dictionary<string, object?>
        {
            ["filePath"] = "/tmp/test.cs",
            ["line"] = 10,
            ["character"] = 15
        });

        Assert.NotNull(result.StructuredContent);

        cts.Cancel();
    }
}
