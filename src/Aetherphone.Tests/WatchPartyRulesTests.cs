using System.Numerics;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;
using Dalamud.Game.Text;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WatchPartyRulesTests
{
    [Theory]
    [InlineData("ABCDEF", "ABCDEF")]
    [InlineData("abc-def", "ABCDEF")]
    [InlineData(" abc def ", "ABCDEF")]
    [InlineData("a2c 4e6", "A2C4E6")]
    public void AnInviteCodeIsReadHoweverItWasTyped(string typed, string expected)
    {
        Assert.Equal(expected, PartyCode.Normalize(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDE")]
    [InlineData("ABCDEFG")]
    [InlineData("ABC-DEF-GHI-JKL-MNO")]
    public void AnythingThatIsNotSixCharactersIsNotACode(string? typed)
    {
        Assert.Equal(string.Empty, PartyCode.Normalize(typed));
    }

    [Fact]
    public void AnInviteCodeIsShownInTwoGroups()
    {
        Assert.Equal("ABC DEF", PartyCode.Display("ABCDEF"));
        Assert.Equal("ABCD", PartyCode.Display("ABCD"));
    }

    [Fact]
    public void AMemberHoldsTheGuestPermissionsPlusTheirOwnGrant()
    {
        var members = new[]
        {
            new StreamMember("trusted", StreamPermission.ControlPlayback),
            new StreamMember("capable", StreamPermission.CanHost),
        };

        var trusted = PartyPermissions.Held(StreamPermission.AddToQueue, members, "trusted");
        Assert.True(PartyPermissions.Allows(trusted, StreamPermission.AddToQueue));
        Assert.True(PartyPermissions.Allows(trusted, StreamPermission.ControlPlayback));

        var plain = PartyPermissions.Held(StreamPermission.AddToQueue, members, "plain");
        Assert.True(PartyPermissions.Allows(plain, StreamPermission.AddToQueue));
        Assert.False(PartyPermissions.Allows(plain, StreamPermission.ControlPlayback));

        Assert.Equal(0, PartyPermissions.Held(0, null, "anyone"));
    }

    [Fact]
    public void AnIdlePartyStaysOpenForFiveMinutes()
    {
        Assert.Equal(0f, PartyIdle.RemainingSeconds(0, 50_000));
        Assert.False(PartyIdle.Expired(0, 50_000));

        Assert.Equal(300f, PartyIdle.RemainingSeconds(10_000, 10_000));
        Assert.Equal(240f, PartyIdle.RemainingSeconds(10_000, 70_000));
        Assert.False(PartyIdle.Expired(10_000, 309_999));
        Assert.True(PartyIdle.Expired(10_000, 310_000));
        Assert.Equal(0f, PartyIdle.RemainingSeconds(10_000, 999_000));
    }

    [Theory]
    [InlineData("\"C:\\Videos\\show.mkv\"", "C:\\Videos\\show.mkv")]
    [InlineData("  'D:\\clip.mp4'  ", "D:\\clip.mp4")]
    [InlineData(" https://youtu.be/abc ", "https://youtu.be/abc")]
    [InlineData("\"\"", "")]
    [InlineData(null, "")]
    public void PastedInputLosesItsQuotesAndPadding(string? raw, string expected)
    {
        Assert.Equal(expected, MediaInput.Normalize(raw));
    }

    [Theory]
    [InlineData("C:\\Videos\\show.mkv", true)]
    [InlineData("d:/clips/intro.mp4", true)]
    [InlineData("\\\\nas\\share\\film.mkv", true)]
    [InlineData("https://youtu.be/abc", false)]
    [InlineData("youtube.com/watch?v=abc", false)]
    [InlineData("aep-local:1:abc:10:name", false)]
    [InlineData("C:", false)]
    public void ALocalPathIsToldApartFromALink(string input, bool expected)
    {
        Assert.Equal(expected, MediaInput.LooksLikeLocalPath(input));
    }

    [Fact]
    public void ARowSubtitleJoinsTheSourceAndTheLength()
    {
        Assert.Equal("Channel", MediaInput.Subtitle("Channel", null));
        Assert.Equal("Channel", MediaInput.Subtitle("Channel", 0d));
        Assert.StartsWith("Channel", MediaInput.Subtitle("Channel", 125d));
        Assert.EndsWith("2:05", MediaInput.Subtitle("Channel", 125d));
        Assert.Equal("2:05", MediaInput.Subtitle(string.Empty, 125d));
    }

    [Fact]
    public void SoundIsFullUpCloseAndGoneAtTheEdgeOfTheRange()
    {
        Assert.Equal(1f, SpatialVolume.Gain(0f, 40f));
        Assert.Equal(1f, SpatialVolume.Gain(10f, 40f));
        Assert.Equal(0f, SpatialVolume.Gain(40f, 40f));
        Assert.Equal(0f, SpatialVolume.Gain(500f, 40f));

        var near = SpatialVolume.Gain(15f, 40f);
        var far = SpatialVolume.Gain(30f, 40f);
        Assert.InRange(near, 0f, 1f);
        Assert.True(near > far);
        Assert.True(far > 0f);
    }

    [Fact]
    public void AScreenTurnedToFaceSomeonePointsAtThem()
    {
        var screen = new Vector3(10f, 2f, -4f);
        var viewer = new Vector3(13f, 2f, 0f);
        var yaw = ScreenGeometry.YawFacing(screen, viewer);
        var facing = ScreenGeometry.Facing(new ScreenPose(screen, yaw, 0f, 0f, 1f));
        var expected = Vector3.Normalize(viewer - screen);

        Assert.Equal(expected.X, facing.X, 3);
        Assert.Equal(expected.Y, facing.Y, 3);
        Assert.Equal(expected.Z, facing.Z, 3);
    }

    [Fact]
    public void ANudgeMovesAlongTheScreenNotAlongTheWorld()
    {
        var pose = new ScreenPose(new Vector3(1f, 1f, 1f), MathF.PI * 0.5f, 0f, 0f, 2f);
        var right = ScreenGeometry.Right(pose);
        var moved = ScreenGeometry.Nudge(pose, 0.25f, 0f, 0f);

        Assert.Equal(0.25f, Vector3.Distance(pose.Position, moved), 4);
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(moved - pose.Position), right), 4);

        var closer = ScreenGeometry.Nudge(pose, 0f, 0f, 0.5f);
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(closer - pose.Position), ScreenGeometry.Facing(pose)), 4);
    }

    [Fact]
    public void TheCornersSpanTheScaledScreen()
    {
        var pose = new ScreenPose(Vector3.Zero, 0f, 0f, 0f, 3f);
        var topLeft = ScreenGeometry.Corner(pose, -1f, 1f);
        var bottomRight = ScreenGeometry.Corner(pose, 1f, -1f);

        Assert.Equal(ScreenGeometry.HalfWidth * 2f * 3f, bottomRight.X - topLeft.X, 4);
        Assert.Equal(ScreenGeometry.HalfHeight * 2f * 3f, topLeft.Y - bottomRight.Y, 4);
    }

    [Fact]
    public void ADragAlongAnArmTurnsPixelsIntoWorldDistance()
    {
        var start = new Vector2(100f, 100f);
        var end = new Vector2(200f, 100f);

        Assert.Equal(0.7f, ScreenGeometry.AlongAxis(new Vector2(100f, 0f), start, end, 0.7f), 4);
        Assert.Equal(-0.35f, ScreenGeometry.AlongAxis(new Vector2(-50f, 30f), start, end, 0.7f), 4);
        Assert.Equal(0f, ScreenGeometry.AlongAxis(new Vector2(0f, 40f), start, end, 0.7f), 4);
        Assert.Equal(0f, ScreenGeometry.AlongAxis(new Vector2(40f, 0f), start, start, 0.7f), 4);
    }

    [Fact]
    public void APlaceIsKeyedByZoneWorldAndHouse()
    {
        Assert.Equal("339:54:12:30:0", ScreenPlaces.Key(339, 54, 12, 30, 0));
        Assert.NotEqual(ScreenPlaces.Key(339, 54, 12, 30, 0), ScreenPlaces.Key(339, 54, 12, 31, 0));
    }

    [Theory]
    [InlineData(10d, 600d, 0d)]
    [InlineData(120d, 600d, 120d)]
    [InlineData(585d, 600d, 0d)]
    [InlineData(120d, 0d, 120d)]
    public void ResumeSkipsTheVeryStartAndTheVeryEnd(double position, double duration, double expected)
    {
        Assert.Equal(expected, VideoLibrary.ResumeFrom(position, duration));
    }

    [Fact]
    public void HistoryKeepsTheNewestFirstAndCarriesTheResumePoint()
    {
        var configuration = new Configuration();
        var library = new VideoLibrary(configuration);
        var first = new VideoQueueEntry("https://example.com/one.mp4", "One", "Site", TimeSpan.FromMinutes(10), null);
        var second = new VideoQueueEntry("https://example.com/two.mp4", "Two", "Site", null, null);

        library.NotePlayed(first);
        library.NotePosition(first.Url, 200d, 600d);
        library.NotePlayed(second);
        library.NotePlayed(first);

        Assert.Equal(2, library.History.Count);
        Assert.Equal(first.Url, library.History[0].Url);
        Assert.Equal(200d, library.History[0].PositionSeconds);

        library.NoteFinished(first.Url);
        Assert.Equal(0d, library.History[0].PositionSeconds);
    }

    [Fact]
    public void AFileSharedByFingerprintStaysOutOfHistory()
    {
        var configuration = new Configuration();
        var library = new VideoLibrary(configuration);
        library.NotePlayed(new VideoQueueEntry("aep-local:1:abc:10:name", "Name", "Local file", null, null));

        Assert.Empty(library.History);
    }

    [Fact]
    public void AReactionRisesForItsLifetimeAndThenIsGone()
    {
        var reactions = new PartyReactions();
        reactions.Add(2, 1_000);
        reactions.Add(99, 1_000);

        var live = 0;
        foreach (var reaction in reactions.Active)
        {
            if (PartyReactions.TryProgress(reaction, 1_000 + PartyReactions.LifetimeMilliseconds / 2, out var progress))
            {
                live++;
                Assert.Equal(2, reaction.Kind);
                Assert.Equal(0.5f, progress, 2);
                Assert.InRange(reaction.Lane, 0f, 1f);
            }
        }

        Assert.Equal(1, live);
        foreach (var reaction in reactions.Active)
        {
            Assert.False(PartyReactions.TryProgress(reaction, 1_000 + PartyReactions.LifetimeMilliseconds, out _));
        }
    }

    [Fact]
    public void TracksAreOfferedOnlyWhenThereIsSomethingToChoose()
    {
        var single = new[]
        {
            new MediaTrack(1, MediaTrackKind.Video, string.Empty, string.Empty, "h264", true),
            new MediaTrack(1, MediaTrackKind.Audio, string.Empty, string.Empty, "aac", true),
        };
        var dubbed = new[]
        {
            new MediaTrack(1, MediaTrackKind.Audio, "Original", "jpn", "flac", true),
            new MediaTrack(2, MediaTrackKind.Audio, string.Empty, "eng", "aac", false),
            new MediaTrack(1, MediaTrackKind.Subtitle, "Signs", string.Empty, "ass", false),
        };

        Assert.False(MediaTracks.OffersChoice(single));
        Assert.True(MediaTracks.OffersChoice(dubbed));
        Assert.Equal(2, MediaTracks.Count(dubbed, MediaTrackKind.Audio));
        Assert.Equal("Original (JPN)", MediaTracks.Label(dubbed[0], "Track 1"));
        Assert.Equal("ENG", MediaTracks.Label(dubbed[1], "Track 2"));
        Assert.Equal("Signs", MediaTracks.Label(dubbed[2], "Track 1"));
        Assert.Equal("Track 1", MediaTracks.Label(single[1], "Track 1"));
    }

    [Fact]
    public void TrackKindsAreReadFromThePlayer()
    {
        Assert.Equal(MediaTrackKind.Audio, MediaTracks.KindOf("audio"));
        Assert.Equal(MediaTrackKind.Subtitle, MediaTracks.KindOf("sub"));
        Assert.Equal(MediaTrackKind.Video, MediaTracks.KindOf("video"));
        Assert.Null(MediaTracks.KindOf("attachment"));
        Assert.Null(MediaTracks.KindOf(null));
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLFgquLnL59alCl_2TQvOiD5Vgm1hCaGSI", true, false)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLFgquLnL59alCl_2TQvOiD5Vgm1hCaGSI", true, true)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", false, true)]
    [InlineData("https://example.com/video.mp4", false, false)]
    [InlineData("hello", false, false)]
    public void APlaylistLinkIsRecognized(string url, bool playlist, bool namesOneVideo)
    {
        Assert.Equal(playlist, VideoUrlResolver.IsPlaylistUrl(url));
        Assert.Equal(namesOneVideo, VideoUrlResolver.NamesOneVideo(url));
    }

    [Fact]
    public void ChatBubblesCarryOnlyTheChannelsThatWereChosen()
    {
        Assert.Equal(ScreenChatChannels.Say, ScreenChatChannels.Of(XivChatType.Say));
        Assert.Equal(ScreenChatChannels.Party, ScreenChatChannels.Of(XivChatType.Party));
        Assert.Equal(ScreenChatChannels.Party, ScreenChatChannels.Of(XivChatType.CrossParty));
        Assert.Equal(ScreenChatChannels.Party, ScreenChatChannels.Of(XivChatType.Alliance));
        Assert.Equal(ScreenChatChannels.FreeCompany, ScreenChatChannels.Of(XivChatType.FreeCompany));
        Assert.Equal(ScreenChatChannels.Shout, ScreenChatChannels.Of(XivChatType.Yell));
        Assert.Equal(0, ScreenChatChannels.Of(XivChatType.TellIncoming));
        Assert.Equal(0, ScreenChatChannels.Of(XivChatType.Echo));
        Assert.Equal(0, ScreenChatChannels.Default & ScreenChatChannels.Shout);
    }

    [Fact]
    public void ChatTextLosesGameIconGlyphsAndStaysShort()
    {
        var withIcon = "hello" + (char)0xE040 + " there";
        Assert.Equal("hello there", ScreenChatFeed.Clean(withIcon));
        Assert.Equal(string.Empty, ScreenChatFeed.Clean(null));
        Assert.Equal(140, ScreenChatFeed.Clean(new string('a', 400)).Length);
    }

    [Fact]
    public void AChatLineFadesOutOverItsLifetime()
    {
        var line = new ScreenChatLine("Y'shtola", "over here", 5_000);

        Assert.True(ScreenChatFeed.TryAge(line, 5_000 + ScreenChatFeed.LifetimeMilliseconds / 2, out var age));
        Assert.Equal(0.5f, age, 2);
        Assert.False(ScreenChatFeed.TryAge(line, 5_000 + ScreenChatFeed.LifetimeMilliseconds, out _));
        Assert.False(ScreenChatFeed.TryAge(default, 5_000, out _));
    }

    [Fact]
    public void ACurvedScreenBringsItsEdgesTowardTheViewer()
    {
        var pose = new ScreenPose(Vector3.Zero, 0f, 0f, 0f, 3f);

        Assert.Equal(0.24f * 3f, ScreenGeometry.Corner(pose, 1f, 1f, 0.24f).Z, 4);
        Assert.Equal(0.24f * 3f, ScreenGeometry.Corner(pose, -1f, -1f, 0.24f).Z, 4);
        Assert.Equal(0f, ScreenGeometry.Corner(pose, 0f, 1f, 0.24f).Z, 4);
        Assert.Equal(0f, ScreenGeometry.Corner(pose, 1f, 1f).Z, 4);
    }

    [Fact]
    public void TheRoomRulesReadFromSettingsAndNeverGrantHosting()
    {
        var configuration = new Configuration
        {
            VideoStreamApprovalRequired = true,
            VideoStreamDiscoverable = false,
            VideoPartyCodeEnabled = true,
            VideoPartyGuestsCanAdd = true,
            VideoPartyGuestsCanControl = false,
        };

        var policy = PartyPolicy.From(configuration);

        Assert.True(policy.ApprovalRequired);
        Assert.False(policy.Discoverable);
        Assert.True(policy.CodeEnabled);
        Assert.True(policy.GuestsCanAdd);
        Assert.False(policy.GuestsCanControl);

        var widened = policy.WithGuestPermission(StreamPermission.ControlPlayback, true);
        Assert.True(widened.GuestsCanControl);
        Assert.True(widened.GuestsCanAdd);
        Assert.False(widened.WithGuestPermission(StreamPermission.AddToQueue, false).GuestsCanAdd);
        Assert.Equal(0, PartyPolicy.GuestMask & StreamPermission.CanHost);
    }

    [Fact]
    public void ASavedQueueNeverTakesANameThatIsInUse()
    {
        var configuration = new Configuration();
        configuration.VideoPlaylists.Add(new VideoPlaylistRecord { Name = "Playlist 2" });
        configuration.VideoPlaylists.Add(new VideoPlaylistRecord { Name = "playlist 3" });
        var library = new VideoLibrary(configuration);

        Assert.Equal("Playlist 4",
            library.NextPlaylistName("Playlist {0}", System.Globalization.CultureInfo.InvariantCulture));
    }
}
