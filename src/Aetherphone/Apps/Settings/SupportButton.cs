using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings;

internal static class SupportButton
{
    public const float GlowPadding = 14f;

    private const float ButtonHeight = 56f;
    private const double HueBlendMs = 5200.0;
    private const double HeartbeatMs = 1500.0;
    private const double SheenMs = 3200.0;

    public static bool Draw(string label, PhoneTheme theme, string? hint = null)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var glowPad = GlowPadding * scale;
        var slotOrigin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        var origin = new Vector2(slotOrigin.X + glowPad, slotOrigin.Y + glowPad);
        var size = new Vector2(available - glowPad * 2f, ButtonHeight * scale);
        var end = origin + size;
        var hovered = UiInteract.Hover(origin, end);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var rounding = size.Y * 0.5f;

        var accent = Pulse.Blend(Accent.Rose, Accent.Violet, HueBlendMs);
        var fill = (hovered ? Palette.Lighten(accent, 0.14f) : accent) with { W = 1f };
        if (pressed)
        {
            fill = Palette.Darken(fill, 0.08f);
        }

        var glowPulse = 0.5f + 0.5f * Pulse.Wave(Pulse.Breath);
        for (var ring = 3; ring >= 1; ring--)
        {
            var grow = ring * 3.2f * scale;
            var glowAlpha = 0.055f * ring * glowPulse * (hovered ? 1.9f : 1f);
            Squircle.Fill(drawList, origin - new Vector2(grow, grow), end + new Vector2(grow, grow),
                rounding + grow, ImGui.GetColorU32(Palette.WithAlpha(fill, glowAlpha)));
        }

        Squircle.Fill(drawList, origin, end, rounding, ImGui.GetColorU32(fill));
        Material.Sheen(drawList, origin, end, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.24f)), 1f,
            1.5f * scale);
        Sheen(drawList, origin, size);
        Squircle.Stroke(drawList, origin, end, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 0.44f : 0.20f)), 1f * scale);
        DrawContent(drawList, origin, size, label, scale);

        ImGui.SetCursorScreenPos(slotOrigin);
        ImGui.Dummy(new Vector2(available, size.Y + glowPad * 2f));
        if (hint is not null)
        {
            HoverTooltip.Show(new Rect(origin, end), hint);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(origin, end, hovered);
    }

    private static void DrawContent(ImDrawListPtr drawList, Vector2 origin, Vector2 size, string label, float scale)
    {
        var ink = new Vector4(1f, 1f, 1f, 1f);
        var heartGlyph = IconGlyph.Of(FontAwesomeIcon.Heart);
        Vector2 iconSize;
        using (Plugin.Fonts.PushDalamudIcon())
        {
            iconSize = ImGui.CalcTextSize(heartGlyph);
        }

        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var innerGap = 11f * scale;
        var contentWidth = iconSize.X + innerGap + labelSize.X;
        var startX = origin.X + (size.X - contentWidth) * 0.5f;
        var midY = origin.Y + size.Y * 0.5f;
        var beat = Heartbeat(HeartbeatMs);
        var iconHeight = iconSize.Y * (0.96f + 0.20f * beat);
        ProgressRing.CenterIcon(drawList, new Vector2(startX + iconSize.X * 0.5f, midY), FontAwesomeIcon.Heart, ink,
            iconHeight);
        Typography.Draw(drawList, new Vector2(startX + iconSize.X + innerGap, midY - labelSize.Y * 0.5f), label, ink,
            TextStyles.Headline);
    }

    private static void Sheen(ImDrawListPtr drawList, Vector2 origin, Vector2 size)
    {
        var phase = Pulse.Phase(SheenMs);
        if (phase > 0.32f)
        {
            return;
        }

        var sweep = phase / 0.32f;
        drawList.PushClipRect(origin, origin + size, true);
        var slant = size.Y * 0.55f;
        var travel = size.X + slant + 40f;
        var centerX = origin.X - 20f + sweep * travel;
        const int half = 16;
        for (var offset = -half; offset <= half; offset++)
        {
            var alpha = 0.18f * (1f - MathF.Abs(offset) / half);
            var x = centerX + offset;
            drawList.AddLine(new Vector2(x + slant, origin.Y), new Vector2(x, origin.Y + size.Y),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 1.3f);
        }

        drawList.PopClipRect();
    }

    private static float Heartbeat(double periodMs)
    {
        var phase = Pulse.Phase(periodMs);
        return MathF.Max(Bump(phase, 0.06f, 0.06f), Bump(phase, 0.20f, 0.06f) * 0.6f);
    }

    private static float Bump(float phase, float center, float width)
    {
        var distance = (phase - center) / width;
        if (distance < -1f || distance > 1f)
        {
            return 0f;
        }

        return 0.5f * (1f + MathF.Cos(distance * MathF.PI));
    }
}
