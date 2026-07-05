using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace Aetherphone.Windows.Components;

internal static class ProgressRing
{
    private const float Top = -MathF.PI / 2f;
    private static Vector2 Dir(float a) => new(MathF.Cos(a), MathF.Sin(a));

    private static void Arc(Vector2 c, float r, float thickness, float a0, float a1, uint col)
    {
        var dl = ImGui.GetWindowDrawList();
        var span = MathF.Abs(a1 - a0);
        var seg = Math.Max(2, (int)MathF.Ceiling(span / (MathF.PI / 48f)));
        var prev = c + Dir(a0) * r;
        for (var i = 1; i <= seg; i++)
        {
            var a = a0 + (a1 - a0) * (i / (float)seg);
            var cur = c + Dir(a) * r;
            dl.AddLine(prev, cur, col, thickness);
            prev = cur;
        }

        var cap = thickness * 0.5f;
        dl.AddCircleFilled(c + Dir(a0) * r, cap, col);
        dl.AddCircleFilled(c + Dir(a1) * r, cap, col);
    }

    public static void Glow(Vector2 c, float radius, Vector4 color, float intensity)
    {
        var dl = ImGui.GetWindowDrawList();
        for (var i = 4; i >= 1; i--)
        {
            var r = radius * (0.72f + i * 0.17f);
            var a = Math.Clamp(intensity * 0.05f * (5 - i), 0f, 0.5f);
            dl.AddCircleFilled(c, r, ImGui.GetColorU32(Palette.WithAlpha(color, a)));
        }
    }

    public static void Disc(Vector2 c, float radius, Vector4 color) =>
        ImGui.GetWindowDrawList().AddCircleFilled(c, radius, ImGui.GetColorU32(color));

    public static void Track(Vector2 c, float r, float thickness, Vector4 col) =>
        Arc(c, r, thickness, Top, Top + MathF.PI * 2f, ImGui.GetColorU32(col));

    public static void Fill(Vector2 c, float r, float thickness, float fraction, Vector4 col)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        if (fraction <= 0.0001f) return;
        Arc(c, r, thickness, Top, Top + fraction * MathF.PI * 2f, ImGui.GetColorU32(col));
    }

    public static void Sweep(Vector2 c, float r, float thickness, Vector4 col, double periodMs, float arcLen,
        float headAlpha)
    {
        var dl = ImGui.GetWindowDrawList();
        var head = Top + Styling.Phase(periodMs) * MathF.PI * 2f;
        var tail = head - arcLen;
        var steps = Math.Max(10, (int)MathF.Ceiling(arcLen / (MathF.PI / 36f)));
        var prev = c + Dir(tail) * r;
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var a = tail + (head - tail) * t;
            var cur = c + Dir(a) * r;
            dl.AddLine(prev, cur, ImGui.GetColorU32(Palette.WithAlpha(col, headAlpha * t * t)), thickness);
            prev = cur;
        }

        dl.AddCircleFilled(c + Dir(head) * r, thickness * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(col, headAlpha)));
    }

    public static void CenterValue(Vector2 c, string big, string? small, Vector4 bigCol, Vector4 smallCol,
        in TextStyle bigStyle)
    {
        var bs = Typography.Measure(big, bigStyle);
        var hasSmall = !string.IsNullOrEmpty(small);
        var ss = hasSmall ? Typography.Measure(small!, TextStyles.Footnote) : Vector2.Zero;
        var gap = hasSmall ? 2f * ImGuiHelpers.GlobalScale : 0f;
        var top = c.Y - (bs.Y + gap + ss.Y) * 0.5f;
        Typography.Draw(new Vector2(c.X - bs.X * 0.5f, top), big, bigCol, bigStyle);
        if (hasSmall)
        {
            Typography.Draw(new Vector2(c.X - ss.X * 0.5f, top + bs.Y + gap), small!, smallCol, TextStyles.Footnote);
        }
    }

    public static void CenterIcon(ImDrawListPtr dl, Vector2 c, FontAwesomeIcon icon, Vector4 col, float targetHeight)
    {
        var glyph = icon.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var font = ImGui.GetFont();
            var baseSize = ImGui.GetFontSize();
            var measured = ImGui.CalcTextSize(glyph);
            var scale = measured.Y > 0f ? targetHeight / measured.Y : 1f;
            var size = measured * scale;
            dl.AddText(font, baseSize * scale, new Vector2(c.X - size.X * 0.5f, c.Y - size.Y * 0.5f),
                ImGui.GetColorU32(col), glyph);
        }
    }

    public static void CenterIcon(Vector2 c, FontAwesomeIcon icon, Vector4 col, float targetHeight)
    {
        var glyph = icon.ToIconString();
        float baseH;
        using (ImRaii.PushFont(UiBuilder.IconFont)) baseH = ImGui.CalcTextSize(glyph).Y;
        var scale = baseH > 0 ? targetHeight / baseH : 1f;
        ImGui.SetWindowFontScale(scale);
        Vector2 sz;
        using (ImRaii.PushFont(UiBuilder.IconFont)) sz = ImGui.CalcTextSize(glyph);
        ImGui.SetCursorScreenPos(new Vector2(c.X - sz.X * 0.5f, c.Y - sz.Y * 0.5f));
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, col))
            ImGui.TextUnformatted(glyph);
        ImGui.SetWindowFontScale(1f);
    }

    public static bool PlayButton(Vector2 c, float radius, bool enabled)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = c - new Vector2(radius, radius);
        var max = c + new Vector2(radius, radius);
        var hovered = enabled && ImGui.IsMouseHoveringRect(min, max);
        var accent = Styling.AccentViolet;
        var thickness = 4.5f * ImGuiHelpers.GlobalScale;
        if (enabled)
            Glow(c, radius, accent, 0.85f + (hovered ? 1.0f : 0f) + 0.55f * Styling.Pulse(Styling.PulseBreath));
        dl.AddCircleFilled(c, radius - thickness * 0.5f,
            ImGui.GetColorU32(enabled
                ? Vector4.Lerp(Styling.CardBg, accent, hovered ? 0.30f : 0.15f)
                : Styling.CardBgSoft));
        Track(c, radius, thickness,
            enabled ? Palette.WithAlpha(accent, hovered ? 1f : 0.78f) : Palette.WithAlpha(Styling.BorderDim, 0.85f));
        var glyph = enabled ? FontAwesomeIcon.Play : FontAwesomeIcon.Lock;
        var glyphCol = enabled ? (hovered ? Styling.TextStrong : Styling.AccentVioletSoft) : Styling.TextMuted;
        // A play triangle is visually heavier on its left edge; nudge right so it reads centred.
        var nudge = enabled ? new Vector2(radius * 0.07f, 0f) : Vector2.Zero;
        CenterIcon(c + nudge, glyph, glyphCol, radius * (enabled ? 0.78f : 0.62f));
        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(max - min);
        if (!enabled) return false;
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
