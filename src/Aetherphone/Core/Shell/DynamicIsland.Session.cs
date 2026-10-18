using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Shell;

internal sealed partial class DynamicIsland
{
    private readonly RemoteImageCache? images;
    private readonly HttpService? http;
    private string? sessionThumbUrl;
    private string sessionThumbKey = string.Empty;
    private Func<CancellationToken, Task<byte[]?>>? sessionThumbFetch;

    private bool ReadVideo(string? foregroundAppId, bool musicPlaying)
    {
        if (!Allows(IslandActivity.Session) || video is not { } suite)
        {
            return false;
        }

        var player = suite.Player;
        var onScreen = string.Equals(foregroundAppId, IslandActivities.OwnerAppId(IslandActivity.Session),
            StringComparison.Ordinal);
        return IslandActivities.VideoLive(suite.WatchAlong.InParty, player.HasMedia && !onScreen, player.Unpaused,
            musicPlaying);
    }

    private bool PcMediaPlaying()
    {
        if (pcMedia is null)
        {
            return false;
        }

        ref readonly var current = ref pcMedia.Current;
        return PcMediaSource.IsLive(current) && current.IsPlaying;
    }

    private void DrawSessionCompact(ImDrawListPtr drawList, Vector2 bubbleCenter, float bubbleRadius,
        float trailingRight, float trailingMaxWidth, Rect bounds, float scale, Vector4 accent, float alpha)
    {
        if (video is not { } suite)
        {
            return;
        }

        DrawSessionArt(drawList, CompactArtCenter(bubbleCenter, scale), bubbleRadius * CompactArtFraction, suite,
            accent, alpha);
        if (suite.WatchAlong.InParty)
        {
            DrawLiveLabel(drawList, trailingRight, bounds.Center.Y, trailingMaxWidth, scale, accent, alpha);
            return;
        }

        Equalizer.Draw(drawList, new Vector2(trailingRight - 3f * scale, bounds.Center.Y), scale,
            bounds.Height * 0.44f, clock, accent, alpha, suite.Player.Unpaused);
    }

    private bool DrawSessionExpanded(ImDrawListPtr drawList, Rect bounds, float scale, Vector4 accent, float alpha,
        bool active)
    {
        if (video is not { } suite)
        {
            return false;
        }

        var player = suite.Player;
        var progress = player.Progress;
        var media = IslandMedia.ForVideo(SessionTitle(suite), SessionSubtitle(suite), progress.Position,
            progress.Duration, player.HasMedia, player.Unpaused, CanDriveVideo(suite), VideoHasNext(suite));
        var artCenter = MediaArtCenter(bounds, scale);
        DrawSessionArt(drawList, artCenter, CardIconRadius * scale, suite, accent, alpha);
        return DrawMediaCard(drawList, bounds, artCenter, media, IslandMediaSource.Video, scale, accent, alpha,
            active);
    }

    private void DrawSessionArt(ImDrawListPtr drawList, Vector2 center, float radius, VideoSuite suite,
        Vector4 accent, float alpha)
    {
        var side = radius * 2f;
        var min = center - new Vector2(radius, radius);
        var max = min + new Vector2(side, side);
        var rounding = side * ArtRadiusFraction;
        var texture = SessionThumbnail(SessionEntry(suite), side);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, min, max, rounding, texture.Handle,
                ImGui.GetColorU32(Palette.WithAlpha(Vector4.One, alpha)), uv0, uv1);
            return;
        }

        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(accent, IconBubbleAlpha * alpha)));
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Tv, Palette.WithAlpha(accent, alpha), radius);
    }

    private IDalamudTextureWrap? SessionThumbnail(VideoQueueEntry? entry, float side)
    {
        if (entry is null || images is null || http is null)
        {
            return null;
        }

        if (!string.Equals(entry.Url, sessionThumbUrl, StringComparison.Ordinal))
        {
            sessionThumbUrl = entry.Url;
            sessionThumbFetch = VideoThumbnailResolver.Source(http, entry.Url, out sessionThumbKey);
        }

        return sessionThumbFetch is not null
            ? images.GetKeyed(sessionThumbKey, sessionThumbFetch)
            : images.Sized(entry.ThumbnailUrl, side);
    }

    private static VideoQueueEntry? SessionEntry(VideoSuite suite) =>
        suite.WatchAlong.IsViewing ? suite.WatchAlong.ViewingEntry : suite.Queue.Current;

    private static string SessionTitle(VideoSuite suite)
    {
        var entry = SessionEntry(suite);
        return entry is { Title.Length: > 0 } ? entry.Title : Loc.T(L.Apps.AetherStream);
    }

    private string SessionSubtitle(VideoSuite suite)
    {
        if (suite.WatchAlong.InParty)
        {
            return SessionStatus(suite);
        }

        var entry = SessionEntry(suite);
        return entry is { Source.Length: > 0 } ? entry.Source : Loc.T(L.Apps.AetherStream);
    }

    private string SessionStatus(VideoSuite suite)
    {
        if (suite.Player.State == VideoPlaybackState.Paused)
        {
            return Loc.T(L.Music.Paused);
        }

        var count = suite.WatchAlong.Roster.Count;
        if (count > 1)
        {
            return ViewersText(count);
        }

        return suite.WatchAlong.IsHosting ? Loc.T(L.Island.Hosting) : Loc.T(L.Island.Watching);
    }

    private static bool CanDriveVideo(VideoSuite suite) =>
        !suite.WatchAlong.IsViewing || suite.WatchAlong.CanControlPlayback;

    private static bool VideoHasNext(VideoSuite suite) =>
        suite.WatchAlong.IsViewing ? suite.WatchAlong.HostQueue.Count > 0 : suite.Queue.HasNext;

    private void RunVideo(MediaCommand command)
    {
        if (video is not { } suite || !CanDriveVideo(suite))
        {
            return;
        }

        var watchAlong = suite.WatchAlong;
        switch (command)
        {
            case MediaCommand.PlayPause:
            {
                var paused = suite.Player.Progress.Paused;
                if (watchAlong.IsViewing)
                {
                    watchAlong.ControlPause(!paused);
                    return;
                }

                if (suite.Player.HasMedia)
                {
                    suite.Player.Pause(!paused);
                }

                return;
            }
            case MediaCommand.Next:
            {
                if (watchAlong.IsViewing)
                {
                    watchAlong.ControlNext();
                    return;
                }

                suite.Queue.Advance();
                return;
            }
        }
    }

    private void SeekVideo(float seconds)
    {
        if (video is not { } suite || !CanDriveVideo(suite))
        {
            return;
        }

        if (suite.WatchAlong.IsViewing)
        {
            suite.WatchAlong.ControlSeek(seconds);
            return;
        }

        suite.Player.Seek(MathF.Max(0f, seconds));
    }
}
