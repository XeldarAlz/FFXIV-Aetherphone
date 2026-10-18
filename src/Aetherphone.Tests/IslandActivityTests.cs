using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Songs;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IslandActivityTests
{
    private const long Now = 1_800_000_000L;
    private const long Minute = 60L;
    private const long UpdatedTicks = 638_000_000_000_000_000L;

    [Fact]
    public void OnlyPausedMediaSwipesAway()
    {
        Assert.True(IslandActivities.Swipeable(IslandActivity.Playback, false));
        Assert.True(IslandActivities.Swipeable(IslandActivity.PcMedia, false));
        Assert.False(IslandActivities.Swipeable(IslandActivity.Playback, true));
        Assert.False(IslandActivities.Swipeable(IslandActivity.PcMedia, true));
        for (var activity = IslandActivity.None; activity <= IslandActivity.Fishing; activity++)
        {
            if (activity is IslandActivity.Playback or IslandActivity.PcMedia)
            {
                continue;
            }

            Assert.False(IslandActivities.Swipeable(activity, false));
        }
    }

    [Theory]
    [InlineData(48f, 0f, 1f, true)]
    [InlineData(47f, 0f, 1f, false)]
    [InlineData(20f, 700f, 1f, true)]
    [InlineData(20f, 699f, 1f, false)]
    [InlineData(0f, 900f, 1f, false)]
    [InlineData(60f, 0f, 1.5f, false)]
    [InlineData(72f, 0f, 1.5f, true)]
    public void ASwipeDismissesOnDistanceOrAFlick(float offset, float velocity, float scale, bool dismisses)
    {
        Assert.Equal(dismisses, IslandActivities.SwipeDismisses(offset, velocity, scale));
    }

    [Fact]
    public void MediaCardsAreTheOnlyTallerCards()
    {
        Assert.Equal(IslandActivities.PlaybackCardHeightUnits,
            IslandActivities.ExpandedHeightUnits(IslandActivity.Playback));
        Assert.Equal(IslandActivities.PlaybackCardHeightUnits,
            IslandActivities.ExpandedHeightUnits(IslandActivity.PcMedia));
        Assert.Equal(IslandActivities.PlaybackCardHeightUnits,
            IslandActivities.ExpandedHeightUnits(IslandActivity.Session));
        Assert.True(IslandActivities.PlaybackCardHeightUnits > IslandActivities.CardHeightUnits);
        for (var activity = IslandActivity.None; activity <= IslandActivity.Fishing; activity++)
        {
            if (activity is IslandActivity.Playback or IslandActivity.PcMedia or IslandActivity.Session)
            {
                continue;
            }

            Assert.Equal(IslandActivities.CardHeightUnits, IslandActivities.ExpandedHeightUnits(activity));
        }
    }

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(false, true, true, true, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, false, false, false, false)]
    public void VideoShowsWhenPlayingInAPartyOrWhenNoMusicPlays(bool inParty, bool loaded, bool playing,
        bool musicPlaying, bool expected)
    {
        Assert.Equal(expected, IslandActivities.VideoLive(inParty, loaded, playing, musicPlaying));
    }

    [Fact]
    public void PlayingVideoOutranksPhoneMusic()
    {
        var session = IslandActivities.VideoLive(false, true, true, true);

        Assert.Equal(IslandActivity.Session,
            IslandActivities.Select(new IslandSignals(false, session, true, false, false)));
    }

    [Fact]
    public void PausedVideoGivesWayToPlayingMusic()
    {
        var session = IslandActivities.VideoLive(false, true, false, true);

        Assert.Equal(IslandActivity.Playback,
            IslandActivities.Select(new IslandSignals(false, session, true, false, false)));
    }

    [Fact]
    public void DrivableVideoCarriesSeekPlayPauseAndNext()
    {
        var media = IslandMedia.ForVideo("Video", "Site", 30f, 600f, true, true, true, true);

        Assert.True(media.HasTimeline);
        Assert.True(media.Has(IslandMediaControls.Playing | IslandMediaControls.PlayPause |
                              IslandMediaControls.Seek | IslandMediaControls.Next));
        Assert.False(media.Has(IslandMediaControls.Previous));
        Assert.False(media.Has(IslandMediaControls.Shuffle));
        Assert.False(media.Has(IslandMediaControls.Repeat));
    }

    [Fact]
    public void GuestWithoutControlSeesAReadOnlyTimeline()
    {
        var media = IslandMedia.ForVideo("Video", "Party", 30f, 600f, true, true, false, true);

        Assert.True(media.HasTimeline);
        Assert.Equal(IslandMediaControls.Playing, media.Controls);
    }

    [Fact]
    public void LiveStreamHidesTheTimelineButKeepsPlayPause()
    {
        var media = IslandMedia.ForVideo("Stream", "Site", 42f, 0f, true, true, true, false);

        Assert.False(media.HasTimeline);
        Assert.Equal(0f, media.Position);
        Assert.True(media.Has(IslandMediaControls.PlayPause));
        Assert.False(media.Has(IslandMediaControls.Seek));
        Assert.False(media.Has(IslandMediaControls.Next));
    }

    [Fact]
    public void PartyWithoutAVideoOffersNoControls()
    {
        var media = IslandMedia.ForVideo("MogCast", "Hosting", 0f, 0f, false, false, true, true);

        Assert.False(media.HasTimeline);
        Assert.Equal(IslandMediaControls.None, media.Controls);
    }

    [Fact]
    public void VideoPositionStaysInsideItsDuration()
    {
        Assert.Equal(600f, IslandMedia.ForVideo("Video", "Site", 640f, 600f, true, true, true, false).Position);
        Assert.Equal(0f, IslandMedia.ForVideo("Video", "Site", -3f, 600f, true, true, true, false).Position);
    }

    [Theory]
    [InlineData((byte)PhoneCaseKind.Art)]
    [InlineData((byte)PhoneCaseKind.Color)]
    public void ShuffleTransportAndRepeatFitTheExpandedCard(byte kind)
    {
        var screenWidth = BetDeckLayoutTests.ScreenWidth((PhoneCaseKind)kind);
        var halfWidth = IslandActivities.ExpandedHalfWidth(screenWidth, 1f);
        var reach = IslandActivities.ModeStrideUnits + IslandActivities.ModeRadiusUnits;

        Assert.True(reach <= halfWidth - IslandActivities.CardPadUnits, $"{reach} > {halfWidth}");
        Assert.True(IslandActivities.ModeStrideUnits - IslandActivities.ModeRadiusUnits >=
                    IslandActivities.TransportStrideUnits + IslandActivities.SideControlRadiusUnits);
    }

    [Fact]
    public void PhoneSongCarriesTimelineShuffleAndRepeat()
    {
        var media = IslandMedia.ForPhone("Song", "Artist", 30f, 200f, true, true, true, SongRepeatMode.One);

        Assert.True(media.HasTimeline);
        Assert.True(media.Has(IslandMediaControls.Playing | IslandMediaControls.Seek));
        Assert.True(media.Has(IslandMediaControls.Shuffle | IslandMediaControls.ShuffleActive));
        Assert.True(media.Has(IslandMediaControls.Repeat | IslandMediaControls.RepeatActive |
                              IslandMediaControls.RepeatOne));
    }

    [Fact]
    public void PhoneRadioKeepsTheTransportButHidesTimelineAndModes()
    {
        var media = IslandMedia.ForPhone("Station", "Now playing", 0f, 0f, true, false, true, SongRepeatMode.All);

        Assert.False(media.HasTimeline);
        Assert.True(media.Has(IslandMediaControls.Previous | IslandMediaControls.PlayPause |
                              IslandMediaControls.Next));
        Assert.False(media.Has(IslandMediaControls.Shuffle));
        Assert.False(media.Has(IslandMediaControls.Repeat));
    }

    [Fact]
    public void SessionThatRefusesSeekStillShowsItsTimes()
    {
        var media = IslandMedia.ForSession(Session(MediaSessionControls.PlayPauseToggle, 200d), "Song", "Artist",
            UpdatedTicks);

        Assert.True(media.HasTimeline);
        Assert.False(media.Has(IslandMediaControls.Seek));
    }

    [Fact]
    public void SessionWithoutDurationHidesTheTimeline()
    {
        var media = IslandMedia.ForSession(Session(MediaSessionControls.Seek, 0d), "Song", "Artist", UpdatedTicks);

        Assert.False(media.HasTimeline);
        Assert.False(media.Has(IslandMediaControls.Seek));
        Assert.Equal(0f, media.Position);
    }

    [Fact]
    public void SessionPositionAdvancesWhilePlaying()
    {
        var media = IslandMedia.ForSession(Session(MediaSessionControls.Seek, 200d), "Song", "Artist",
            UpdatedTicks + TimeSpan.FromSeconds(5).Ticks);

        Assert.True(media.Has(IslandMediaControls.Seek));
        Assert.Equal(15f, media.Position, 3);
        Assert.Equal(200f, media.Duration, 3);
    }

    [Fact]
    public void SessionModesFollowWhatTheAppSupports()
    {
        var supported = IslandMedia.ForSession(
            Session(MediaSessionControls.Shuffle | MediaSessionControls.Repeat, 200d, true, MediaSessionRepeat.Track),
            "Song", "Artist", UpdatedTicks);
        var unsupported = IslandMedia.ForSession(Session(MediaSessionControls.None, 200d, true,
            MediaSessionRepeat.List), "Song", "Artist", UpdatedTicks);

        Assert.True(supported.Has(IslandMediaControls.Shuffle | IslandMediaControls.ShuffleActive));
        Assert.True(supported.Has(IslandMediaControls.Repeat | IslandMediaControls.RepeatActive |
                                  IslandMediaControls.RepeatOne));
        Assert.Equal(IslandMediaControls.Playing, unsupported.Controls);
    }

    [Fact]
    public void CornerRadiusRoundsPillsFullyAndCapsTallCards()
    {
        Assert.Equal(18f, IslandActivities.CornerRadius(36f, 1f));
        Assert.Equal(48f, IslandActivities.CornerRadius(IslandActivities.CardHeightUnits, 1f));
        Assert.Equal(48f, IslandActivities.CornerRadius(IslandActivities.PlaybackCardHeightUnits, 1f));
        Assert.Equal(96f, IslandActivities.CornerRadius(IslandActivities.PlaybackCardHeightUnits * 2f, 2f));
    }

    [Fact]
    public void RemainingSecondsCountsWholeSecondsLeftAndNeverGoesNegative()
    {
        Assert.Equal(91, IslandActivities.RemainingSeconds(185f, 276f));
        Assert.Equal(92, IslandActivities.RemainingSeconds(185.6f, 276.4f));
        Assert.Equal(0, IslandActivities.RemainingSeconds(300f, 276f));
        Assert.Equal(0, IslandActivities.RemainingSeconds(10f, 0f));
        Assert.Equal(276, IslandActivities.RemainingSeconds(-5f, 276f));
    }

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
    [InlineData("aetherstream", true)]
    [InlineData("music", true)]
    [InlineData("clock", true)]
    [InlineData("muster", true)]
    [InlineData("timers", true)]
    [InlineData("fishing", true)]
    [InlineData("message", false)]
    [InlineData("notes", false)]
    [InlineData("", false)]
    public void OnlyNonCallActivitiesOfferAnIslandToggle(string appId, bool expected)
    {
        Assert.Equal(expected, IslandActivities.HasOptionalActivity(appId));
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

    private static MediaSessionSnapshot Session(MediaSessionControls controls, double durationSeconds,
        bool shuffleActive = false, MediaSessionRepeat repeat = MediaSessionRepeat.None) =>
        new("app", "App", "Song", "Artist", string.Empty, MediaSessionPlayback.Playing, controls,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(durationSeconds), UpdatedTicks, null, 0, shuffleActive,
            repeat, MediaSessionSnapshot.NoVolume);

    private static MusterDto Muster(string id, long startsAtUnix) =>
        new(id, "host", "Host Character", "Phoenix", 0, string.Empty, 0, 0, 0f, 0f, 0, 0, 0, 0, string.Empty, 0, 0,
            startsAtUnix, startsAtUnix + 3600, 1, 8, false, true, true, 0, 0, Now);
}
