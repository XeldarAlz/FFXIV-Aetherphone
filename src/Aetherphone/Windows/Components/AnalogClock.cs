using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class AnalogClock
{
    private static readonly Vector4 DayFace = new(0.90f, 0.93f, 0.98f, 1f);
    private static readonly Vector4 NightFace = new(0.19f, 0.21f, 0.31f, 1f);
    private static readonly Vector4 DayInk = new(0.20f, 0.23f, 0.32f, 1f);
    private static readonly Vector4 NightInk = new(0.85f, 0.89f, 0.96f, 1f);

    public static void
        Draw(Vector2 center, float radius, float hours, float minutes, float seconds, PhoneTheme theme) =>
        Draw(center, radius, hours, minutes, seconds, DayFraction(hours), theme);

    public static void Draw(Vector2 center, float radius, float hours, float minutes, float seconds, float dayFraction,
        PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var face = Vector4.Lerp(NightFace, DayFace, dayFraction);
        var ink = Luminance(face) > 0.5f ? DayInk : NightInk;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(face), 64);
        drawList.AddCircleFilled(center - new Vector2(0f, radius * 0.4f), radius * 0.62f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f + 0.05f * dayFraction)), 48);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ink, 0.28f)), 64, 1.4f * scale);
        for (var tick = 0; tick < 12; tick++)
        {
            var angle = tick * (MathF.PI / 6f) - MathF.PI / 2f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var major = tick % 3 == 0;
            var inner = center + direction * radius * (major ? 0.74f : 0.82f);
            var outer = center + direction * radius * 0.91f;
            drawList.AddLine(inner, outer, ImGui.GetColorU32(Palette.WithAlpha(ink, major ? 0.9f : 0.5f)),
                (major ? 2.0f : 1.1f) * scale);
        }

        var hourAngle = (hours % 12f + minutes / 60f) * (MathF.PI / 6f) - MathF.PI / 2f;
        var minuteAngle = (minutes + seconds / 60f) * (MathF.PI / 30f) - MathF.PI / 2f;
        var secondAngle = seconds * (MathF.PI / 30f) - MathF.PI / 2f;
        Hand(drawList, center, hourAngle, radius * 0.50f, radius * 0.16f, 3.4f * scale, ink);
        Hand(drawList, center, minuteAngle, radius * 0.76f, radius * 0.18f, 2.6f * scale, ink);
        Hand(drawList, center, secondAngle, radius * 0.84f, radius * 0.22f, 1.3f * scale, theme.Accent);
        drawList.AddCircleFilled(center, 3.0f * scale, ImGui.GetColorU32(theme.Accent), 16);
        drawList.AddCircleFilled(center, 1.4f * scale, ImGui.GetColorU32(face), 12);
    }

    private static void Hand(ImDrawListPtr drawList, Vector2 center, float angle, float length, float tail,
        float thickness, Vector4 color)
    {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var tip = center + direction * length;
        var back = center - direction * tail;
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(back, tip, packed, thickness);
        drawList.AddCircleFilled(tip, thickness * 0.5f, packed, 12);
        drawList.AddCircleFilled(back, thickness * 0.5f, packed, 8);
    }

    private static float Luminance(Vector4 color) => color.X * 0.299f + color.Y * 0.587f + color.Z * 0.114f;

    private static float DayFraction(float hours)
    {
        var normalized = ((hours % 24f) + 24f) % 24f;
        return 0.5f - 0.5f * MathF.Cos(normalized / 24f * MathF.PI * 2f);
    }
}
