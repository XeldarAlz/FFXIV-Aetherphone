using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed partial class DynamicIsland
{
    private const float ArtRadiusFraction = 0.22f;
    private const float CompactArtFraction = 0.84f;
    private const float CompactArtShiftX = 5f;

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
            pcMediaSwipedAway = false;
            return false;
        }

        if (pcMediaSwipedAway)
        {
            if (!current.IsPlaying)
            {
                return false;
            }

            pcMediaSwipedAway = false;
        }

        pcSnapshot = current;
        return true;
    }

    private void DrawPcMediaCompact(ImDrawListPtr drawList, Vector2 bubbleCenter, float bubbleRadius,
        float trailingRight, Rect bounds, float scale, Vector4 accent, float alpha)
    {
        var artRadius = bubbleRadius * CompactArtFraction;
        var side = artRadius * 2f;
        var texture = pcMedia?.Artwork(pcSnapshot, side);
        PcMediaView.DrawArt(drawList, CompactArtCenter(bubbleCenter, scale) - new Vector2(artRadius, artRadius), side,
            side * ArtRadiusFraction, texture, pcSnapshot, accent, alpha);
        Equalizer.Draw(drawList, new Vector2(trailingRight - 3f * scale, bounds.Center.Y), scale,
            bounds.Height * 0.44f, clock, accent, alpha, pcSnapshot.IsPlaying);
    }

    private static Vector2 CompactArtCenter(Vector2 bubbleCenter, float scale)
    {
        return bubbleCenter + new Vector2(CompactArtShiftX * scale, 0f);
    }

    private bool DrawPcMediaExpanded(ImDrawListPtr drawList, Rect bounds, float scale, Vector4 accent, float alpha,
        bool active)
    {
        if (pcMedia is null)
        {
            return false;
        }

        var media = IslandMedia.ForSession(pcSnapshot, PcMediaView.Title(pcSnapshot),
            PcMediaView.Subtitle(pcSnapshot), DateTime.UtcNow.Ticks);
        var artCenter = MediaArtCenter(bounds, scale);
        var radius = CardIconRadius * scale;
        var side = radius * 2f;
        PcMediaView.DrawArt(drawList, artCenter - new Vector2(radius, radius), side, side * ArtRadiusFraction,
            pcMedia.Artwork(pcSnapshot, side), pcSnapshot, accent, alpha);
        return DrawMediaCard(drawList, bounds, artCenter, media, IslandMediaSource.Pc, scale, accent, alpha, active);
    }
}
