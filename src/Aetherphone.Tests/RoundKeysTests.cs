using Aetherphone.Apps.Casino.Cabinets;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RoundKeysTests
{
    [Fact]
    public void KeysReadRoomHashRoundAndAreReusedWhileTheRoundLasts()
    {
        var first = RoundKeys.Of("wheel-floor", 42);
        Assert.Equal("wheel-floor#42", first);
        Assert.Same(first, RoundKeys.Of("wheel-floor", 42));
        Assert.Equal("bingo-hall#42", RoundKeys.Of("bingo-hall", 42));
        Assert.Same(first, RoundKeys.Of("wheel-floor", 42));
        Assert.Equal("wheel-floor#43", RoundKeys.Of("wheel-floor", 43));
    }
}
