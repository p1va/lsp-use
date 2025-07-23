using System.Dynamic;
using System.Text.Json.Nodes;
using LspUse.LanguageServerClient.Handlers;
using Microsoft.CSharp.RuntimeBinder;
using Xunit;

namespace LspUse.Client.UnitTests;

/// <summary>
/// Mock InvokeMemberBinder for testing DynamicObject functionality
/// </summary>
public class MockInvokeMemberBinder : InvokeMemberBinder
{
    public MockInvokeMemberBinder(string name) : base(name, false, new CallInfo(0)) { }

    public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject? errorSuggestion)
    {
        throw new NotImplementedException();
    }

    public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject? errorSuggestion)
    {
        throw new NotImplementedException();
    }
}

public class DefaultRequestHandlerTests
{
    [Fact]
    public void TryInvokeMember_HandlesWorkspaceConfigurationRequest()
    {
        // Arrange
        var handler = new DefaultRequestHandler();
        var mockBinder = new MockInvokeMemberBinder("workspace/configuration");

        // Act - simulate workspace/configuration request  
        var success = handler.TryInvokeMember(mockBinder, new object[] { }, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result); // Should return JSON array
        Assert.Single(handler.UnhandledRequests);

        handler.UnhandledRequests.TryDequeue(out var request);
        Assert.NotNull(request);
        Assert.Equal("workspace/configuration", request.MethodName);
        Assert.Equal(0, request.ArgumentCount);
    }

    [Fact]
    public void TryInvokeMember_HandlesUnknownRequest()
    {
        // Arrange
        var handler = new DefaultRequestHandler();

        // Create a mock binder for testing
        var mockBinder = new MockInvokeMemberBinder("unknown/request");

        // Act - simulate unknown request
        var success = handler.TryInvokeMember(mockBinder, new object[] { "param1" }, out var result);

        // Assert
        Assert.True(success);
        Assert.Null(result); // Unknown requests return null
        Assert.Single(handler.UnhandledRequests);

        handler.UnhandledRequests.TryDequeue(out var request);
        Assert.NotNull(request);
        Assert.Equal("unknown/request", request.MethodName);
        Assert.Equal(1, request.ArgumentCount);
    }

    [Fact]
    public void TryInvokeMember_RecordsTimestamp()
    {
        // Arrange
        var handler = new DefaultRequestHandler();
        var mockBinder = new MockInvokeMemberBinder("testRequest");
        var before = DateTime.UtcNow;

        // Act
        var success = handler.TryInvokeMember(mockBinder, new object[] { "param1", 42 }, out _);
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(success);
        Assert.Single(handler.UnhandledRequests);

        handler.UnhandledRequests.TryDequeue(out var request);
        Assert.NotNull(request);
        Assert.True(request.Timestamp >= before && request.Timestamp <= after);
        Assert.Equal("testRequest", request.MethodName);
        Assert.Equal(2, request.ArgumentCount);
    }
}
