using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed partial class DynamicIsland
{
    private const float PcControlRadius = 15f;
    private const float PcControlStride = 31f;
    private const float PcArtRadiusFraction = 0.22f;

    private readonly PcMediaSource? pcMedia;
    private MediaSessionSnapshot pcSnapshot = MediaSessionSnapshot.Empty;

    private bool ReadPcMedia(bool outranked)
    {
        if (outranked || pcMedia is null)
        {
            return false;
        }

        ref readonly var current = ref pcMedia.Current;
        if (!PcMediaSource.IsLive(current))
        {
            return false;
        }

        pcSnapshot = current;
        return true;
    }

    private void DrawPcMediaCompact(ImDrawListPtr drawList, Vector2 bubbleCenter, float bubbleRadius,
        float trailingRight, Rect bounds, float scale, Vector4 accent, float alpha)
    {
        var texture = pcMedia?.Artwork(pcSnapshot, bubbleRadius * 2f);
        PcMediaView.DrawDisc(drawList, bubbleCenter, bubbleRadius, texture, pcSnapshot, accent, alpha);
        Equalizer.Draw(drawList, new Vector2(trailingRight - 3f * scale, bounds.Center.Y), scale,
            bounds.Height * 0.44f, clock, accent, alpha, pcSnapshot.IsPlaying);
    }

    private bool DrawPcMediaExpanded(ImDrawListPtr drawList, Rect bounds, float scale, Vector4 accent, float alpha,
        bool active)
    {
        if (pcMedia is null)
        {
            return false;
        }

        var side = CardIconRadius * 2f * scale;
        var artMin = new Vector2(bounds.Min.X + CardPadX * scale, bounds.Center.Y - side * 0.5f);
        PcMediaView.DrawArt(drawList, artMin, side, side * PcArtRadiusFraction, pcMedia.Artwork(pcSnapshot, side),
            pcSnapshot, accent, alpha);
        var radius = PcControlRadius * scale;
        var stride = PcControlStride * scale;
        var nextCenter = new Vector2(bounds.Max.X - CardPadX * scale - radius, bounds.Center.Y);
        var playCenter = nextCenter - new Vector2(stride, 0f);
        var previousCenter = playCenter - new Vector2(stride, 0f);
        var textLeft = artMin.X + side + CardTextGap * scale;
        var textWidth = MathF.Max(1f, previousCenter.X - radius - CardTextGap * scale - textLeft);
        DrawLines(drawList, PcMediaView.Title(pcSnapshot), TextStyles.Headline, Ink, PcMediaView.Subtitle(pcSnapshot),
            TextStyles.Subheadline, accent, textLeft, textWidth, bounds.Center.Y, scale, alpha, true);
        var extent = new Vector2(radius, radius);
        var overControl = active && UiInteract.Hover(previousCenter - extent, nextCenter + extent);
        if (pcSnapshot.CanPrevious && TransportButton.Draw(previousCenter, radius, TransportAction.Previous, accent,
                Ink, alpha, active, drawList))
        {
            pcMedia.Previous();
        }

        drawList.AddCircleFilled(playCenter, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Ink, ControlFillAlpha * alpha)), 32);
        var playAlpha = pcSnapshot.CanPlayPause ? alpha : alpha * 0.4f;
        if (TransportButton.Draw(playCenter, radius,
                pcSnapshot.IsPlaying ? TransportAction.Pause : TransportAction.Play, accent, Ink, playAlpha,
                active && pcSnapshot.CanPlayPause, drawList))
        {
            pcMedia.TogglePlayPause(pcSnapshot);
        }

        if (pcSnapshot.CanNext && TransportButton.Draw(nextCenter, radius, TransportAction.Next, accent, Ink, alpha,
                active, drawList))
        {
            pcMedia.Next();
        }

        return overControl;
    }
}
