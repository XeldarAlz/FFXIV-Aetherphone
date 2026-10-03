using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Shell;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IslandActivityTests
{
    private const long Now = 1_800_000_000L;
    private const long Minute = 60L;

    [Fact]
    public void CallOutranksEveryOtherActivity()
    {
        Assert.Equal(IslandActivity.Call, IslandActivities.Select(new IslandSignals(true, true, true, true, true)));
    }

    [Fact]
    public void SessionOutranksPlaybackTimerAndMuster()
    {
        Assert.Equal(IslandActivity.Session, IslandActivities.Select(new IslandSignals(false, true, true, true, true)));
    }

    [Fact]
    public void PlaybackOutranksTimerAndMuster()
    {
        Assert.Equal(IslandActivity.Playback,
            IslandActivities.Select(new IslandSignals(false, false, true, true, true)));
    }

    [Fact]
    public void PhonePlaybackOutranksPcMedia()
    {
        Assert.Equal(IslandActivity.Playback,
            IslandActivities.Select(new IslandSignals(false, false, true, false, false, true)));
    }

    [Fact]
    public void PcMediaOutranksTimerAndMuster()
    {
        Assert.Equal(IslandActivity.PcMedia,
            IslandActivities.Select(new IslandSignals(false, false, false, true, true, true)));
    }

    [Fact]
    public void CallAndSessionOutrankPcMedia()
    {
        Assert.Equal(IslandActivity.Call, IslandActivities.Select(new IslandSignals(true, false, false, false, false, true)));
        Assert.Equal(IslandActivity.Session,
            IslandActivities.Select(new IslandSignals(false, true, false, false, false, true)));
    }

    [Fact]
    public void TimerOutranksMuster()
    {
        Assert.Equal(IslandActivity.Timer, IslandActivities.Select(new IslandSignals(false, false, false, true, true)));
    }

    [Fact]
    public void MusterShowsWhenItIsTheOnlyActivity()
    {
        Assert.Equal(IslandActivity.Muster,
            IslandActivities.Select(new IslandSignals(false, false, false, false, true)));
    }

    [Fact]
    public void FishingRanksBelowMuster()
    {
        Assert.Equal(IslandActivity.Muster,
            IslandActivities.Select(new IslandSignals(false, false, false, false, true, Fishing: true)));
        Assert.Equal(IslandActivity.GameTimer,
            IslandActivities.Select(new IslandSignals(false, false, false, false, false, GameTimer: true,
                Fishing: true)));
        Assert.Equal(IslandActivity.Fishing,
            IslandActivities.Select(new IslandSignals(false, false, false, false, false, Fishing: true)));
    }

    [Fact]
    public void NothingLiveSelectsNone()
    {
        Assert.Equal(IslandActivity.None, IslandActivities.Select(new IslandSignals(false, false, false, false, false)));
    }

    [Theory]
    [InlineData((byte)IslandActivity.Call, "message")]
    [InlineData((byte)IslandActivity.Session, "aetherstream")]
    [InlineData((byte)IslandActivity.Playback, "music")]
    [InlineData((byte)IslandActivity.PcMedia, "music")]
    [InlineData((byte)IslandActivity.Timer, "clock")]
    [InlineData((byte)IslandActivity.Muster, "muster")]
    [InlineData((byte)IslandActivity.Fishing, "fishing")]
    [InlineData((byte)IslandActivity.None, "")]
    public void EveryActivityNamesItsOwningApp(byte activity, string expected)
    {
        Assert.Equal(expected, IslandActivities.OwnerAppId((IslandActivity)activity));
    }

    [Theory]
    [InlineData(3600L, true)]
    [InlineData(3601L, false)]
    [InlineData(1L, true)]
    [InlineData(0L, true)]
    [InlineData(-299L, true)]
    [InlineData(-300L, false)]
    [InlineData(-7200L, false)]
    public void MusterWindowCoversTheNextHourAndAShortGraceAfterTheStart(long untilStart, bool expected)
    {
        Assert.Equal(expected, IslandActivities.MusterInWindow(Now + untilStart, Now));
    }

    [Fact]
    public void SoonestMusterPicksTheEarliestStartInsideTheWindow()
    {
        var going = new[]
        {
            Muster("later", Now + 50 * Minute), Muster("soon", Now + 10 * Minute), Muster("far", Now + 90 * Minute),
        };

        var picked = IslandActivities.SoonestMuster(going, Muster("mine", Now + 30 * Minute), Now);

        Assert.NotNull(picked);
        Assert.Equal("soon", picked!.Id);
    }

    [Fact]
    public void SoonestMusterIncludesTheOneTheUserHosts()
    {
        var picked = IslandActivities.SoonestMuster(Array.Empty<MusterDto>(), Muster("mine", Now + 20 * Minute), Now);

        Assert.NotNull(picked);
        Assert.Equal("mine", picked!.Id);
    }

    [Fact]
    public void SoonestMusterIgnoresStartsBeyondTheHourAndLongPast()
    {
        var going = new[] { Muster("far", Now + 61 * Minute), Muster("past", Now - 6 * Minute) };

        Assert.Null(IslandActivities.SoonestMuster(going, null, Now));
    }

    [Theory]
    [InlineData(0, 40, 0)]
    [InlineData(4, 40, 3)]
    [InlineData(4, 120, 3)]
    [InlineData(4, 121, 2)]
    [InlineData(2, 260, 2)]
    [InlineData(4, 261, 1)]
    [InlineData(1, 900, 1)]
    public void WifiArcsFollowLatencyOnceAnySignalExists(int bars, int latency, int expected)
    {
        Assert.Equal(expected, StatusIcons.WifiArcs(bars, latency));
    }

    private static MusterDto Muster(string id, long startsAtUnix) =>
        new(id, "host", "Host Character", "Phoenix", 0, string.Empty, 0, 0, 0f, 0f, 0, 0, 0, 0, string.Empty, 0, 0,
            startsAtUnix, startsAtUnix + 3600, 1, 8, false, true, true, 0, 0, Now);
}
