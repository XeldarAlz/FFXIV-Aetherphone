using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class LoadingPulse
{
    private const float CometPeriodMs = 1200f;
    private const float CometArcRadians = 2.0f;
    private const float CorePulsePeriodMs = 2600f;
    private const float WordBreathPeriodMs = 2600f;

    public static string SafeLabel() => Plugin.Fonts.Ready ? Loc.T(L.Common.Loading) : "Loading";

    public static void Draw(Vector2 center, float radius, Vector4 accent, Vector4 textColor, string? label,
        float alpha = 1f, float captionFontScale = 0.80f, ImDrawListPtr? drawList = null)
    {
        Spinner(center, radius, accent, alpha, drawList);
        if (label is null)
        {
            return;
        }

        var caret = center.Y + radius + 20f * ImGuiHelpers.GlobalScale;
        Caption(new Vector2(center.X, caret), textColor, accent, label, alpha, captionFontScale, drawList: drawList);
    }

    public static void Spinner(Vector2 center, float radius, Vector4 accent, float alpha = 1f,
        ImDrawListPtr? drawList = null)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var dl = drawList ?? ImGui.GetWindowDrawList();
        var thickness = MathF.Max(2f * ImGuiHelpers.GlobalScale, radius * 0.10f);
        dl.AddCircleFilled(center, radius * 1.9f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.06f * alpha)), 48);
        dl.AddCircleFilled(center, radius * 1.25f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.10f * alpha)), 48);
        dl.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.18f * alpha)), 72, thickness);
        ProgressRing.Sweep(center, radius, thickness, accent, CometPeriodMs, CometArcRadians, alpha, dl);
        var core = Palette.Mix(accent, Vector4.One, 0.7f);
        var pulse = 0.9f + 0.1f * Styling.Pulse(CorePulsePeriodMs);
        dl.AddCircleFilled(center, radius * 0.26f * pulse, ImGui.GetColorU32(Palette.WithAlpha(core, alpha)), 32);
    }

    public static void Caption(Vector2 center, Vector4 textColor, Vector4 accent, string label, float alpha,
        float fontScale, FontWeight weight = FontWeight.Medium, ImDrawListPtr? drawList = null)
    {
        var dl = drawList ?? ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        label = label.TrimEnd('…', '.', ' ');
        using (Plugin.Fonts.Push(fontScale, weight))
        {
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var labelSize = ImGui.CalcTextSize(label);
            var dotSpacing = 8f * scale;
            var dotRadius = 2.3f * scale;
            var gap = 9f * scale;
            var totalWidth = labelSize.X + gap + 3f * dotSpacing;
            var startX = center.X - totalWidth * 0.5f;
            var wordBreath = 0.78f + 0.12f * Styling.Pulse(WordBreathPeriodMs);
            dl.AddText(font, fontSize, new Vector2(startX, center.Y - labelSize.Y * 0.5f),
                ImGui.GetColorU32(Palette.WithAlpha(textColor, alpha * wordBreath)), label);
            Dots(new Vector2(startX + labelSize.X + gap + dotRadius, center.Y), dotSpacing, dotRadius, accent, alpha,
                dl);
        }
    }

    public static void Dots(Vector2 origin, float spacing, float radius, Vector4 accent, float alpha,
        ImDrawListPtr? drawList = null)
    {
        var dl = drawList ?? ImGui.GetWindowDrawList();
        var time = ImGui.GetTime();
        for (var index = 0; index < 3; index++)
        {
            var wave = 0.5f + 0.5f * MathF.Sin((float)(time * 4.2 - index * 0.9));
            var dotAlpha = alpha * (0.30f + 0.70f * wave);
            var dotRadius = radius * (0.8f + 0.5f * wave);
            dl.AddCircleFilled(new Vector2(origin.X + index * spacing, origin.Y), dotRadius,
                ImGui.GetColorU32(Palette.WithAlpha(accent, dotAlpha)), 16);
        }
    }
}
