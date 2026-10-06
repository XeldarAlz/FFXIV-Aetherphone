using Aetherphone.Apps.Music.NowPlaying;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const string PcSourceMenuId = "music.pc.source";
    private const string PcRepeatOneLabel = "1";
    private const float PcSourceGlyphFraction = 0.8f;
    private const float PcModeGlyphFraction = 0.34f;
    private const float PcModeIdleAlpha = 0.7f;
    private const float PcModeDotRadius = 2f;
    private const float PcModeDotGap = 5f;
    private const float PcVolumeRowHeight = 30f;
    private const float PcVolumeIconReserve = 28f;
    private const float PcVolumeIconScale = 0.8f;
    private const float PcVolumeSendStep = 0.01f;
    private const long PcVolumeHoldMilliseconds = 1500;

    private readonly DropdownMenu pcSourceMenu = new() { Detached = true };
    private readonly DropdownMenu.Item[] pcSourceItems = new DropdownMenu.Item[PcMediaView.SourceCapacity + 1];
    private readonly string[] pcSourceIds = new string[PcMediaView.SourceCapacity];
    private readonly string[] pcSourceLabels = new string[PcMediaView.SourceCapacity];
    private readonly FontAwesomeIcon[] pcSourceGlyphs = new FontAwesomeIcon[PcMediaView.SourceCapacity];
    private readonly NowPlayingSlider pcVolumeSlider = new();
    private float pcVolumeSent = -1f;
    private float pcVolumeHeld;
    private long pcVolumeHeldUntil;

    private float DrawPcSourceButton(ImDrawListPtr drawList, float right, float top, float lineHeight, Vector4 ink,
        float alpha, bool interactive, out bool hovered)
    {
        var label = Loc.T(L.Music.PcMedia.Source);
        var labelWidth = Typography.Measure(label, TextStyles.Footnote).X;
        var gap = Metrics.Space.Xs * UiScale.Current;
        var reach = Metrics.Space.Xs * UiScale.Current;
        var left = right - labelWidth - gap - lineHeight;
        var min = new Vector2(left - reach, top - reach);
        var max = new Vector2(right + reach, top + lineHeight + reach);
        hovered = interactive && UiInteract.Hover(min, max);
        var highlighted = hovered || pcSourceMenu.IsOpenFor(PcSourceMenuId);
        var color = Palette.WithAlpha(highlighted ? ui.Accent : ink, alpha);
        ProgressRing.CenterIcon(drawList, new Vector2(left + lineHeight * 0.5f, top + lineHeight * 0.5f),
            FontAwesomeIcon.Desktop, color, lineHeight * PcSourceGlyphFraction);
        Typography.Draw(drawList, new Vector2(left + lineHeight + gap, top), label, color, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            pcSourceMenu.Toggle(PcSourceMenuId, new Rect(min, max));
        }

        return min.X;
    }

    private void DrawPcSourceMenu(Rect screen)
    {
        if (!pcSourceMenu.IsOpenFor(PcSourceMenuId))
        {
            return;
        }

        var count = PcMediaView.SourceOptions(pcMedia, pcSourceIds, pcSourceLabels, pcSourceGlyphs,
            out var selected);
        var enabled = pcMedia.IsEnabled;
        for (var index = 0; index < count; index++)
        {
            pcSourceItems[index] = new DropdownMenu.Item(pcSourceLabels[index], IconGlyph.Of(pcSourceGlyphs[index]),
                Selected: enabled && index == selected);
        }

        pcSourceItems[count] = new DropdownMenu.Item(Loc.T(L.Common.Off), IconGlyph.Of(FontAwesomeIcon.PowerOff),
            Selected: !enabled);
        pcSourceMenu.Header = Loc.T(L.Music.PcMedia.SourceMenuHeader);
        var picked = pcSourceMenu.Draw(screen, theme, pcSourceItems.AsSpan(0, count + 1));
        if (picked < 0)
        {
            return;
        }

        if (picked == count)
        {
            pcMedia.TurnOff();
            return;
        }

        pcMedia.Select(pcSourceIds[picked]);
    }

    private bool DrawPcModes(ImDrawListPtr drawList, in MediaSessionSnapshot snapshot, float centerY,
        float shuffleX, float repeatX, float radius, Vector4 ink, float alpha, bool interactive)
    {
        var over = false;
        if (snapshot.CanShuffle)
        {
            var center = new Vector2(shuffleX, centerY);
            over |= TransportButton.Hovered(center, radius);
            if (DrawPcMode(drawList, center, radius, false, snapshot.ShuffleActive, false, ink, alpha, interactive,
                    Loc.T(L.Music.Shuffle)))
            {
                pcMedia.ToggleShuffle(snapshot);
            }
        }

        if (snapshot.CanRepeat)
        {
            var center = new Vector2(repeatX, centerY);
            over |= TransportButton.Hovered(center, radius);
            if (DrawPcMode(drawList, center, radius, true, snapshot.Repeat != MediaSessionRepeat.None,
                    snapshot.Repeat == MediaSessionRepeat.Track, ink, alpha, interactive, Loc.T(L.Music.Repeat)))
            {
                pcMedia.CycleRepeat(snapshot);
            }
        }

        return over;
    }

    private bool DrawPcMode(ImDrawListPtr drawList, Vector2 center, float radius, bool repeat, bool active,
        bool repeatOne, Vector4 ink, float alpha, bool interactive, string tooltip)
    {
        var scale = UiScale.Current;
        var reach = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - reach, center + reach);
        var tint = active ? ui.Accent : hovered ? ink : Palette.WithAlpha(ink, ink.W * PcModeIdleAlpha);
        var color = ImGui.GetColorU32(Palette.WithAlpha(tint, tint.W * alpha));
        var size = radius * PcModeGlyphFraction;
        if (repeat)
        {
            MediaGlyph.Repeat(drawList, center, size, color);
        }
        else
        {
            MediaGlyph.Shuffle(drawList, center, size, color);
        }

        if (repeatOne)
        {
            Typography.DrawCentered(drawList, center, PcRepeatOneLabel, Palette.WithAlpha(tint, tint.W * alpha),
                TextStyles.Caption2);
        }

        if (active)
        {
            drawList.AddCircleFilled(new Vector2(center.X, center.Y + size + PcModeDotGap * scale),
                PcModeDotRadius * scale, color, 12);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(center - reach, center + reach), tooltip, HoverLabelSide.Above);
        }

        return UiInteract.Click(center - reach, center + reach, hovered);
    }

    private void DrawPcVolume(ImDrawListPtr drawList, in MediaSessionSnapshot snapshot, float left, float right,
        float centerY, Vector4 ink, Vector4 muted, bool interactive, float scale)
    {
        var reserve = PcVolumeIconReserve * scale;
        AppSkin.Icon(drawList, new Vector2(left + reserve * 0.3f, centerY), IconGlyph.Of(FontAwesomeIcon.VolumeDown),
            muted, PcVolumeIconScale);
        AppSkin.Icon(drawList, new Vector2(right - reserve * 0.35f, centerY), IconGlyph.Of(FontAwesomeIcon.VolumeUp),
            muted, PcVolumeIconScale);
        var now = Environment.TickCount64;
        var shown = now < pcVolumeHeldUntil ? pcVolumeHeld : snapshot.Volume;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var result = pcVolumeSlider.Draw(drawList, left + reserve, right - reserve, centerY, shown, interactive,
            Palette.WithAlpha(ink, 0.9f), Palette.WithAlpha(ink, PcRailAlpha), delta);
        if (!result.Dragging && !result.Released)
        {
            return;
        }

        pcMediaSheet.ReleasePress();
        pcVolumeHeld = result.Value;
        pcVolumeHeldUntil = now + PcVolumeHoldMilliseconds;
        if (!result.Released && MathF.Abs(result.Value - pcVolumeSent) < PcVolumeSendStep)
        {
            return;
        }

        pcVolumeSent = result.Value;
        pcMedia.SetVolume(result.Value);
    }
}
