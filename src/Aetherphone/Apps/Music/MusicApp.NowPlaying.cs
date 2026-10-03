using Aetherphone.Apps.Music.NowPlaying;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal enum NowPlayingPane : byte
{
    Artwork,
    Lyrics,
    UpNext,
}

internal sealed partial class MusicApp
{
    private const float MiniPlayerHeight = 56f;
    private const float MiniPlayerGap = 8f;
    private const float PausedArtScale = 0.86f;
    private const float PaneSmoothTime = 0.24f;
    private const float ArtScaleSmoothTime = 0.3f;
    private const float PlayMorphSmoothTime = 0.12f;
    private const float NowPlayingVeil = 0.4f;

    private static readonly Vector4 NowPlayingInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 NowPlayingMuted = new(1f, 1f, 1f, 0.62f);
    private static readonly Vector4 NowPlayingFaint = new(1f, 1f, 1f, 0.38f);
    private static readonly Vector4 NowPlayingWash = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 NowPlayingRail = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 ActiveGlyphInk = new(0.08f, 0.08f, 0.10f, 1f);

    private readonly NowPlayingPresenter nowPlaying = new();
    private readonly PaletteBackdrop paletteBackdrop = new();
    private readonly NowPlayingText nowPlayingText = new();
    private readonly NowPlayingSlider scrubber = new();
    private readonly NowPlayingSlider volumeSlider = new();
    private Spring miniPresence;
    private Spring paneBlend;
    private Spring artScale = new(1f);
    private Spring playMorph;
    private NowPlayingPane pane = NowPlayingPane.Artwork;
    private NowPlayingPane listPane = NowPlayingPane.UpNext;
    private Rect miniArtRect;
    private bool miniArtKnown;
    private Rect nowPlayingDragZone;
    private float nowPlayingClock;

    private bool NowPlayingCapturesPointer => nowPlaying.CapturesPointer || NowPlayingPopupsCapture;

    private bool NowPlayingPopupsCapture =>
        outputSheet.CapturesPointer || sleepMenu.CapturesPointer || queueMenu.CapturesPointer;

    private void ResetNowPlaying()
    {
        nowPlaying.CloseImmediately();
        CloseNowPlayingPopups();
        SelectPane(NowPlayingPane.Artwork, true);
        scrubber.Cancel();
        volumeSlider.Cancel();
        CancelQueueDrag();
        miniPresence.SnapTo(playback.IsActive ? 1f : 0f);
        playMorph.SnapTo(playback.IsPlaying ? 1f : 0f);
        artScale.SnapTo(playback.IsPlaying ? 1f : PausedArtScale);
    }

    private void ResumeNowPlaying()
    {
        miniPresence.SnapTo(playback.IsActive ? 1f : 0f);
        playMorph.SnapTo(playback.IsPlaying ? 1f : 0f);
    }

    private void OpenNowPlaying()
    {
        if (!playback.IsActive)
        {
            return;
        }

        SelectPane(NowPlayingPane.Artwork, true);
        nowPlaying.Open();
    }

    private void CloseNowPlaying()
    {
        CloseNowPlayingPopups();
        CancelQueueDrag();
        nowPlaying.Close();
    }

    private void DisposeNowPlaying() => paletteBackdrop.Dispose();

    private void CloseNowPlayingPopups()
    {
        outputSheet.Close();
        sleepMenu.Close();
        queueMenu.Close();
    }

    private void SelectPane(NowPlayingPane wanted, bool immediate)
    {
        if (wanted == NowPlayingPane.UpNext && pane != NowPlayingPane.UpNext)
        {
            queueScroll.Reset();
        }

        if (wanted != NowPlayingPane.Artwork)
        {
            listPane = wanted;
        }

        pane = wanted;
        if (immediate)
        {
            paneBlend.SnapTo(wanted == NowPlayingPane.Artwork ? 0f : 1f);
        }
    }

    private void TogglePane(NowPlayingPane wanted)
    {
        SelectPane(pane == wanted ? NowPlayingPane.Artwork : wanted, false);
    }

    private float MiniPlayerInset(float scale, float delta)
    {
        if (!playback.IsActive && nowPlaying.IsOpen)
        {
            CloseNowPlaying();
        }

        nowPlayingClock += delta;
        playMorph.Step(playback.IsPlaying ? 1f : 0f, PlayMorphSmoothTime, delta);
        var presence = Math.Clamp(miniPresence.Step(playback.IsActive ? 1f : 0f, Motion.Appear, delta), 0f, 1f);
        return (MiniPlayerHeight + MiniPlayerGap) * scale * presence;
    }

    private void DrawNowPlayingSheet(Rect screen, float scale)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        if (!nowPlaying.CapturesPointer)
        {
            DrawNowPlayingPopups(screen);
            return;
        }

        paletteBackdrop.Request(images, playback.ArtworkUrl, playback.Title);
        paletteBackdrop.Tick(delta);
        paneBlend.Step(pane == NowPlayingPane.Artwork ? 0f : 1f, PaneSmoothTime, delta);
        var paused = !playback.IsPlaying && !playback.IsBuffering;
        artScale.Step(paused ? PausedArtScale : 1f, ArtScaleSmoothTime, delta);
        using (InputShield.Engage(NowPlayingPopupsCapture))
        {
            var pressInZone = UiInteract.HoverWindowOnly(nowPlayingDragZone.Min, nowPlayingDragZone.Max) &&
                              !scrubber.Dragging && !volumeSlider.Dragging && !QueueDragging;
            nowPlaying.Step(screen.Height, pressInZone, delta, scale);
            DrawNowPlayingPanel(screen, scale, delta);
            if (scrubber.Dragging || volumeSlider.Dragging || QueueDragging)
            {
                nowPlaying.ReleasePress();
            }
        }

        DrawNowPlayingPopups(screen);
    }

    private void DrawNowPlayingPanel(Rect screen, float scale, float delta)
    {
        var openness = nowPlaying.Openness;
        using var layer = ScreenLayer.Begin("music.nowPlaying", screen, false);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        Material.Veil(drawList, screen.Min, screen.Max, NowPlayingVeil * openness);
        var topInset = theme.TopZoneHeight * scale;
        var travel = screen.Height;
        var panelTop = screen.Min.Y + topInset + travel * (1f - openness);
        var panel = new Rect(new Vector2(screen.Min.X, panelTop),
            new Vector2(screen.Max.X, panelTop + screen.Height - topInset));
        if (UiInteract.HoverWindowOnly(panel.Min, panel.Max))
        {
            UiInteract.ReportGestureSurface();
        }

        var rounding = theme.ScreenRounding * scale;
        Elevation.Floating(drawList, panel.Min, panel.Max, rounding, scale, openness);
        paletteBackdrop.Draw(drawList, panel, rounding, nowPlayingClock);
        drawList.PushClipRect(panel.Min, panel.Max, true);
        DrawNowPlayingContent(drawList, panel, scale, delta, openness);
        drawList.PopClipRect();
        drawList.PopClipRect();
    }
}
