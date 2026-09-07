using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AppBadge
{
    private const float Radius = 9f;
    private const float DotRadius = 5.5f;
    private const float BorderThickness = 1.5f;
    private const float TextScale = 0.7f;
    private const float TextPadding = 4f;
    private const int CapSegments = 12;
    private const int ShadowLayerCount = 4;
    private const float ShadowSpread = 1.4f;
    private const float ShadowStrength = 0.14f;
    private const float ShadowDrop = 1.2f;
    private const string OverflowLabel = "99+";
    private static readonly Vector4 ShadowColor = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 BorderColor = new(1f, 1f, 1f, 0.9f);
    private static readonly string[] CountLabels = BuildCountLabels();

    public static void Draw(Vector2 center, int count, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var zoom = scale / UiScale.Current;
        var radius = Radius * scale;
        var label = Label(count);
        var textScale = TextScale * zoom;
        var textWidth = Typography.MeasureExact(label, textScale, FontWeight.SemiBold).X;
        var stretch = MathF.Max(0f, textWidth * 0.5f + TextPadding * scale - radius);
        var leftCenter = new Vector2(center.X - stretch, center.Y);
        var rightCenter = new Vector2(center.X + stretch, center.Y);
        DrawCapsule(drawList, leftCenter, rightCenter, radius, theme, scale);
        Typography.DrawCenteredExact(drawList, center, label, theme.TextStrong, textScale, FontWeight.SemiBold);
    }

    public static void DrawDot(Vector2 center, PhoneTheme theme, float scale) =>
        DrawCapsule(ImGui.GetWindowDrawList(), center, center, DotRadius * scale, theme, scale);

    private static string Label(int count) =>
        count >= 0 && count < CountLabels.Length ? CountLabels[count] : OverflowLabel;

    private static string[] BuildCountLabels()
    {
        var labels = new string[100];
        for (var count = 0; count < labels.Length; count++)
        {
            labels[count] = count.ToString();
        }

        return labels;
    }

    private static void DrawCapsule(ImDrawListPtr drawList, Vector2 leftCenter, Vector2 rightCenter, float radius,
        PhoneTheme theme, float scale)
    {
        DrawShadow(drawList, leftCenter, rightCenter, radius, scale);
        TraceCapsule(drawList, leftCenter, rightCenter, radius);
        drawList.PathFillConvex(ImGui.GetColorU32(theme.Danger));
        TraceCapsule(drawList, leftCenter, rightCenter, radius);
        drawList.PathStroke(ImGui.GetColorU32(BorderColor), ImDrawFlags.Closed, BorderThickness * scale);
    }

    private static void DrawShadow(ImDrawListPtr drawList, Vector2 leftCenter, Vector2 rightCenter, float radius,
        float scale)
    {
        var drop = new Vector2(0f, ShadowDrop * scale);
        for (var layer = ShadowLayerCount; layer >= 1; layer--)
        {
            var spread = radius + layer * ShadowSpread * scale;
            var alpha = ShadowStrength * (ShadowLayerCount - layer + 1) / ShadowLayerCount;
            TraceCapsule(drawList, leftCenter + drop, rightCenter + drop, spread);
            drawList.PathFillConvex(ImGui.GetColorU32(Palette.WithAlpha(ShadowColor, alpha)));
        }
    }

    private static void TraceCapsule(ImDrawListPtr drawList, Vector2 leftCenter, Vector2 rightCenter, float radius)
    {
        drawList.PathArcTo(rightCenter, radius, -MathF.PI * 0.5f, MathF.PI * 0.5f, CapSegments);
        drawList.PathArcTo(leftCenter, radius, MathF.PI * 0.5f, MathF.PI * 1.5f, CapSegments);
    }
}
