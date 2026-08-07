using Aetherphone.Core.Muster;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MusterShareTests
{
    [Fact]
    public void ComposeRoundTripsThroughTryParse()
    {
        var token = MusterShare.Compose("a3f2b81c9d4e40f2a1b2c3d4e5f60718");

        Assert.True(MusterShare.TryParse(token, out var musterId));
        Assert.Equal("a3f2b81c9d4e40f2a1b2c3d4e5f60718", musterId);
    }

    [Fact]
    public void WhitespaceAroundTheTokenStillParses()
    {
        var token = "  " + MusterShare.Compose("a3f2b81c9d4e40f2a1b2c3d4e5f60718") + "\n";

        Assert.True(MusterShare.TryParse(token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[aep.muster.v1:]")]
    [InlineData("[aep.muster.v1:abc")]
    [InlineData("aep.muster.v1:abc]")]
    [InlineData("[aep.loc.v1:129;12;9.6;11.2;33;0;0;0]")]
    [InlineData("look at [aep.muster.v1:a3f2b81c9d4e40f2] this")]
    [InlineData("[aep.muster.v1:../../etc/passwd]")]
    [InlineData("[aep.muster.v1:abc def]")]
    public void InvalidBodiesDoNotParse(string? body)
    {
        Assert.False(MusterShare.TryParse(body, out _));
    }

    [Fact]
    public void MixedTextContainingATokenStaysPlain()
    {
        var body = "join us " + MusterShare.Compose("a3f2b81c9d4e40f2a1b2c3d4e5f60718");

        Assert.False(MusterShare.IsToken(body));
    }

    [Fact]
    public void AnOverlongIdentifierIsRejected()
    {
        var token = MusterShare.Compose(new string('a', 65));

        Assert.False(MusterShare.TryParse(token, out _));
    }
}
