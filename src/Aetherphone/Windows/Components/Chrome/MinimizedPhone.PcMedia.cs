using Aetherphone.Core;
using Aetherphone.Core.SystemMedia;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class MinimizedPhone
{
    private readonly PcMediaSource pcMedia;
    private MediaSessionSnapshot pcSnapshot = MediaSessionSnapshot.Empty;
    private bool pcMusicShown;

    private bool ReadPcMusic()
    {
        ref readonly var current = ref pcMedia.Current;
        if (!PcMediaSource.IsLive(current))
        {
            return false;
        }

        pcSnapshot = current;
        pcMusicShown = true;
        return true;
    }

    private bool ShowsPcMusic()
    {
        if (playback.IsActive)
        {
            pcMusicShown = false;
        }

        return pcMusicShown;
    }

    private void DrawPcMusicCard(ImDrawListPtr drawList, Rect inner, float alpha, bool active)
    {
        var scale = frameScale;
        var art = MinimizedPhoneRenderer.MusicArtRect(inner, scale);
        var texture = pcMedia.Artwork(pcSnapshot, art.Width);
        PcMediaView.DrawArt(drawList, art.Min, art.Width, MinimizedPhoneRenderer.ArtRadius(scale), texture,
            pcSnapshot, MusicAccent, alpha);
        MinimizedPhoneRenderer.DrawMusicText(drawList, inner, "minimized.pcmusic", PcMediaView.Title(pcSnapshot),
            PcMediaView.Subtitle(pcSnapshot), frameInk, alpha, scale);
        var state = new TransportState(pcSnapshot.CanPrevious, pcSnapshot.CanNext, pcSnapshot.CanPlayPause,
            pcSnapshot.IsPlaying);
        var result = MinimizedPhoneRenderer.DrawTransport(drawList, inner, state, frameInk, alpha, active, scale);
        ApplyPcMusicControl(result.Action);
        controlHovered |= result.Hovered;
    }

    private void ApplyPcMusicControl(MinimizedControl control)
    {
        switch (control)
        {
            case MinimizedControl.Previous:
                pcMedia.Previous();
                break;
            case MinimizedControl.Next:
                pcMedia.Next();
                break;
            case MinimizedControl.PlayPause:
                pcMedia.TogglePlayPause(pcSnapshot);
                break;
        }
    }
}
