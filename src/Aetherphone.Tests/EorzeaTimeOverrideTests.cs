using Aetherphone.Core.Game;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EorzeaTimeOverrideTests
{
    private const long SecondsPerDay = 86400;
    private const long NaturalSeconds = 1_000 * SecondsPerDay + 9 * 3600;
    private const long NightOverride = 1_000 * SecondsPerDay + 23 * 3600;

    [Fact]
    public void AnActiveOverrideReplacesTheNaturalClock()
    {
        Assert.Equal(NightOverride, EorzeaTime.RenderedSeconds(NaturalSeconds, NightOverride, true));
    }

    [Fact]
    public void AnOverrideLeftBehindAnInactiveFlagShiftsTheRenderedClock()
    {
        var rendered = EorzeaTime.RenderedSeconds(NaturalSeconds, NightOverride, false);

        Assert.NotEqual(NaturalSeconds % SecondsPerDay, rendered % SecondsPerDay);
        Assert.True(EorzeaTime.HasStrayOverrideOffset(NightOverride, false));
    }

    [Fact]
    public void ReleasingTheOverrideHandsTheSkyBackToTheNaturalClock()
    {
        var overrideSeconds = NightOverride;
        var overridden = true;

        EorzeaTime.ReleaseOverride(ref overrideSeconds, ref overridden);

        Assert.False(overridden);
        Assert.Equal(0, overrideSeconds);
        Assert.Equal(NaturalSeconds, EorzeaTime.RenderedSeconds(NaturalSeconds, overrideSeconds, overridden));
        Assert.False(EorzeaTime.HasStrayOverrideOffset(overrideSeconds, overridden));
    }

    [Theory]
    [InlineData(0L, false)]
    [InlineData(0L, true)]
    [InlineData(NightOverride, true)]
    public void OnlyAnOffsetBehindAnInactiveFlagCountsAsStray(long overrideSeconds, bool overridden)
    {
        Assert.False(EorzeaTime.HasStrayOverrideOffset(overrideSeconds, overridden));
    }
}
