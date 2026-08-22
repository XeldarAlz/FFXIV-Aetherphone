using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntWindowDtoTests
{
    [Fact]
    public void StartedAtPrefersSnipedTimestampWhenBothArePresent()
    {
        var window = new HuntWindowDto
        {
            StartedAtNormal = new DateTimeOffset(2026, 8, 16, 1, 31, 55, TimeSpan.Zero),
            StartedAtSniped = new DateTimeOffset(2026, 8, 18, 18, 31, 55, TimeSpan.Zero),
        };

        Assert.Equal(window.StartedAtSniped, window.StartedAt);
        Assert.True(window.IsSniped);
    }

    [Fact]
    public void StartedAtFallsBackToNormalWhenNotSniped()
    {
        var window = new HuntWindowDto
        {
            StartedAtNormal = new DateTimeOffset(2026, 8, 16, 1, 31, 55, TimeSpan.Zero),
        };

        Assert.Equal(window.StartedAtNormal, window.StartedAt);
        Assert.False(window.IsSniped);
    }
}
