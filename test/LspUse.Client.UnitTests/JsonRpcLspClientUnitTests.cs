using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Models;
using Nerdbank.Streams;
using StreamJsonRpc;
using Xunit;

namespace LspUse.Client.UnitTests;

public sealed class JsonRpcLspClientUnitTests
{
    private static (JsonRpc clientRpc, JsonRpc serverRpc) CreateConnectedRpcs(object serverTarget)
    {
        // Create an in-memory full-duplex stream pair.
        var (stream1, stream2) = FullDuplexStream.CreatePair();

        var formatter1 = new JsonMessageFormatter();
        var handler1 = new HeaderDelimitedMessageHandler(stream1, stream1, formatter1);

        var formatter2 = new JsonMessageFormatter();
        var handler2 = new HeaderDelimitedMessageHandler(stream2, stream2, formatter2);

        var serverRpc = new JsonRpc(handler2, serverTarget);
        serverRpc.StartListening();

        var clientRpc = new JsonRpc(handler1);
        clientRpc.StartListening();

        return (clientRpc, serverRpc);
    }

    private static DidOpenTextDocumentParams SampleDidOpen() =>
        new()
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri("file:///tmp/foo.cs"),
                LanguageId = "csharp",
                Version = 1,
                Text = "class Foo {}"
            }
        };

    [Fact]
    public async Task DidOpen_SendsNotification()
    {
        // Arrange: server target records the params of didOpen notifications.
        var tcs = new TaskCompletionSource<DidOpenTextDocumentParams>();

        var serverTarget = new NotificationRecorder(tcs);

        var (clientRpc, serverRpc) = CreateConnectedRpcs(serverTarget);
        using var _ = clientRpc;
        using var __ = serverRpc;

        var client = new JsonRpcLspClient(clientRpc);

        var payload = SampleDidOpen();

        // Act
        await client.DidOpenAsync(payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(payload.TextDocument.Uri, received.TextDocument.Uri);
        Assert.Equal(payload.TextDocument.Text, received.TextDocument.Text);
    }

    private sealed class NotificationRecorder
    {
        private readonly TaskCompletionSource<DidOpenTextDocumentParams> _tcs;

        public NotificationRecorder(TaskCompletionSource<DidOpenTextDocumentParams> tcs) =>
            _tcs = tcs;

        [JsonRpcMethod("textDocument/didOpen", UseSingleObjectParameterDeserialization = true)]
        public void OnDidOpen(DidOpenTextDocumentParams p) => _tcs.TrySetResult(p);
    }

    [Fact]
    public async Task Definition_ReturnsServerResult()
    {
        var expected = new[]
        {
            new Location
            {
                Uri = new Uri("file:///bar.cs"),
                Range = new LanguageServerClient.Models.Range
                {
                    Start = new ZeroBasedPosition
                    {
                        Line = 1,
                        Character = 2
                    },
                    End = new ZeroBasedPosition
                    {
                        Line = 1,
                        Character = 5
                    }
                }
            }
        };

        var serverTarget = new DefinitionResponder(expected);
        var (clientRpc, serverRpc) = CreateConnectedRpcs(serverTarget);
        using var _ = clientRpc;
        using var __ = serverRpc;

        var client = new JsonRpcLspClient(clientRpc);

        var result = await client.DefinitionAsync(new DocumentClientRequest
        {
            Document = new Uri("file:///tmp/foo.cs"),
            Position = new ZeroBasedPosition
            {
                Line = 0,
                Character = 0
            }
        });

        Assert.Equal(expected, result);
    }

    private sealed class DefinitionResponder
    {
        private readonly Location[] _response;

        public DefinitionResponder(Location[] response) => _response = response;

        [JsonRpcMethod("textDocument/definition", UseSingleObjectParameterDeserialization = true)]
        public Location[] OnDefinition(TextDocumentPositionParams _params) => _response;
    }
}
