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

public class HoverToolTests
{
    private static async Task<(IMcpClient Client, IMcpServer Server, CancellationTokenSource Cts)>
        StartInMemoryServerAsync()
    {
        var pipeClientToServer = new Pipe();
        var pipeServerToClient = new Pipe();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMcpServer()
            .WithStreamServerTransport(pipeClientToServer.Reader.AsStream(),
                pipeServerToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(HoverTool).Assembly);

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
        var hover = Assert.Single(tools, t => t.Name == "hover");

        // Schema must describe the structured output
        Assert.True(hover.ReturnJsonSchema.HasValue);
        var schema = hover.ReturnJsonSchema.Value;
        // The hover tool can return null or an object (the hover contents)
        // So the schema should accept both null and object types

        cts.Cancel();
    }

    [Fact]
    public async Task Tool_returns_structured_hover_content()
    {
        var (client, _, cts) = await StartInMemoryServerAsync();
        _ = client;

        var result = await client.CallToolAsync("hover", new Dictionary<string, object?>
        {
            ["filePath"] =
                TestResource.Paths.JsonRpcTest,
            ["line"] = 8,
            ["character"] = 25
        });

        Assert.NotEmpty(result.Content);
        Assert.NotNull(result.Content.First());

        cts.Cancel();
    }
}
