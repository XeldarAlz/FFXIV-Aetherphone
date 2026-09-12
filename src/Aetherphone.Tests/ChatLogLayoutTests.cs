using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatLogLayoutTests
{
    private const int ShortCodeMaxLength = 4;

    [Fact]
    public void LogKeepsTheStoredValueOfTheOldCompactDensity()
    {
        Assert.Equal(0, (int)ChatDensity.Log);
        Assert.Equal(1, (int)ChatDensity.Bubbles);
    }

    [Fact]
    public void EveryChannelHasAUniqueShortCode()
    {
        var channels = GameChannels.All;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < channels.Length; index++)
        {
            var code = GameChannels.ShortCode(channels[index]);
            Assert.False(string.IsNullOrWhiteSpace(code), $"{channels[index].Key} has no short code");
            Assert.True(code.Length <= ShortCodeMaxLength, $"{channels[index].Key} short code {code} is too long");
            Assert.True(seen.Add(code), $"short code {code} is used twice");
        }
    }

    [Theory]
    [InlineData("say", "Say")]
    [InlineData("fc", "FC")]
    [InlineData("ls1", "LS1")]
    [InlineData("cwls8", "CW8")]
    [InlineData("tell", "Tell")]
    public void ShortCodesReadLikeTheGamesMarkers(string key, string expected)
    {
        Assert.True(GameChannels.TryByKey(key, out var channel));
        Assert.Equal(expected, GameChannels.ShortCode(channel));
    }
}
