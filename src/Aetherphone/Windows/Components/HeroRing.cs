using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

/// <summary>
/// The shared "hero" progress ring used at the top of summary apps (Dailies, Timers): a glowing
/// accent ring with a value-or-icon centre and a title + subtitle beneath it, reserving a fixed
/// block of vertical space. Callers supply the fraction, palette inks and labels.
/// </summary>
internal static class HeroRing
{
    private const float CenterOffsetY = 86f;
    private const float Radius = 56f;
    private const float Thickness = 7f;
    private const float ReservedHeight = 196f;
    private const float TitleGap = 26f;
    private const float SubtitleGap = 50f;
    private const float IconFactor = 0.62f;

    public static void Draw(float fraction, Vector4 accent, Vector4 titleInk, Vector4 mutedInk, string value,
        string? valueCaption, string title, string subtitle)
    {
        var (ringCenter, radius) = Frame(fraction, accent, titleInk);
        ProgressRing.CenterValue(ringCenter, value, valueCaption, titleInk, mutedInk, TextStyles.LargeTitle);
        Labels(ringCenter, radius, titleInk, mutedInk, title, subtitle);
    }

    public static void Draw(float fraction, Vector4 accent, Vector4 titleInk, Vector4 mutedInk, FontAwesomeIcon icon,
        string title, string subtitle)
    {
        var (ringCenter, radius) = Frame(fraction, accent, titleInk);
        ProgressRing.CenterIcon(ringCenter, icon, accent, radius * IconFactor);
        Labels(ringCenter, radius, titleInk, mutedInk, title, subtitle);
    }

    private static (Vector2 Center, float Radius) Frame(float fraction, Vector4 accent, Vector4 titleInk)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var ringCenter = new Vector2(origin.X + width * 0.5f, origin.Y + CenterOffsetY * scale);
        var radius = Radius * scale;
        var thickness = Thickness * scale;
        var clamped = Math.Clamp(fraction, 0f, 1f);
        ProgressRing.Glow(ringCenter, radius, accent, 0.45f + 0.30f * Styling.Pulse(Styling.PulseBreath));
        ProgressRing.Track(ringCenter, radius, thickness, Palette.WithAlpha(titleInk, 0.10f));
        ProgressRing.Fill(ringCenter, radius, thickness, clamped, accent);
        return (ringCenter, radius);
    }

    private static void Labels(Vector2 ringCenter, float radius, Vector4 titleInk, Vector4 mutedInk, string title,
        string subtitle)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.DrawCentered(new Vector2(ringCenter.X, ringCenter.Y + radius + TitleGap * scale), title, titleInk,
            TextStyles.Title3);
        Typography.DrawCentered(new Vector2(ringCenter.X, ringCenter.Y + radius + SubtitleGap * scale), subtitle,
            mutedInk, TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ReservedHeight * scale));
    }
}
