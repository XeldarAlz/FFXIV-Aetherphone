using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal static class StagePill
{
    public const float Height = 28f;
    private const float PadX = 12f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float Opacity = 0.9f;
    private static readonly TextStyle Style = TextStyles.FootnoteEmphasized;

    public static float Width(string text, bool withIcon, float scale)
    {
        var icon = withIcon ? (IconSize + IconGap) * scale : 0f;
        return PadX * scale * 2f + icon + Typography.Measure(text, Style).X;
    }

    public static Rect Around(Vector2 center, float width, float scale)
    {
        var half = new Vector2(width * 0.5f, Height * scale * 0.5f);
        return new Rect(center - half, center + half);
    }

    public static void Draw(ImDrawListPtr drawList, Rect rect, string text, Vector4 ink, float alpha, float scale)
    {
        Material.Frosted(drawList, rect.Min, rect.Max, rect.Height * 0.5f, scale, Opacity * alpha);
        Typography.Draw(drawList, TextOrigin(rect.Min.X + PadX * scale, rect.Center.Y), text, ink with { W = alpha },
            Style);
    }

    public static void Draw(ImDrawListPtr drawList, Rect rect, FontAwesomeIcon icon, Vector4 iconColor, string text,
        Vector4 ink, float alpha, float scale)
    {
        Material.Frosted(drawList, rect.Min, rect.Max, rect.Height * 0.5f, scale, Opacity * alpha);
        var left = rect.Min.X + PadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + IconSize * scale * 0.5f, rect.Center.Y), icon,
            iconColor with { W = alpha }, IconSize * scale);
        Typography.Draw(drawList, TextOrigin(left + (IconSize + IconGap) * scale, rect.Center.Y), text,
            ink with { W = alpha }, Style);
    }

    private static Vector2 TextOrigin(float left, float centerY) =>
        new(left, centerY - Typography.LineHeight(Style) * 0.5f);
}
