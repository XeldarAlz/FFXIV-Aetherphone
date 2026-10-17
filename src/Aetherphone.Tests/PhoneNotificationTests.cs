using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhoneNotificationTests
{
    private static PhoneNotification Build(string body) =>
        new("settings", "Your post was marked sensitive", body, DateTime.Now, default);

    [Fact]
    public void ASingleLineBodyPassesThroughUntouched()
    {
        var notification = Build("A moderator covered the picture on one of your posts.");
        Assert.Same(notification.Body, notification.SingleLineBody);
    }

    [Fact]
    public void ParagraphBreaksCollapseIntoOneSpace()
    {
        var notification = Build("A moderator covered the picture.\n\n1 photo(s) attached\n\nReach out to us.");
        Assert.Equal("A moderator covered the picture. 1 photo(s) attached Reach out to us.", notification.SingleLineBody);
    }

    [Fact]
    public void WindowsLineEndingsAndPaddedBreaksStaySingleSpaced()
    {
        var notification = Build("first line \r\n second line");
        Assert.Equal("first line second line", notification.SingleLineBody);
    }

    [Fact]
    public void LeadingAndTrailingBreaksAreTrimmed()
    {
        var notification = Build("\nbody text\n");
        Assert.Equal("body text", notification.SingleLineBody);
    }

    [Fact]
    public void TheStackKeyIsTheAppWhenThereIsNoGroup()
    {
        var notification = Build("body");
        Assert.Equal("settings", notification.StackKey);
    }

    [Fact]
    public void TheStackKeyScopesTheGroupToItsApp()
    {
        var velvet = new PhoneNotification("velvet", "Alice", "hi", DateTime.Now, default, "user-1");
        var aethergram = new PhoneNotification("aethergram", "Alice", "hi", DateTime.Now, default, "user-1");
        Assert.Equal("velvet:user-1", velvet.StackKey);
        Assert.Equal(PhoneNotification.StackKeyOf("aethergram", "user-1"), aethergram.StackKey);
        Assert.NotEqual(velvet.StackKey, aethergram.StackKey);
    }
}
