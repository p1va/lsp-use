using LspUse.LanguageServerClient.Handlers;
using Xunit;

namespace LspUse.Client.UnitTests;

public class DefaultNotificationHandlerTests
{
    [Fact]
    public void TryInvokeMember_CatchesUnknownMethods()
    {
        // Arrange
        var handler = new DefaultNotificationHandler();
        dynamic dynamicHandler = handler;

        // Act - call unknown methods via dynamic
        dynamicHandler.UnknownMethod("param1", 42);
        dynamicHandler.AnotherMethod();
        dynamicHandler.TestMethod("test", true);

        // Assert
        Assert.Equal(3, handler.UnhandledNotifications.Count);

        var notifications = new List<UnhandledNotification>();
        while (handler.UnhandledNotifications.TryDequeue(out var notification))
        {
            notifications.Add(notification);
        }

        Assert.Contains(notifications, n => n.MethodName == "UnknownMethod" && n.ArgumentCount == 2);
        Assert.Contains(notifications, n => n.MethodName == "AnotherMethod" && n.ArgumentCount == 0);
        Assert.Contains(notifications, n => n.MethodName == "TestMethod" && n.ArgumentCount == 2);
    }

    [Fact]
    public void TryInvokeMember_RecordsTimestamp()
    {
        // Arrange
        var handler = new DefaultNotificationHandler();
        dynamic dynamicHandler = handler;
        var before = DateTime.UtcNow;

        // Act
        dynamicHandler.TimestampTest();
        var after = DateTime.UtcNow;

        // Assert
        Assert.Single(handler.UnhandledNotifications);

        handler.UnhandledNotifications.TryDequeue(out var notification);
        Assert.NotNull(notification);
        Assert.True(notification.Timestamp >= before && notification.Timestamp <= after);
    }
}
