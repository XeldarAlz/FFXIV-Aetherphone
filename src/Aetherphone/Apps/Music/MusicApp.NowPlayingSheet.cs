using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float NowPlayingSide = 26f;
    private const float GrabberTop = 8f;
    private const float GrabberAlpha = 0.35f;
    private const float NowPlayingTop = 30f;
    private const float BottomRowHeight = 44f;
    private const float VolumeRowHeight = 30f;
    private const float TransportRowHeight = 72f;
    private const float ScrubRowHeight = 44f;
    private const float TitleRowHeight = 58f;
    private const float HeaderArtUnits = 56f;
    private const float ScrubBarOffset = 10f;
    private const float ScrubLabelGap = 8f;
    private const float TransportSpread = 0.32f;
    private const float SkipGlyph = 13f;
    private const float PlayGlyph = 19f;
    private const float SkipHitRadius = 30f;
    private const float PlayHitRadius = 36f;
    private const float VolumeIconReserve = 28f;
    private const float VolumeIconScale = 0.8f;
    private const float RowButtonRadius = 15f;
    private const float RowButtonSpacing = 38f;
    private const float RowButtonGlyph = 0.85f;
    private const float PaneButtonRadius = 20f;
    private const float PaneButtonEdge = 22f;
    private const float PaneGlyphScale = 1f;
    private const float MiniArtRadiusFraction = 0.18f;
    private const float LivePillPadX = 10f;
    private const float LivePillPadY = 3f;
    private const float InteractiveBlend = 0.5f;
    private const float DisabledAlpha = 0.35f;
    private const float PressedWashAlpha = 0.14f;
    private const float MoonScale = 0.7f;
    private const float MoonGap = 6f;

    private static readonly MarqueeId TitleMarquee = new("music.np.", "title");
    private static readonly MarqueeId ArtistMarquee = new("music.np.", "artist");
    private static readonly MarqueeId HeaderTitleMarquee = new("music.np.", "headerTitle");
    private static readonly MarqueeId HeaderArtistMarquee = new("music.np.", "headerArtist");

    private readonly record struct NowPlayingLayout(Rect Panel, float Left, float Right, Rect Hero, Rect HeaderArt,
        Rect HeaderRow, Rect TitleRow, Rect ListArea, Rect Scrub, Rect Transport, Rect Volume, Rect BottomRow);

    private void DrawNowPlayingContent(ImDrawListPtr drawList, Rect panel, float scale, float delta, float openness)
    {
        var layout = ComputeNowPlayingLayout(drawList, panel, scale);
        var blend = Math.Clamp(paneBlend.Value, 0f, 1f);
        nowPlayingDragZone = blend < InteractiveBlend
            ? new Rect(panel.Min, new Vector2(panel.Max.X, layout.Scrub.Min.Y))
            : new Rect(panel.Min, new Vector2(panel.Max.X, layout.HeaderRow.Max.Y));
        DrawGrabber(drawList, panel, scale);
        DrawSleepBadge(drawList, panel, layout.Right, scale);
        if (blend > 0.01f)
        {
            DrawListPane(drawList, layout.ListArea, blend, scale, delta);
            DrawHeaderRow(drawList, layout, blend, scale);
        }

        if (blend < 0.99f)
        {
            DrawTitleRow(drawList, layout, 1f - blend, scale);
        }

        DrawScrubber(drawList, layout, scale, delta);
        DrawTransport(drawList, layout, scale);
        DrawVolume(drawList, layout, scale, delta);
        DrawPaneButtons(drawList, layout, scale);
        drawList.PopClipRect();
        DrawNowPlayingArtwork(drawList, layout, blend, openness, scale);
        drawList.PushClipRect(panel.Min, panel.Max, true);
    }

    private NowPlayingLayout ComputeNowPlayingLayout(ImDrawListPtr drawList, Rect panel, float scale)
    {
        var left = panel.Min.X + NowPlayingSide * scale;
        var right = panel.Max.X - NowPlayingSide * scale;
        var top = panel.Min.Y + NowPlayingTop * scale;
        var badge = DrawJamNowPlayingBadge(drawList, new Vector2(left, top), right - left, scale);
        if (badge > 0f)
        {
            top += badge + Metrics.Space.Sm * scale;
        }

        var bottom = panel.Max.Y - MathF.Max(theme.BottomZoneHeight * scale, Metrics.Space.Lg * scale);
        var bottomRow = RowAbove(left, right, bottom, BottomRowHeight * scale);
        var volume = RowAbove(left, right, bottomRow.Min.Y - Metrics.Space.Md * scale, VolumeRowHeight * scale);
        var transport = RowAbove(left, right, volume.Min.Y - Metrics.Space.Xs * scale,
            TransportRowHeight * scale);
        var scrub = RowAbove(left, right, transport.Min.Y - Metrics.Space.Xs * scale, ScrubRowHeight * scale);
        var titleRow = RowAbove(left, right, scrub.Min.Y - Metrics.Space.Lg * scale, TitleRowHeight * scale);
        var artTop = top + Metrics.Space.Md * scale;
        var artBottom = titleRow.Min.Y - Metrics.Space.Xl * scale;
        var heroSide = MathF.Max(0f, MathF.Min(right - left, artBottom - artTop));
        var heroMin = new Vector2((left + right - heroSide) * 0.5f,
            artTop + MathF.Max(0f, (artBottom - artTop - heroSide) * 0.5f));
        var hero = new Rect(heroMin, heroMin + new Vector2(heroSide, heroSide));
        var headerSide = HeaderArtUnits * scale;
        var headerMin = new Vector2(left, top + Metrics.Space.Sm * scale);
        var headerArt = new Rect(headerMin, headerMin + new Vector2(headerSide, headerSide));
        var headerRow = new Rect(headerMin, new Vector2(right, headerArt.Max.Y));
        var listArea = new Rect(new Vector2(panel.Min.X, headerRow.Max.Y + Metrics.Space.Lg * scale),
            new Vector2(panel.Max.X, MathF.Max(headerRow.Max.Y, scrub.Min.Y - Metrics.Space.Md * scale)));
        return new NowPlayingLayout(panel, left, right, hero, headerArt, headerRow, titleRow, listArea, scrub,
            transport, volume, bottomRow);
    }

    private static Rect RowAbove(float left, float right, float bottom, float height) =>
        new(new Vector2(left, bottom - height), new Vector2(right, bottom));

    private static Rect LerpRect(Rect from, Rect to, float amount) =>
        new(Vector2.Lerp(from.Min, to.Min, amount), Vector2.Lerp(from.Max, to.Max, amount));

    private static Rect ScaleRect(Rect rect, float factor)
    {
        var half = rect.Size * (0.5f * factor);
        return new Rect(rect.Center - half, rect.Center + half);
    }

    private static void DrawGrabber(ImDrawListPtr drawList, Rect panel, float scale)
    {
        var width = Metrics.Size.GrabberWidth * scale;
        var height = Metrics.Size.GrabberHeight * scale;
        var min = new Vector2(panel.Center.X - width * 0.5f, panel.Min.Y + GrabberTop * scale);
        drawList.AddRectFilled(min, min + new Vector2(width, height),
            ImGui.GetColorU32(NowPlayingInk with { W = GrabberAlpha }), height * 0.5f);
    }

    private void DrawSleepBadge(ImDrawListPtr drawList, Rect panel, float right, float scale)
    {
        if (!playback.SleepTimerActive)
        {
            return;
        }

        var text = SleepStatusText();
        var size = Typography.Measure(text, TextStyles.Footnote);
        var top = panel.Min.Y + GrabberTop * scale;
        Typography.Draw(drawList, new Vector2(right - size.X, top), text, NowPlayingMuted, TextStyles.Footnote);
        var moonCenter = new Vector2(right - size.X - MoonGap * scale - size.Y * 0.4f, top + size.Y * 0.5f);
        AppSkin.Icon(drawList, moonCenter, IconGlyph.Of(FontAwesomeIcon.Moon), NowPlayingMuted, MoonScale);
    }

    private string SleepStatusText()
    {
        if (playback.SleepAtTrackEnd)
        {
            return Loc.T(L.Music.NowPlaying.SleepEndOfTrack);
        }

        return MusicUi.Duration((int)MathF.Ceiling(playback.SleepRemainingSeconds));
    }

    private void DrawNowPlayingArtwork(ImDrawListPtr drawList, in NowPlayingLayout layout, float blend,
        float openness, float scale)
    {
        var hero = ScaleRect(layout.Hero, 1f + (Math.Clamp(artScale.Value, 0f, 1f) - 1f) * (1f - blend));
        var paneRect = LerpRect(hero, layout.HeaderArt, blend);
        var target = miniArtKnown ? LerpRect(miniArtRect, paneRect, openness) : paneRect;
        if (target.Width <= 1f)
        {
            return;
        }

        var heroFraction = ArtworkTile.HeroRadiusFraction + (ArtworkTile.TileRadiusFraction -
                                                             ArtworkTile.HeroRadiusFraction) * blend;
        var radiusFraction = miniArtKnown
            ? MiniArtRadiusFraction + (heroFraction - MiniArtRadiusFraction) * openness
            : heroFraction;
        var shadow = openness * (1f - blend) * Math.Clamp((artScale.Value - PausedArtScale) / (1f - PausedArtScale),
            0.35f, 1f);
        Elevation.Squircle(drawList, target.Min, target.Max, target.Width * radiusFraction, scale, shadow);
        ArtworkTile.Draw(drawList, images, target.Min, target.Width, playback.ArtworkUrl, playback.Title,
            radiusFraction);
        if (blend <= InteractiveBlend || !nowPlaying.IsOpen)
        {
            return;
        }

        if (UiInteract.HoverClick(target.Min, target.Max))
        {
            SelectPane(NowPlayingPane.Artwork, false);
        }
    }

    private void DrawTitleRow(ImDrawListPtr drawList, in NowPlayingLayout layout, float alpha, float scale)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        var row = layout.TitleRow;
        var interactive = alpha > InteractiveBlend && nowPlaying.IsOpen;
        var buttonsLeft = DrawSongButtons(drawList, layout.Right, row.Center.Y, interactive, scale);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var artistHeight = Typography.LineHeight(TextStyles.Body);
        var top = row.Center.Y - (titleHeight + artistHeight) * 0.5f;
        var width = MathF.Max(1f, buttonsLeft - Metrics.Space.Lg * scale - layout.Left);
        Marquee.DrawLeft(drawList, TitleMarquee, playback.Title, layout.Left, top, width, TextStyles.Title3,
            NowPlayingInk, true);
        DrawArtistLine(drawList, ArtistMarquee, layout.Left, top + titleHeight, width, TextStyles.Body, interactive);
        LayerCompositor.Fade(drawList, firstVertex, alpha);
    }

    private void DrawHeaderRow(ImDrawListPtr drawList, in NowPlayingLayout layout, float alpha, float scale)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        var row = layout.HeaderRow;
        var interactive = alpha > InteractiveBlend && nowPlaying.IsOpen;
        var buttonsLeft = DrawSongButtons(drawList, layout.Right, row.Center.Y, interactive, scale);
        var left = layout.HeaderArt.Max.X + Metrics.Space.Md * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var artistHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = row.Center.Y - (titleHeight + artistHeight) * 0.5f;
        var width = MathF.Max(1f, buttonsLeft - Metrics.Space.Md * scale - left);
        Marquee.DrawLeft(drawList, HeaderTitleMarquee, playback.Title, left, top, width, TextStyles.Headline,
            NowPlayingInk, true);
        DrawArtistLine(drawList, HeaderArtistMarquee, left, top + titleHeight, width, TextStyles.Subheadline,
            interactive);
        LayerCompositor.Fade(drawList, firstVertex, alpha);
    }

    private void DrawArtistLine(ImDrawListPtr drawList, MarqueeId id, float left, float top, float width,
        in TextStyle style, bool interactive)
    {
        var song = playback.CurrentSong;
        var linked = interactive && playback.SongActive && !string.IsNullOrEmpty(song.ChannelId);
        var hovered = linked && UiInteract.Hover(new Vector2(left, top),
            new Vector2(left + width, top + Typography.LineHeight(style)));
        Marquee.DrawLeft(drawList, id, playback.Subtitle, left, top, width, style,
            hovered ? NowPlayingInk : NowPlayingMuted, true);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (linked && UiInteract.Click(new Vector2(left, top),
                new Vector2(left + width, top + Typography.LineHeight(style)), hovered))
        {
            GoToArtistFromNowPlaying();
        }
    }

    private float DrawSongButtons(ImDrawListPtr drawList, float right, float centerY, bool interactive,
        float scale)
    {
        var radius = RowButtonRadius * scale;
        if (!playback.SongActive)
        {
            return right;
        }

        var song = playback.CurrentSong;
        var menuCenter = new Vector2(right - radius, centerY);
        var loveCenter = new Vector2(menuCenter.X - RowButtonSpacing * scale, centerY);
        var loved = library.IsLoved(song.VideoId);
        drawList.AddCircleFilled(loveCenter, radius, ImGui.GetColorU32(NowPlayingWash));
        drawList.AddCircleFilled(menuCenter, radius, ImGui.GetColorU32(NowPlayingWash));
        if (RoundGlyphButton(drawList, ImGui.GetID("music.np.love"), loveCenter, radius,
                IconGlyph.Of(FontAwesomeIcon.Heart),
                loved ? NowPlayingInk : NowPlayingMuted, interactive,
                Loc.T(loved ? L.Music.Unlove : L.Music.Love)))
        {
            library.SetLoved(song, !loved);
        }

        if (RoundGlyphButton(drawList, ImGui.GetID("music.np.menu"), menuCenter, radius,
                IconGlyph.Of(FontAwesomeIcon.EllipsisH), NowPlayingInk, interactive, Loc.T(L.Music.MoreOptions)))
        {
            songMenu.Open(song);
        }

        return loveCenter.X - radius;
    }

    private static bool RoundGlyphButton(ImDrawListPtr drawList, uint pressKey, Vector2 center, float radius,
        string glyph, Vector4 ink, bool interactive, string tooltip)
    {
        var hit = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(pressKey, down, PressFx.ControlPressedScale);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(NowPlayingWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        }

        AppSkin.Icon(drawList, center, glyph, ink, RowButtonGlyph * grow);
        return interactive && UiInteract.Click(center - hit, center + hit, hovered);
    }

    private void GoToArtistFromNowPlaying()
    {
        var song = playback.CurrentSong;
        if (string.IsNullOrEmpty(song.ChannelId))
        {
            return;
        }

        CloseNowPlaying();
        Push(MusicRoute.Artist(song.ChannelId, song.Author));
    }

    private void DrawScrubber(ImDrawListPtr drawList, in NowPlayingLayout layout, float scale, float delta)
    {
        var barY = layout.Scrub.Min.Y + ScrubBarOffset * scale;
        if (!playback.SongActive)
        {
            DrawLivePill(drawList, new Vector2((layout.Left + layout.Right) * 0.5f, barY), scale);
            return;
        }

        var duration = playback.Duration;
        var position = playback.Position;
        var enabled = playback.CanSeek && nowPlaying.IsOpen;
        var value = duration > 0f ? position / duration : 0f;
        var result = scrubber.Draw(drawList, layout.Left, layout.Right, barY, value, enabled,
            NowPlayingInk with { W = 0.9f }, NowPlayingRail, delta);
        if (result.Released && enabled)
        {
            playback.Seek(result.Value * duration);
        }

        var scrubbing = result.Dragging || result.Released;
        var shownSeconds = (int)(scrubbing ? result.Value * duration : position);
        var labelTop = barY + scrubber.Thickness(scale) * 0.5f + ScrubLabelGap * scale;
        var ink = scrubbing ? NowPlayingInk : NowPlayingMuted;
        Typography.Draw(drawList, new Vector2(layout.Left, labelTop), MusicUi.Duration(shownSeconds), ink,
            TextStyles.Footnote);
        var remaining = MusicUi.Remaining((int)duration - shownSeconds);
        var remainingWidth = Typography.Measure(remaining, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(layout.Right - remainingWidth, labelTop), remaining, ink,
            TextStyles.Footnote);
    }

    private static void DrawLivePill(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var text = Loc.T(L.Common.Live);
        var size = Typography.Measure(text, TextStyles.FootnoteEmphasized);
        var pad = new Vector2(LivePillPadX * scale, LivePillPadY * scale);
        var half = size * 0.5f + pad;
        drawList.AddRectFilled(center - half, center + half, ImGui.GetColorU32(NowPlayingRail), half.Y);
        Typography.Draw(drawList, center - size * 0.5f, text, NowPlayingInk, TextStyles.FootnoteEmphasized);
    }

    private void DrawTransport(ImDrawListPtr drawList, in NowPlayingLayout layout, float scale)
    {
        var centerY = layout.Transport.Center.Y;
        var centerX = (layout.Left + layout.Right) * 0.5f;
        var spread = (layout.Right - layout.Left) * TransportSpread;
        var interactive = nowPlaying.IsOpen;
        var skipEnabled = interactive && (playback.SongActive || playback.HasQueue);
        var previousCenter = new Vector2(centerX - spread, centerY);
        var previousScale = TransportPress(drawList, "music.np.previous", previousCenter, SkipHitRadius * scale,
            skipEnabled, out var previousTapped);
        MediaGlyph.Previous(drawList, previousCenter, SkipGlyph * scale * previousScale,
            ImGui.GetColorU32(skipEnabled ? NowPlayingInk : NowPlayingFaint));
        if (previousTapped)
        {
            playback.Previous();
        }

        var playCenter = new Vector2(centerX, centerY);
        var playScale = TransportPress(drawList, "music.np.play", playCenter, PlayHitRadius * scale, interactive,
            out var playTapped);
        DrawPlayPause(drawList, playCenter, PlayGlyph * scale * playScale, NowPlayingInk);
        if (playTapped)
        {
            playback.TogglePlayPause();
        }

        var nextCenter = new Vector2(centerX + spread, centerY);
        var nextScale = TransportPress(drawList, "music.np.next", nextCenter, SkipHitRadius * scale, skipEnabled,
            out var nextTapped);
        MediaGlyph.Next(drawList, nextCenter, SkipGlyph * scale * nextScale,
            ImGui.GetColorU32(skipEnabled ? NowPlayingInk : NowPlayingFaint));
        if (nextTapped)
        {
            playback.Next();
        }
    }

    private static float TransportPress(ImDrawListPtr drawList, string id, Vector2 center, float hitRadius,
        bool enabled, out bool tapped)
    {
        var hit = new Vector2(hitRadius, hitRadius);
        var hovered = enabled && UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        if (hovered)
        {
            var wash = down ? PressedWashAlpha : NowPlayingWash.W;
            drawList.AddCircleFilled(center, hitRadius * grow, ImGui.GetColorU32(NowPlayingInk with { W = wash }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        tapped = enabled && UiInteract.Click(center - hit, center + hit, hovered);
        return grow;
    }

    private void DrawPlayPause(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink)
    {
        var morph = Math.Clamp(playMorph.Value, 0f, 1f);
        if (morph < 0.99f)
        {
            var playSize = size * (0.6f + 0.4f * (1f - morph));
            MediaGlyph.Play(drawList, center, playSize, ImGui.GetColorU32(ink with { W = ink.W * (1f - morph) }));
        }

        if (morph > 0.01f)
        {
            var pauseSize = size * (0.6f + 0.4f * morph);
            MediaGlyph.Pause(drawList, center, pauseSize, ImGui.GetColorU32(ink with { W = ink.W * morph }));
        }
    }

    private void DrawVolume(ImDrawListPtr drawList, in NowPlayingLayout layout, float scale, float delta)
    {
        var centerY = layout.Volume.Center.Y;
        var reserve = VolumeIconReserve * scale;
        AppSkin.Icon(drawList, new Vector2(layout.Left + reserve * 0.3f, centerY),
            IconGlyph.Of(FontAwesomeIcon.VolumeDown), NowPlayingMuted, VolumeIconScale);
        AppSkin.Icon(drawList, new Vector2(layout.Right - reserve * 0.35f, centerY),
            IconGlyph.Of(FontAwesomeIcon.VolumeUp), NowPlayingMuted, VolumeIconScale);
        var result = volumeSlider.Draw(drawList, layout.Left + reserve, layout.Right - reserve, centerY,
            playback.Volume, nowPlaying.IsOpen, NowPlayingInk with { W = 0.9f }, NowPlayingRail, delta);
        if (result.Dragging || result.Released)
        {
            playback.Volume = result.Value;
        }

        if (result.Released)
        {
            playback.CommitVolume();
        }
    }

    private void DrawPaneButtons(ImDrawListPtr drawList, in NowPlayingLayout layout, float scale)
    {
        var centerY = layout.BottomRow.Center.Y;
        var radius = PaneButtonRadius * scale;
        var edge = PaneButtonEdge * scale;
        var interactive = nowPlaying.IsOpen;
        if (PaneButton(drawList, new Vector2(layout.Left + edge, centerY), radius,
                IconGlyph.Of(FontAwesomeIcon.QuoteRight), pane == NowPlayingPane.Lyrics,
                interactive && playback.SongActive, Loc.T(L.Music.NowPlaying.Lyrics)))
        {
            TogglePane(NowPlayingPane.Lyrics);
        }

        if (PaneButton(drawList, new Vector2((layout.Left + layout.Right) * 0.5f, centerY), radius,
                IconGlyph.Of(FontAwesomeIcon.Headphones), outputSheet.IsOpen, interactive,
                Loc.T(L.Music.NowPlaying.Output)))
        {
            outputSheet.Open();
        }

        if (PaneButton(drawList, new Vector2(layout.Right - edge, centerY), radius,
                IconGlyph.Of(FontAwesomeIcon.ListUl), pane == NowPlayingPane.UpNext, interactive,
                Loc.T(L.Music.NowPlaying.UpNext)))
        {
            TogglePane(NowPlayingPane.UpNext);
        }
    }

    private static bool PaneButton(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, bool active,
        bool enabled, string tooltip)
    {
        var hit = new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(ImGui.GetID(tooltip), down, PressFx.ControlPressedScale);
        if (active)
        {
            drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(NowPlayingInk with { W = 0.92f }));
        }
        else if (hovered)
        {
            drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(NowPlayingWash));
        }

        var ink = active ? ActiveGlyphInk : enabled ? NowPlayingMuted : NowPlayingInk with { W = DisabledAlpha };
        AppSkin.Icon(drawList, center, glyph, ink, PaneGlyphScale * grow);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        }

        return enabled && UiInteract.Click(center - hit, center + hit, hovered);
    }

    private void DrawListPane(ImDrawListPtr drawList, Rect area, float alpha, float scale, float delta)
    {
        if (area.Height <= 1f)
        {
            return;
        }

        var firstVertex = drawList.VtxBuffer.Size;
        var interactive = alpha > InteractiveBlend && pane != NowPlayingPane.Artwork && nowPlaying.IsOpen;
        drawList.PushClipRect(area.Min, area.Max, true);
        if (listPane == NowPlayingPane.Lyrics)
        {
            DrawLyricsPane(drawList, area, interactive, scale, delta);
        }
        else
        {
            DrawUpNextPane(drawList, area, interactive, scale, delta);
        }

        drawList.PopClipRect();
        LayerCompositor.Fade(drawList, firstVertex, alpha);
    }
}
