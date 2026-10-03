using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal static class WidgetResizeHandle
{
    private const float HitRadiusUnits = 15f;
    private const float ArcOffsetUnits = 2.5f;
    private const float ArcThicknessUnits = 4.5f;
    private const float ShadowThicknessUnits = 7f;
    private const float ArcSweep = 0.42f;
    private const float ShadowAlpha = 0.28f;
    private const float OutlineFillAlpha = 0.10f;
    private const float OutlineStrokeAlpha = 0.85f;
    private const float OutlineThicknessUnits = 2f;
    private const int ArcSegments = 12;

    public static bool Resizable(HomeTile tile)
    {
        var sizes = HomeLayoutService.SizesOf(tile);
        var count = 0;
        for (var size = WidgetSize.Small; size <= WidgetSize.Large; size++)
        {
            if (WidgetSizes.Contains(sizes, size))
            {
                count++;
            }
        }

        return count > 1;
    }

    public static Vector2 Center(Rect rect, float scale) => CurvePoint(rect, scale, MathF.PI * 0.25f);

    public static bool Hit(Rect rect, Vector2 point, float scale)
    {
        var reach = HitRadiusUnits * scale;
        return Vector2.DistanceSquared(Center(rect, scale), point) <= reach * reach;
    }

    public static bool Hovered(Rect rect, float scale)
    {
        var center = Center(rect, scale);
        var reach = new Vector2(HitRadiusUnits * scale);
        return UiInteract.Hover(center - reach, center + reach);
    }

    public static void Draw(ImDrawListPtr drawList, Rect rect, float scale, bool hovered)
    {
        TraceArc(drawList, rect, scale);
        drawList.PathStroke(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ShadowAlpha)), ImDrawFlags.None,
            ShadowThicknessUnits * scale);
        TraceArc(drawList, rect, scale);
        drawList.PathStroke(ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 1f : 0.92f)), ImDrawFlags.None,
            ArcThicknessUnits * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);
        }
    }

    private static void TraceArc(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var start = MathF.PI * (0.25f - ArcSweep * 0.5f);
        var step = MathF.PI * ArcSweep / ArcSegments;
        for (var segment = 0; segment <= ArcSegments; segment++)
        {
            drawList.PathLineTo(CurvePoint(rect, scale, start + step * segment));
        }
    }

    private static Vector2 CurvePoint(Rect rect, float scale, float angle)
    {
        var radius = WidgetChrome.Radius(scale);
        var corner = rect.Max - new Vector2(radius, radius);
        var power = 2f / Squircle.Exponent;
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        var unit = new Vector2(MathF.Pow(cosine, power), MathF.Pow(sine, power));
        return corner + unit * radius + new Vector2(cosine, sine) * (ArcOffsetUnits * scale);
    }

    public static void Outline(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float scale)
    {
        var radius = WidgetChrome.Radius(scale);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(theme.TextStrong with { W = OutlineFillAlpha }));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(theme.TextStrong with { W = OutlineStrokeAlpha }), OutlineThicknessUnits * scale);
    }
}
