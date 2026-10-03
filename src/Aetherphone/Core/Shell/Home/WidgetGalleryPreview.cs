using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal static class WidgetGalleryPreview
{
    private const float TileTopUnits = 3f;
    private const float StackInsetUnits = 9f;
    private const float StackLiftUnits = 6f;
    private const float StackPlateAlpha = 0.22f;
    private const int StackPlates = 2;
    private const float StackDotRadiusUnits = 2.2f;
    private const float StackDotGapUnits = 7f;
    private const float StackDotInsetUnits = 7f;
    private const float StackDotActiveAlpha = 0.85f;
    private const float StackDotIdleAlpha = 0.32f;
    private static readonly Vector4 StackDotInk = new(0.55f, 0.55f, 0.58f, 1f);

    public static Vector2 Footprint(WidgetSize size, in HomeMetrics metrics)
    {
        var width = WidgetSizes.ColumnSpan(size) * metrics.CellWidth - (metrics.CellWidth - metrics.IconSize);
        var height = WidgetSizes.RowSpan(size) * metrics.CellHeight -
                     (HomeMetrics.LabelBandUnits - TileTopUnits) * metrics.Scale;
        return new Vector2(MathF.Max(width, 1f), MathF.Max(height, 1f));
    }

    public static float Fit(Vector2 footprint, Vector2 room) =>
        Math.Clamp(MathF.Min(room.X / footprint.X, room.Y / footprint.Y), 0.05f, 1f);

    public static Rect Centered(Vector2 center, Vector2 size) => new(center - size * 0.5f, center + size * 0.5f);

    public static void Draw(ImDrawListPtr drawList, WidgetHost host, IHomeWidget widget, WidgetSize size, Rect rect,
        PhoneTheme theme, float scale, float delta)
    {
        Elevation.Floating(drawList, rect.Min, rect.Max, WidgetChrome.Radius(scale), scale);
        widget.Draw(host.Preview(drawList, rect, theme, widget.Id, size, scale, delta));
    }

    public static void DrawStack(ImDrawListPtr drawList, WidgetHost host, IReadOnlyList<IHomeWidget> members,
        WidgetSize size, Rect rect, PhoneTheme theme, Vector4 ink, float scale, float delta)
    {
        if (members.Count == 0)
        {
            return;
        }

        var radius = WidgetChrome.Radius(scale);
        var plates = Math.Min(members.Count - 1, StackPlates);
        for (var layer = plates; layer >= 1; layer--)
        {
            var inset = StackInsetUnits * layer * scale;
            var lift = StackLiftUnits * layer * scale;
            Squircle.Fill(drawList, new Vector2(rect.Min.X + inset, rect.Min.Y - lift),
                new Vector2(rect.Max.X - inset, rect.Max.Y - lift), radius,
                ImGui.GetColorU32(Palette.WithAlpha(ink, StackPlateAlpha / layer)));
        }

        Draw(drawList, host, members[0], size, rect, theme, scale, delta);
        var gap = StackDotGapUnits * scale;
        var dotX = rect.Max.X - StackDotInsetUnits * scale;
        var firstY = rect.Center.Y - (members.Count - 1) * gap * 0.5f;
        for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
        {
            var alpha = memberIndex == 0 ? StackDotActiveAlpha : StackDotIdleAlpha;
            drawList.AddCircleFilled(new Vector2(dotX, firstY + memberIndex * gap), StackDotRadiusUnits * scale,
                ImGui.GetColorU32(Palette.WithAlpha(StackDotInk, alpha)), 12);
        }
    }
}
