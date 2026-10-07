using Aetherphone.Core.Games;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LeaderboardSupportTests
{
    [Fact]
    public void AReplyMeansTheServerHasLeaderboards()
    {
        var next = LeaderboardStore.SupportAfter(LeaderboardSupport.Unknown, true, AepFailure.None);

        Assert.Equal(LeaderboardSupport.Supported, next);
    }

    [Fact]
    public void ANotFoundMeansTheServerHasNoLeaderboardsYet()
    {
        var notFound = new AepFailure(AepFailureKind.Server, 404, "not_found", null, null, null);

        var next = LeaderboardStore.SupportAfter(LeaderboardSupport.Unknown, false, notFound);

        Assert.Equal(LeaderboardSupport.Unsupported, next);
    }

    [Theory]
    [InlineData((byte)AepFailureKind.Offline, 0)]
    [InlineData((byte)AepFailureKind.Timeout, 0)]
    [InlineData((byte)AepFailureKind.Server, 500)]
    [InlineData((byte)AepFailureKind.Server, 401)]
    public void OtherFailuresKeepWhatWasKnown(byte kind, int status)
    {
        var failure = new AepFailure((AepFailureKind)kind, status, null, null, null, null);

        Assert.Equal(LeaderboardSupport.Unknown,
            LeaderboardStore.SupportAfter(LeaderboardSupport.Unknown, false, failure));
        Assert.Equal(LeaderboardSupport.Supported,
            LeaderboardStore.SupportAfter(LeaderboardSupport.Supported, false, failure));
    }
}
