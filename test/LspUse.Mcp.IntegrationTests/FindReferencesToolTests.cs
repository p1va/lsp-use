using System.IO.Pipelines;
using LspUse.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace LspUse.Mcp.IntegrationTests;

public class FindReferencesToolTests
{
    private static async Task<(IMcpClient Client, IMcpServer Server, CancellationTokenSource Cts)>
        StartInMemoryServerAsync()
    {
        var pipeClientToServer = new Pipe();
        var pipeServerToClient = new Pipe();

        var services = new ServiceCollection();
        services.AddLogging();
        // Configure in-memory MCP server using the same API version as the production code.
        // Tools are discovered via reflection from the assembly that contains FindReferencesTool.

        services.AddMcpServer()
            .WithStreamServerTransport(pipeClientToServer.Reader.AsStream(),
                pipeServerToClient.Writer.AsStream())
            // First parameter is optional JsonSerializerOptions (pass null), second is the assembly.
            .WithToolsFromAssembly(typeof(FindReferencesTool).Assembly);

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
        var findRefs = Assert.Single(tools, t => t.Name == "find_references");

        // Schema must describe the structured output
        Assert.True(findRefs.ReturnJsonSchema.HasValue);
        var schema = findRefs.ReturnJsonSchema.Value;
        Assert.Equal("object", schema.GetProperty("type").GetString());

        cts.Cancel();
    }

    [Fact]
    public async Task Tool_returns_stub_reference_locations()
    {
        var (client, _, cts) = await StartInMemoryServerAsync();
        _ = client;

        var result = await client.CallToolAsync("find_references", new Dictionary<string, object?>
        {
            ["symbolName"] = "Foo.Bar"
        });

        Assert.NotNull(result.StructuredContent);

        cts.Cancel();
    }
}
