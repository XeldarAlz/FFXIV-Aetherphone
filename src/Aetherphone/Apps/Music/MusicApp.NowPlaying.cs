using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private void DrawRadioNowPlaying(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var station = playback.Radio.CurrentStation;
        var state = playback.Radio.State;
        var dl = ImGui.GetWindowDrawList();
        AppHeader.Draw(context, Loc.T(L.Music.NowPlaying), GoToReturnView);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        var centerX = body.Center.X;
        var margin = 22f * scale;
        var artSize = MathF.Min(body.Width - margin * 2f, body.Height * 0.46f);
        var artTop = body.Min.Y + 18f * scale;
        var artMin = new Vector2(centerX - artSize * 0.5f, artTop);
        var artMax = artMin + new Vector2(artSize, artSize);
        var artRounding = 22f * scale;
        dl.AddRectFilled(artMin + new Vector2(0f, 12f * scale), artMax + new Vector2(0f, 14f * scale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), artRounding);
        dl.AddImageRounded(artwork.HandleForName(station), artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
            artRounding, ImDrawFlags.RoundCornersAll);
        var pulse = state == RadioPlaybackState.Buffering ? 0.5f + 0.5f * MathF.Abs(MathF.Sin(clock * 3f)) : 1f;
        Equalizer.Draw(dl, new Vector2(artMax.X - 24f * scale, artMax.Y - 20f * scale), scale, 18f * scale, clock,
            new Vector4(1f, 1f, 1f, 1f), pulse, state == RadioPlaybackState.Playing);
        var nameY = artMax.Y + 32f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), Truncate(station, 24), theme.TextStrong, 1.45f);
        Typography.DrawCentered(new Vector2(centerX, nameY + 27f * scale), RadioNowPlayingSubtitle(state),
            Palette.WithAlpha(Accent, 0.95f), 0.9f);
        var controlsY = nameY + 74f * scale;
        if (playback.Radio.HasQueue)
        {
            if (TransportButton.Draw(new Vector2(centerX - 72f * scale, controlsY), 22f * scale,
                    TransportAction.Previous, Accent, theme.TextStrong, 1f, true))
            {
                playback.Previous();
            }

            if (TransportButton.Draw(new Vector2(centerX + 72f * scale, controlsY), 22f * scale, TransportAction.Next,
                    Accent, theme.TextStrong, 1f, true))
            {
                playback.Next();
            }
        }

        if (TransportButton.Draw(new Vector2(centerX, controlsY), 30f * scale, TransportAction.Stop, Accent,
                theme.TextStrong, 1f, true))
        {
            playback.Stop();
            GoToReturnView();
        }

        var trackY = controlsY + 56f * scale;
        var trackRect = new Rect(new Vector2(body.Min.X + margin + 20f * scale, trackY - 3f * scale),
            new Vector2(body.Max.X - margin - 20f * scale, trackY + 3f * scale));
        playback.Volume = Scrubber.Draw(trackRect, playback.Volume, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f),
            1f);
    }

    private void DrawSongNowPlaying(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var songs = playback.Songs;
        var dl = ImGui.GetWindowDrawList();
        AppHeader.Draw(context, Loc.T(L.Music.NowPlaying), GoToReturnView);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        var centerX = body.Center.X;
        var margin = 22f * scale;
        var artSize = MathF.Min(body.Width - margin * 2f, body.Height * 0.42f);
        var artTop = body.Min.Y + 16f * scale;
        var artMin = new Vector2(centerX - artSize * 0.5f, artTop);
        var artMax = artMin + new Vector2(artSize, artSize);
        var artRounding = 20f * scale;
        dl.AddRectFilled(artMin + new Vector2(0f, 12f * scale), artMax + new Vector2(0f, 14f * scale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), artRounding);
        DrawThumb(dl, artMin, artMax, songs.CurrentThumbnail, songs.CurrentTitle, artRounding);
        var nameY = artMax.Y + 28f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), Truncate(songs.CurrentTitle, 30), theme.TextStrong, 1.4f,
            FontWeight.SemiBold);
        Typography.DrawCentered(new Vector2(centerX, nameY + 27f * scale), Truncate(SongNowPlayingSubtitle(), 34),
            Palette.WithAlpha(Accent, 0.95f), 0.9f);
        var duration = songs.Duration;
        var position = songs.Position;
        var fraction = duration > 0f ? Math.Clamp(position / duration, 0f, 1f) : 0f;
        var trackY = nameY + 60f * scale;
        var trackRect = new Rect(new Vector2(body.Min.X + margin, trackY - 3f * scale),
            new Vector2(body.Max.X - margin, trackY + 3f * scale));
        var newFraction = Scrubber.Draw(trackRect, fraction, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f), 1f);
        if (duration > 0f && MathF.Abs(newFraction - fraction) > 0.0025f)
        {
            songs.Seek(newFraction * duration);
        }

        Typography.Draw(new Vector2(trackRect.Min.X, trackY + 9f * scale), FormatTime((int)position), theme.TextMuted,
            0.72f);
        var durationLabel = FormatTime((int)duration);
        var durationSize = Typography.Measure(durationLabel, 0.72f);
        Typography.Draw(new Vector2(trackRect.Max.X - durationSize.X, trackY + 9f * scale), durationLabel,
            theme.TextMuted, 0.72f);
        var controlsY = trackY + 50f * scale;
        if (playback.HasQueue)
        {
            if (TransportButton.Draw(new Vector2(centerX - 72f * scale, controlsY), 22f * scale,
                    TransportAction.Previous, Accent, theme.TextStrong, 1f, true))
            {
                playback.Previous();
            }

            if (TransportButton.Draw(new Vector2(centerX + 72f * scale, controlsY), 22f * scale, TransportAction.Next,
                    Accent, theme.TextStrong, 1f, true))
            {
                playback.Next();
            }
        }

        if (TransportButton.Draw(new Vector2(centerX, controlsY), 32f * scale, TransportAction.Stop, Accent,
                theme.TextStrong, 1f, true))
        {
            playback.Stop();
            GoToReturnView();
        }

        var volumeY = controlsY + 50f * scale;
        var volumeRect = new Rect(new Vector2(body.Min.X + margin + 20f * scale, volumeY - 2.5f * scale),
            new Vector2(body.Max.X - margin - 20f * scale, volumeY + 2.5f * scale));
        playback.Volume = Scrubber.Draw(volumeRect, playback.Volume, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f),
            1f);
    }

    private void DrawMiniPlayer(in PhoneContext context, float scale)
    {
        if (!playback.IsActive)
        {
            return;
        }

        var theme = context.Theme;
        var content = context.Content;
        var height = MiniHeight * scale;
        var min = new Vector2(content.Min.X + 2f * scale, content.Max.Y - height);
        var max = new Vector2(content.Max.X - 2f * scale, content.Max.Y - 2f * scale);
        var rounding = 16f * scale;
        var dl = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        Squircle.Fill(dl, min, max, rounding, ImGui.GetColorU32(theme.Surface));
        Squircle.Stroke(dl, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)), 1f);
        var discRadius = 18f * scale;
        var discCenter = new Vector2(min.X + 12f * scale + discRadius, min.Y + height * 0.5f);
        if (playback.SongActive)
        {
            var artMin = new Vector2(discCenter.X - discRadius, discCenter.Y - discRadius);
            var artMax = new Vector2(discCenter.X + discRadius, discCenter.Y + discRadius);
            DrawThumb(dl, artMin, artMax, playback.Songs.CurrentThumbnail, playback.Title, 9f * scale);
        }
        else
        {
            ArtGradient.DrawDisc(dl, discCenter, discRadius, ArtGradient.FromName(playback.Title), 1f);
        }

        var textLeft = discCenter.X + discRadius + 12f * scale;
        var stopCenter = new Vector2(max.X - 26f * scale, discCenter.Y);
        Typography.Draw(new Vector2(textLeft, min.Y + 11f * scale), Truncate(playback.Title, 18), theme.TextStrong,
            0.95f);
        Typography.Draw(new Vector2(textLeft, min.Y + 31f * scale), Truncate(playback.Subtitle, 22), theme.TextMuted,
            0.8f);
        var stopped = TransportButton.Draw(stopCenter, 16f * scale, TransportAction.Stop, Accent, theme.TextStrong, 1f,
            true);
        if (stopped)
        {
            playback.Stop();
            return;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(
                stopCenter - new Vector2(16f * scale, 16f * scale), stopCenter + new Vector2(16f * scale, 16f * scale)))
        {
            router.Push(playback.SongActive ? View.SongNowPlaying : View.RadioNowPlaying);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private string RadioNowPlayingSubtitle(RadioPlaybackState state)
    {
        if (state is RadioPlaybackState.Buffering or RadioPlaybackState.Failed)
        {
            return RadioStateLabel(state);
        }

        return categoryIndex >= 0
            ? $"{CatalogLabels.RadioCategory(RadioService.Categories[categoryIndex].Display)} · {Loc.T(L.Common.Live)}"
            : Loc.T(L.Common.Live);
    }

    private string SongNowPlayingSubtitle()
    {
        var songs = playback.Songs;
        return songs.State switch
        {
            SongPlaybackState.Resolving => Loc.T(L.Common.Loading),
            SongPlaybackState.Buffering => Loc.T(L.Music.Buffering),
            SongPlaybackState.Failed => Loc.T(L.Music.CouldntPlay),
            _ => songs.CurrentAuthor,
        };
    }

    private static string RadioStateLabel(RadioPlaybackState state)
    {
        return state switch
        {
            RadioPlaybackState.Buffering => Loc.T(L.Music.Buffering),
            RadioPlaybackState.Playing => Loc.T(L.Music.Playing),
            RadioPlaybackState.Failed => Loc.T(L.Music.ConnectionLost),
            _ => string.Empty,
        };
    }
}
