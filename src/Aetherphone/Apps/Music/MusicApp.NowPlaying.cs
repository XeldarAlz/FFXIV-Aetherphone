using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar |
                                                  ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private static readonly Vector4 PlayInk = new(0.05f, 0.06f, 0.05f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private void DrawMiniPlayer(Rect content, float scale)
    {
        var presence = Math.Clamp(miniPresence.Value, 0f, 1f);
        if (presence <= 0.01f || !playback.IsActive)
        {
            return;
        }

        var margin = MiniMargin * scale;
        var height = MiniHeight * scale;
        var stripTop = content.Max.Y - margin - height;
        ImGui.SetCursorScreenPos(new Vector2(content.Min.X, stripTop));
        using (ImRaii.Child("##musicMini", new Vector2(content.Width, margin + height), false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            var slide = (height + margin) * (1f - presence);
            var min = new Vector2(content.Min.X + margin, stripTop + slide);
            var max = new Vector2(content.Max.X - margin, stripTop + height + slide);
            var rounding = 12f * scale;
            var swatch = ArtGradient.FromName(playback.Title);
            var fill = Palette.Mix(swatch.Bottom, AppPalettes.Music.BackdropBottom, 0.62f);
            drawList.AddRectFilled(min + new Vector2(0f, 3f * scale), max + new Vector2(0f, 3f * scale),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), rounding);
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f)), 1f);
            var centerY = (min.Y + max.Y) * 0.5f;
            var artSize = 40f * scale;
            var artMin = new Vector2(min.X + 8f * scale, centerY - artSize * 0.5f);
            var artMax = artMin + new Vector2(artSize, artSize);
            if (playback.SongActive)
            {
                DrawCover(drawList, artMin, artMax, playback.Songs.CurrentThumbnail, playback.Title, 6f * scale);
            }
            else
            {
                drawList.AddImageRounded(artwork.HandleForName(playback.Title), artMin, artMax, Vector2.Zero,
                    Vector2.One, 0xFFFFFFFFu, 6f * scale, ImDrawFlags.RoundCornersAll);
            }

            var toggleCenter = new Vector2(max.X - 28f * scale, centerY);
            var stopCenter = new Vector2(max.X - 62f * scale, centerY);
            var buttonsLeft = stopCenter.X - 16f * scale;
            var textLeft = artMax.X + 12f * scale;
            var textWidth = buttonsLeft - textLeft - 6f * scale;
            var title = Typography.FitText(playback.Title, textWidth, TextStyles.FootnoteEmphasized);
            Typography.Draw(new Vector2(textLeft, min.Y + 10f * scale), title, ui.TitleInk,
                TextStyles.FootnoteEmphasized);
            var subtitle = Typography.FitText(playback.Subtitle, textWidth, TextStyles.Caption1);
            Typography.Draw(new Vector2(textLeft, min.Y + 29f * scale), subtitle, ui.MutedInk, TextStyles.Caption1);
            var stopped = MiniGlyphButton(drawList, stopCenter, 14f * scale, scale, playing: false, stopGlyph: true);
            if (stopped)
            {
                playback.Stop();
                return;
            }

            if (MiniGlyphButton(drawList, toggleCenter, 15f * scale, scale, playback.IsPlaying, stopGlyph: false))
            {
                playback.TogglePlayPause();
                return;
            }

            if (playback.SongActive && playback.Songs.Duration > 0f)
            {
                var fraction = Math.Clamp(playback.Songs.Position / playback.Songs.Duration, 0f, 1f);
                var lineLeft = min.X + 12f * scale;
                var lineRight = max.X - 12f * scale;
                var lineY = max.Y - 5f * scale;
                var thickness = 2f * scale;
                drawList.AddRectFilled(new Vector2(lineLeft, lineY - thickness * 0.5f),
                    new Vector2(lineRight, lineY + thickness * 0.5f),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), thickness * 0.5f);
                drawList.AddRectFilled(new Vector2(lineLeft, lineY - thickness * 0.5f),
                    new Vector2(lineLeft + (lineRight - lineLeft) * fraction, lineY + thickness * 0.5f),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f)), thickness * 0.5f);
            }

            var overButtons = UiInteract.Hover(new Vector2(buttonsLeft, min.Y), max);
            if (!overButtons && UiInteract.HoverClick(min, max))
            {
                OpenNowPlaying();
            }
        }
    }

    private static bool MiniGlyphButton(ImDrawListPtr drawList, Vector2 center, float hitRadius, float scale,
        bool playing, bool stopGlyph)
    {
        var hit = new Vector2(hitRadius, hitRadius);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var ink = ImGui.GetColorU32(hovered ? White : new Vector4(1f, 1f, 1f, 0.82f));
        if (stopGlyph)
        {
            MediaGlyph.Stop(drawList, center, 5.5f * scale, ink);
        }
        else if (playing)
        {
            MediaGlyph.Pause(drawList, center, 7f * scale, ink);
        }
        else
        {
            MediaGlyph.Play(drawList, center + new Vector2(1f * scale, 0f), 7f * scale, ink);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawNowPlayingSheet(Rect content, float scale, float presence, float delta)
    {
        if (presence <= 0.003f || !playback.IsActive)
        {
            return;
        }

        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##musicNowPlaying", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            var slide = (1f - presence) * screen.Height;
            var backdropTop = screen.Min.Y + slide;
            drawList.AddRectFilled(screen.Min, new Vector2(screen.Max.X, backdropTop),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f * presence)));
            var swatch = ArtGradient.FromName(playback.Title);
            var tint = Palette.Mix(swatch.Bottom, AppPalettes.Music.BackdropTop, 0.55f);
            Squircle.FillVerticalGradient(drawList, new Vector2(screen.Min.X, backdropTop),
                new Vector2(screen.Max.X, backdropTop + screen.Height), 0f, ImGui.GetColorU32(tint),
                ImGui.GetColorU32(AppPalettes.Music.BackdropBottom));
            var frame = content.Translate(new Vector2(0f, slide));
            DrawSheetTopBar(drawList, frame, scale);
            DrawSheetBody(drawList, frame, scale, delta);
        }
    }

    private void DrawSheetTopBar(ImDrawListPtr drawList, Rect frame, float scale)
    {
        var barCenterY = frame.Min.Y + 24f * scale;
        if (MusicRenderer.ChevronDown("music.sheetClose", new Vector2(frame.Min.X + 26f * scale, barCenterY),
                13f * scale, ui.TitleInk, scale))
        {
            CloseNowPlaying();
        }

        var caption = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Music.PlayingFrom));
        Typography.DrawCentered(drawList, new Vector2(frame.Center.X, barCenterY - 8f * scale), caption,
            ui.HeaderInk, TextStyles.Caption2);
        var source = playSource.Length > 0 ? playSource : DisplayName;
        var fitted = Typography.FitText(source, frame.Width - 160f * scale, TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, new Vector2(frame.Center.X, barCenterY + 8f * scale), fitted, ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        if (playback.SongActive && ui.IconButton(new Vector2(frame.Max.X - 58f * scale, barCenterY), 14f * scale,
                FontAwesomeIcon.Plus.ToIconString(), ui.MutedInk, AppSkin.Transparent, 0.82f, Loc.T(L.Music.AddToPlaylist)))
        {
            OpenPicker(CurrentSong());
        }

        if (ui.IconButton(new Vector2(frame.Max.X - 26f * scale, barCenterY), 14f * scale,
                FontAwesomeIcon.Stop.ToIconString(), ui.MutedInk, AppSkin.Transparent, 0.72f))
        {
            playback.Stop();
            CloseNowPlaying();
        }
    }

    private void DrawSheetBody(ImDrawListPtr drawList, Rect frame, float scale, float delta)
    {
        var songActive = playback.SongActive;
        var songs = playback.Songs;
        var pad = 24f * scale;
        var centerX = frame.Center.X;
        var volumeY = frame.Max.Y - 40f * scale;
        var transportY = volumeY - 74f * scale;
        var progressY = transportY - 56f * scale;
        var artistY = progressY - 42f * scale;
        var titleY = artistY - 27f * scale;
        var artTop = frame.Min.Y + 52f * scale;
        var artBottom = titleY - 18f * scale;
        var artSpace = MathF.Max(artBottom - artTop, 40f * scale);
        var artSize = MathF.Min(frame.Width - pad * 2f, artSpace);
        artBreath.Step(playback.IsPlaying ? 1f : 0.92f, ArtSmoothTime, delta);
        var drawnArt = artSize * Math.Clamp(artBreath.Value, 0.85f, 1f);
        var artCenter = new Vector2(centerX, artTop + artSpace * 0.5f);
        var artMin = artCenter - new Vector2(drawnArt * 0.5f, drawnArt * 0.5f);
        var artMax = artCenter + new Vector2(drawnArt * 0.5f, drawnArt * 0.5f);
        var artRounding = 12f * scale;
        drawList.AddRectFilled(artMin + new Vector2(0f, 10f * scale), artMax + new Vector2(0f, 12f * scale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), artRounding);
        if (songActive)
        {
            DrawCover(drawList, artMin, artMax, songs.CurrentThumbnail, playback.Title, artRounding);
            if (songs.State is SongPlaybackState.Resolving or SongPlaybackState.Buffering)
            {
                drawList.AddRectFilled(artMin, artMax, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)),
                    artRounding);
                LoadingPulse.Spinner(artCenter, 14f * scale, ui.Accent, 1f, drawList);
            }
        }
        else
        {
            drawList.AddImageRounded(artwork.HandleForName(playback.Title), artMin, artMax, Vector2.Zero, Vector2.One,
                0xFFFFFFFFu, artRounding, ImDrawFlags.RoundCornersAll);
            Equalizer.Draw(drawList, new Vector2(artMax.X - 22f * scale, artMax.Y - 18f * scale), scale, 16f * scale,
                clock, White, 1f, playback.IsPlaying);
        }

        var textWidth = frame.Width - pad * 2f;
        var title = Typography.FitText(playback.Title, textWidth, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(frame.Min.X + pad, titleY), title, ui.TitleInk, TextStyles.Title3);
        var statusLine = SheetStatusLine(out var statusColor);
        var status = Typography.FitText(statusLine, textWidth, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(frame.Min.X + pad, artistY), status, statusColor,
            TextStyles.Subheadline);
        var trackRect = new Rect(new Vector2(frame.Min.X + pad, progressY - 2f * scale),
            new Vector2(frame.Max.X - pad, progressY + 2f * scale));
        if (songActive)
        {
            DrawSongProgress(drawList, trackRect, progressY, scale, songs);
        }
        else
        {
            DrawLiveProgress(drawList, trackRect, progressY, scale);
        }

        if (playback.HasQueue)
        {
            if (TransportButton.Draw(new Vector2(centerX - 84f * scale, transportY), 20f * scale,
                    TransportAction.Previous, ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), 1f, true))
            {
                playback.Previous();
            }

            if (TransportButton.Draw(new Vector2(centerX + 84f * scale, transportY), 20f * scale,
                    TransportAction.Next, ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), 1f, true))
            {
                playback.Next();
            }
        }

        if (MusicRenderer.PlayButton("music.sheetPlay", new Vector2(centerX, transportY), 29f * scale, White,
                PlayInk, playback.IsPlaying))
        {
            playback.TogglePlayPause();
        }

        DrawVolumeRow(frame, volumeY, pad, scale);
    }

    private void DrawSongProgress(ImDrawListPtr drawList, Rect trackRect, float progressY, float scale,
        SongPlayer songs)
    {
        var duration = songs.Duration;
        var position = songs.Position;
        var fraction = duration > 0f ? Math.Clamp(position / duration, 0f, 1f) : 0f;
        var slider = MusicRenderer.Slider("music.progress", trackRect, fraction,
            Palette.WithAlpha(ui.TitleInk, 0.92f), ui.Accent, new Vector4(1f, 1f, 1f, 0.20f));
        if (slider.Released && duration > 0f)
        {
            songs.Seek(slider.Value * duration);
        }

        var shownSeconds = slider.Dragging || slider.Released ? slider.Value * duration : position;
        var elapsed = FormatTime((int)shownSeconds);
        Typography.Draw(drawList, new Vector2(trackRect.Min.X, progressY + 8f * scale), elapsed, ui.MutedInk,
            TextStyles.Caption1);
        var total = FormatTime((int)duration);
        var totalSize = Typography.Measure(total, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(trackRect.Max.X - totalSize.X, progressY + 8f * scale), total,
            ui.MutedInk, TextStyles.Caption1);
    }

    private void DrawLiveProgress(ImDrawListPtr drawList, Rect trackRect, float progressY, float scale)
    {
        var thickness = trackRect.Height;
        drawList.AddRectFilled(trackRect.Min, trackRect.Max,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.90f)), thickness * 0.5f);
        var label = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Common.Live));
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var labelPosition = new Vector2(trackRect.Max.X - labelSize.X, progressY + 8f * scale);
        drawList.AddCircleFilled(new Vector2(labelPosition.X - 9f * scale, labelPosition.Y + labelSize.Y * 0.5f),
            3f * scale, ImGui.GetColorU32(ui.Accent), 16);
        Typography.Draw(drawList, labelPosition, label, ui.TitleInk, TextStyles.FootnoteEmphasized);
    }

    private void DrawVolumeRow(Rect frame, float volumeY, float pad, float scale)
    {
        var leftIcon = new Vector2(frame.Min.X + pad + 6f * scale, volumeY);
        var rightIcon = new Vector2(frame.Max.X - pad - 6f * scale, volumeY);
        AppSkin.Icon(leftIcon, FontAwesomeIcon.VolumeDown.ToIconString(), ui.MutedInk, 0.70f);
        AppSkin.Icon(rightIcon, FontAwesomeIcon.VolumeUp.ToIconString(), ui.MutedInk, 0.70f);
        var track = new Rect(new Vector2(leftIcon.X + 22f * scale, volumeY - 1.5f * scale),
            new Vector2(rightIcon.X - 22f * scale, volumeY + 1.5f * scale));
        var slider = MusicRenderer.Slider("music.volume", track, playback.Volume,
            Palette.WithAlpha(ui.TitleInk, 0.92f), ui.Accent, new Vector4(1f, 1f, 1f, 0.20f));
        if (slider.Dragging || slider.Released)
        {
            playback.Volume = slider.Value;
        }

        if (slider.Released)
        {
            playback.CommitVolume();
        }
    }

    private string SheetStatusLine(out Vector4 color)
    {
        color = ui.MutedInk;
        if (playback.SongActive)
        {
            var songs = playback.Songs;
            switch (songs.State)
            {
                case SongPlaybackState.Resolving:
                    return Loc.T(L.Common.Loading);
                case SongPlaybackState.Buffering:
                    return Loc.T(L.Music.Buffering);
                case SongPlaybackState.Failed:
                    color = theme.Danger;
                    return Loc.T(L.Music.CouldntPlay);
                default:
                    return songs.CurrentAuthor;
            }
        }

        switch (playback.Radio.State)
        {
            case RadioPlaybackState.Buffering:
                return Loc.T(L.Music.Buffering);
            case RadioPlaybackState.Failed:
                color = theme.Danger;
                return Loc.T(L.Music.ConnectionLost);
            case RadioPlaybackState.Paused:
                return Loc.T(L.Music.Paused);
            default:
                return CategoryTitle();
        }
    }
}
