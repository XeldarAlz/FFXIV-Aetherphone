using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal static class RaceLine
{
    private const float Slack = 0.5f;

    public static float DrawLeft(ImDrawListPtr drawList, float left, float centerY, string text, float maxWidth,
        in TextStyle style, in TextStyle floor, Vector4 ink)
    {
        var shown = Fit(text, maxWidth, style, floor, out var fontScale, out var size);
        Typography.Draw(drawList, new Vector2(left, centerY - size.Y * 0.5f), shown, ink, fontScale, style.Weight);
        return size.X;
    }

    public static float DrawRight(ImDrawListPtr drawList, float right, float centerY, string text, float maxWidth,
        in TextStyle style, in TextStyle floor, Vector4 ink)
    {
        var shown = Fit(text, maxWidth, style, floor, out var fontScale, out var size);
        Typography.Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), shown, ink, fontScale,
            style.Weight);
        return size.X;
    }

    public static Vector2 DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth,
        in TextStyle style, in TextStyle floor, Vector4 ink)
    {
        var shown = Fit(text, maxWidth, style, floor, out var fontScale, out var size);
        Typography.Draw(drawList, center - size * 0.5f, shown, ink, fontScale, style.Weight);
        return size;
    }

    public static Vector2 Measure(string text, float maxWidth, in TextStyle style, in TextStyle floor)
    {
        Fit(text, maxWidth, style, floor, out _, out var size);
        return size;
    }

    private static string Fit(string text, float maxWidth, in TextStyle style, in TextStyle floor,
        out float fontScale, out Vector2 size)
    {
        var bounded = MathF.Max(1f, maxWidth);
        fontScale = Typography.FitScale(text, bounded, style.Scale, MathF.Min(style.Scale, floor.Scale),
            style.Weight);
        size = Typography.Measure(text, fontScale, style.Weight);
        if (size.X <= bounded + Slack)
        {
            return text;
        }

        var shown = Typography.FitText(text, bounded, fontScale, style.Weight);
        size = Typography.Measure(shown, fontScale, style.Weight);
        return shown;
    }
}
