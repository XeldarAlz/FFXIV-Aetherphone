using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class NowPlayingIsland
{
    private const ImGuiWindowFlags IslandFlags =
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private static readonly Vector4 Accent = new(0.99f, 0.42f, 0.58f, 1f);
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);

    private const float ExpandedHeight = 120f;
    private const float ExpandedHalfWidth = 142f;
    private const float CompactPadX = 20f;
    private const float CompactPadY = 5f;
    private const float ControlThreshold = 0.6f;

    private readonly PlaybackHub playback;

    private float expand;
    private float clock;

    public NowPlayingIsland(PlaybackHub playback)
    {
        this.playback = playback;
    }

    public bool CapturesPointer(Rect screen)
    {
        if (!playback.IsActive)
        {
            return false;
        }

        var bounds = PreviewBounds(screen);
        return ImGui.IsMouseHoveringRect(bounds.Min, bounds.Max);
    }

    public void Draw(Rect screen, PhoneTheme theme, INavigator navigation)
    {
        if (!playback.IsActive)
        {
            expand = 0f;
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##nowPlayingIsland", screen.Size, false, IslandFlags))
        {
            DrawContent(screen, theme, navigation);
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, INavigator navigation)
    {
        var playing = playback.IsPlaying;
        var scale = ImGuiHelpers.GlobalScale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        clock += delta;

        var rest = StatusBar.BaseIsland(screen);
        var compact = CompactBounds(rest, scale);
        var expanded = ExpandedBounds(screen, rest, scale);

        var hoverBounds = LerpRect(compact, expanded, Ease(expand));
        var hovered = ImGui.IsMouseHoveringRect(hoverBounds.Min, hoverBounds.Max);

        var target = hovered ? 1f : 0f;
        expand = Math.Clamp(expand + (target - expand) * MathF.Min(1f, delta * 16f), 0f, 1f);

        var eased = Ease(expand);
        var bounds = LerpRect(compact, expanded, eased);
        var collapsedAlpha = 1f - eased;
        var dl = ImGui.GetWindowDrawList();

        DrawPulse(dl, compact, scale, collapsedAlpha);

        var rounding = float.Lerp(compact.Height * 0.5f, 30f * scale, eased);

        Elevation.Draw(dl, bounds.Min, bounds.Max, rounding, scale, 5f + 5f * eased, 3f, 0.30f + 0.16f * eased);

        dl.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(theme.BezelOuter), rounding);
        dl.AddRect(bounds.Min, bounds.Max, ImGui.GetColorU32(Palette.WithAlpha(Accent, 0.14f + 0.46f * eased)), rounding, ImDrawFlags.RoundCornersAll, 1.5f * scale);

        DrawCompact(dl, compact, scale, playing, collapsedAlpha);
        var consumed = DrawExpanded(dl, bounds, scale, theme, eased);

        if (consumed || !hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (eased < 0.5f && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            navigation.Open("music");
        }
    }

    private void DrawPulse(ImDrawListPtr dl, Rect compact, float scale, float collapsedAlpha)
    {
        if (collapsedAlpha <= 0.02f)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 2.4f);
        var spread = (2.5f + 4f * pulse) * scale;
        var alpha = (0.18f + 0.26f * pulse) * collapsedAlpha;
        var rounding = compact.Height * 0.5f + spread;
        dl.AddRect(compact.Min - new Vector2(spread, spread), compact.Max + new Vector2(spread, spread), ImGui.GetColorU32(Palette.WithAlpha(Accent, alpha)), rounding, ImDrawFlags.RoundCornersAll, 2.4f * scale);
    }

    private void DrawCompact(ImDrawListPtr dl, Rect compact, float scale, bool playing, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var discRadius = compact.Height * 0.34f;
        var discCenter = new Vector2(compact.Min.X + 9f * scale + discRadius, compact.Center.Y);
        ArtGradient.DrawDisc(dl, discCenter, discRadius, ArtGradient.FromName(playback.Title), alpha);

        var eqCenter = new Vector2(compact.Max.X - 13f * scale, compact.Center.Y);
        Equalizer.Draw(dl, eqCenter, scale, compact.Height * 0.5f, clock, Accent, alpha, playing);
    }

    private bool DrawExpanded(ImDrawListPtr dl, Rect bounds, float scale, PhoneTheme theme, float alpha)
    {
        if (alpha <= 0.05f)
        {
            return false;
        }

        var left = bounds.Min.X;
        var top = bounds.Min.Y;
        var centerX = bounds.Center.X;

        var discRadius = 19f * scale;
        var discCenter = new Vector2(left + 18f * scale + discRadius, top + 30f * scale);
        ArtGradient.DrawDisc(dl, discCenter, discRadius, ArtGradient.FromName(playback.Title), alpha);

        var textLeft = discCenter.X + discRadius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, top + 18f * scale), Truncate(playback.Title, 16), Palette.WithAlpha(theme.TextStrong, alpha), 1.0f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, top + 40f * scale), Truncate(playback.Subtitle, 18), Palette.WithAlpha(Accent, 0.9f * alpha), 0.8f);

        var active = alpha > ControlThreshold;
        var controlY = top + 66f * scale;
        var consumed = false;

        if (playback.HasQueue)
        {
            if (TransportButton.Draw(new Vector2(centerX - 46f * scale, controlY), 16f * scale, TransportAction.Previous, Accent, Ink, alpha, active))
            {
                playback.Previous();
                consumed = true;
            }

            if (TransportButton.Draw(new Vector2(centerX + 46f * scale, controlY), 16f * scale, TransportAction.Next, Accent, Ink, alpha, active))
            {
                playback.Next();
                consumed = true;
            }
        }

        if (TransportButton.Draw(new Vector2(centerX, controlY), 18f * scale, TransportAction.Stop, Accent, Ink, alpha, active))
        {
            playback.Stop();
            consumed = true;
        }

        if (active)
        {
            var trackY = top + 99f * scale;
            var track = new Rect(new Vector2(left + 22f * scale, trackY - 2.5f * scale), new Vector2(bounds.Max.X - 22f * scale, trackY + 2.5f * scale));
            playback.Volume = Scrubber.Draw(track, playback.Volume, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f), alpha);
        }

        return consumed;
    }

    private Rect PreviewBounds(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rest = StatusBar.BaseIsland(screen);
        return LerpRect(CompactBounds(rest, scale), ExpandedBounds(screen, rest, scale), Ease(expand));
    }

    private static Rect CompactBounds(Rect rest, float scale)
    {
        return new Rect(
            rest.Min - new Vector2(CompactPadX * scale, CompactPadY * scale),
            rest.Max + new Vector2(CompactPadX * scale, CompactPadY * scale));
    }

    private static Rect ExpandedBounds(Rect screen, Rect rest, float scale)
    {
        var halfWidth = MathF.Min(screen.Width * 0.5f - 14f * scale, ExpandedHalfWidth * scale);
        var centerX = screen.Center.X;
        var top = rest.Min.Y - 2f * scale;
        return new Rect(new Vector2(centerX - halfWidth, top), new Vector2(centerX + halfWidth, top + ExpandedHeight * scale));
    }

    private static Rect LerpRect(Rect from, Rect to, float t)
    {
        return new Rect(Vector2.Lerp(from.Min, to.Min, t), Vector2.Lerp(from.Max, to.Max, t));
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max - 1) + "…";
    }
}
