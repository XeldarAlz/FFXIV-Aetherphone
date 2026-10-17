using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed partial class DynamicIsland
{
    private const float PlaybackCardPadY = 14f;
    private const float PlaybackTrackGap = 14f;
    private const float PlaybackTrackThickness = 4f;
    private const float PlaybackTimeGap = 8f;
    private const float PlaybackTransportGap = 30f;
    private const float PlaybackTransportStride = 44f;
    private const float PlaybackTimeAlpha = 0.72f;
    private const string RemainingSign = "-";

    private bool playbackScrubbing;
    private float playbackScrubValue;

    private bool DrawPlaybackExpanded(ImDrawListPtr drawList, Rect bounds, float scale, Vector4 accent, float alpha,
        bool active)
    {
        var iconRadius = CardIconRadius * scale;
        var padX = CardPadX * scale;
        var artCenter = new Vector2(bounds.Min.X + padX + iconRadius,
            bounds.Min.Y + PlaybackCardPadY * scale + iconRadius);
        DrawPlaybackArt(drawList, artCenter, iconRadius, alpha);
        var textLeft = artCenter.X + iconRadius + CardTextGap * scale;
        var textWidth = MathF.Max(1f, bounds.Max.X - padX - textLeft);
        DrawLines(drawList, playback.Title, TextStyles.Headline, Ink, playback.Subtitle, TextStyles.Subheadline,
            accent, textLeft, textWidth, artCenter.Y, scale, alpha, true);
        var duration = playback.Duration;
        var artBottom = artCenter.Y + iconRadius;
        var overControl = false;
        float transportY;
        if (duration > 0f)
        {
            var trackY = artBottom + PlaybackTrackGap * scale;
            overControl = DrawPlaybackTimeline(drawList, bounds, trackY, duration, scale, accent, alpha, active);
            transportY = trackY + PlaybackTransportGap * scale;
        }
        else
        {
            playbackScrubbing = false;
            transportY = (artBottom + bounds.Max.Y - PlaybackCardPadY * scale) * 0.5f;
        }

        return DrawPlaybackTransport(drawList, new Vector2(bounds.Center.X, transportY), scale, accent, alpha,
            active) || overControl;
    }

    private bool DrawPlaybackTimeline(ImDrawListPtr drawList, Rect bounds, float centerY, float duration,
        float scale, Vector4 accent, float alpha, bool active)
    {
        var padX = CardPadX * scale;
        var fraction = playbackScrubbing ? playbackScrubValue : Math.Clamp(playback.Position / duration, 0f, 1f);
        var shownSeconds = fraction * duration;
        var elapsed = TimeText.Duration((int)shownSeconds);
        var remaining = TimeText.Duration(IslandActivities.RemainingSeconds(shownSeconds, duration));
        var style = TextStyles.Caption1;
        var elapsedWidth = WidgetText.TabularWidth(elapsed, style);
        var signWidth = Typography.Measure(RemainingSign, style).X;
        var remainingWidth = WidgetText.TabularWidth(remaining, style) + signWidth;
        var textTop = centerY - Typography.LineHeight(style) * 0.5f;
        var color = Palette.WithAlpha(Ink, PlaybackTimeAlpha * alpha);
        WidgetText.Tabular(drawList, new Vector2(bounds.Min.X + padX, textTop), elapsed, color, style);
        var remainingLeft = bounds.Max.X - padX - remainingWidth;
        Typography.Draw(drawList, new Vector2(remainingLeft, textTop), RemainingSign, color, style);
        WidgetText.Tabular(drawList, new Vector2(remainingLeft + signWidth, textTop), remaining, color, style);
        var gap = PlaybackTimeGap * scale;
        var halfThickness = PlaybackTrackThickness * scale * 0.5f;
        var track = new Rect(new Vector2(bounds.Min.X + padX + elapsedWidth + gap, centerY - halfThickness),
            new Vector2(remainingLeft - gap, centerY + halfThickness));
        if (track.Width <= 0f)
        {
            return false;
        }

        var rail = Palette.WithAlpha(Ink, ControlFillAlpha);
        if (!active || !playback.CanSeek)
        {
            playbackScrubbing = false;
            DrawPlaybackRail(drawList, track, fraction, accent, rail, alpha);
            return false;
        }

        var hovered = Scrubber.IsHovered(track);
        var updated = Scrubber.Draw(track, fraction, accent, rail, alpha);
        var wasScrubbing = playbackScrubbing;
        if ((wasScrubbing || hovered) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            playbackScrubbing = true;
            playbackScrubValue = updated;
            UiInteract.CancelPendingTap();
        }
        else if (wasScrubbing)
        {
            playbackScrubbing = false;
            playback.Seek(playbackScrubValue * duration);
            UiInteract.CancelPendingTap();
        }

        return hovered || wasScrubbing;
    }

    private static void DrawPlaybackRail(ImDrawListPtr drawList, Rect track, float fraction, Vector4 accent,
        Vector4 rail, float alpha)
    {
        var rounding = track.Height * 0.5f;
        drawList.AddRectFilled(track.Min, track.Max, ImGui.GetColorU32(Palette.WithAlpha(rail, alpha)), rounding);
        var fillMax = new Vector2(track.Min.X + track.Width * fraction, track.Max.Y);
        drawList.AddRectFilled(track.Min, fillMax, ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)), rounding);
    }

    private bool DrawPlaybackTransport(ImDrawListPtr drawList, Vector2 center, float scale, Vector4 accent,
        float alpha, bool active)
    {
        var sideRadius = PcControlRadius * scale;
        var playRadius = CardControlRadius * scale;
        var stride = PlaybackTransportStride * scale;
        var previousCenter = center - new Vector2(stride, 0f);
        var nextCenter = center + new Vector2(stride, 0f);
        var extent = new Vector2(playRadius, playRadius);
        var overControl = active && UiInteract.Hover(previousCenter - extent, nextCenter + extent);
        if (TransportButton.Draw(previousCenter, sideRadius, TransportAction.Previous, accent, Ink, alpha, active,
                drawList))
        {
            playback.Previous();
        }

        drawList.AddCircleFilled(center, playRadius,
            ImGui.GetColorU32(Palette.WithAlpha(Ink, ControlFillAlpha * alpha)), 32);
        if (TransportButton.Draw(center, playRadius,
                playback.IsPlaying ? TransportAction.Pause : TransportAction.Play, accent, Ink, alpha, active,
                drawList))
        {
            playback.TogglePlayPause();
        }

        if (TransportButton.Draw(nextCenter, sideRadius, TransportAction.Next, accent, Ink, alpha, active, drawList))
        {
            playback.Next();
        }

        return overControl;
    }
}
