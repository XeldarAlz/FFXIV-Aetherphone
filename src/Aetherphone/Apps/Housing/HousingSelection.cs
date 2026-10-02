using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Housing;

internal static class HousingSelection
{
    public const float RowHeight = 32f;
    public const float RowInset = 10f;
    public const float MarkerInset = 12f;
    public const float MarkerRadius = 3f;
    public const float TitleGap = 12f;

    public static readonly TextStyle RowStyle = TextStyles.Subheadline;
    public static readonly TextStyle TitleStyle = TextStyles.SubheadlineEmphasized;
    public static readonly TextStyle LegendStyle = TextStyles.Caption1;

    public static float Radius(float scale) => Metrics.Radius.Sm * scale;

    public static float TitleHeight(float scale) => Typography.LineHeight(TitleStyle) + TitleGap * scale;

    public static float Title(ImDrawListPtr drawList, Rect panel, string title, float top, AppSkin ui, float scale)
    {
        var size = Typography.Measure(title, TitleStyle);
        Typography.DrawCentered(drawList, new Vector2(panel.Center.X, top + size.Y * 0.5f), title, ui.TitleInk,
            TitleStyle);
        return top + size.Y + TitleGap * scale;
    }

    public static void Surface(ImDrawListPtr drawList, Rect bounds, bool selected, bool hovered, bool strong,
        AppSkin ui, float scale)
    {
        var radius = Radius(scale);
        if (selected)
        {
            var fill = strong ? Palette.WithAlpha(ui.Accent, 0.92f) : Palette.WithAlpha(ui.Accent, 0.18f);
            Squircle.Fill(drawList, bounds.Min, bounds.Max, radius, ImGui.GetColorU32(fill));
            if (!strong)
            {
                Squircle.Stroke(drawList, bounds.Min, bounds.Max, radius,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.68f)), Metrics.Stroke.Hairline * scale);
            }

            return;
        }

        if (hovered)
        {
            Squircle.Fill(drawList, bounds.Min, bounds.Max, radius, ImGui.GetColorU32(ui.HoverTint));
        }
    }

    public static void Marker(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        drawList.AddCircleFilled(center, MarkerRadius * scale, ImGui.GetColorU32(color), 14);
    }

    public static Vector4 Ink(bool selected, bool hovered, bool strong, AppSkin ui)
    {
        if (selected)
        {
            return strong ? StrongInk : ui.TitleInk;
        }

        return hovered ? ui.TitleInk : ui.BodyInk;
    }

    public static readonly Vector4 StrongInk = new(0.05f, 0.09f, 0.07f, 1f);
}
