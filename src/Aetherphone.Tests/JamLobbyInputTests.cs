using Aetherphone.Apps.Music;
using Aetherphone.Apps.Music.Jam;
using Aetherphone.Core.Jam;
using Xunit;

namespace Aetherphone.Tests;

public sealed class JamLobbyInputTests
{
    [Theory]
    [InlineData("abc123", "ABC123")]
    [InlineData("ABC 123", "ABC123")]
    [InlineData("abc-123", "ABC123")]
    [InlineData("  q7x  k2m ", "Q7XK2M")]
    [InlineData("abc", "ABC")]
    [InlineData("abcdefg", "ABCDEF")]
    [InlineData("music:jam:K2M9QX", "K2M9QX")]
    [InlineData("Join my Jam in Aetherphone Music with the code ABC 123", "ABC123")]
    [InlineData("Join my Jam: Q7XK2M", "Q7XK2M")]
    [InlineData("", "")]
    [InlineData("--- !!", "")]
    public void SanitizeKeepsTheCodeFromTypedAndPastedText(string input, string expected)
    {
        Assert.Equal(expected, JamCodeInput.Sanitize(input));
    }

    [Fact]
    public void SanitizeNeverReturnsMoreThanACode()
    {
        Assert.True(JamCodeInput.Sanitize("abcdefghijklmnopqrstuvwxyz0123456789").Length <= JamCodeInput.Length);
    }

    [Fact]
    public void EveryDeclineReasonHasAMessage()
    {
        foreach (var reason in Enum.GetValues<JamDeclineReason>())
        {
            Assert.Equal(reason != JamDeclineReason.None, JamMessages.Decline(reason).HasValue);
        }
    }

    [Fact]
    public void EveryRefusalHasAMessage()
    {
        foreach (var refusal in Enum.GetValues<JamRefusal>())
        {
            Assert.Equal(refusal != JamRefusal.None, JamMessages.Refusal(refusal).HasValue);
        }
    }
}
