using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class IconTile
{
    public static void Draw(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var drawList = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half),
            size * Metrics.Radius.TileFactor, ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(drawList, center, icon, AccentRing.Ink, size * 0.50f);
    }

    public static void DrawApp(ImDrawListPtr drawList, string appId, Vector2 center, float size, Vector4 surface)
    {
        var half = size * 0.5f;
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half),
            size * Metrics.Radius.TileFactor, ImGui.GetColorU32(surface));
        var ink = AccentRing.Ink;
        if (AppIconArt.TryDraw(drawList, appId, center, size * 0.98f, ink, Palette.Mix(surface, ink, 0.28f)))
        {
            return;
        }

        drawList.AddCircleFilled(center, size * 0.13f, ImGui.GetColorU32(ink), 16);
    }

    public static Vector4 Surface(Vector4 accent) =>
        Palette.ShadeToLuminance(accent with { W = 1f }, AccentRing.TileLuminance);

    public static void FillShaded(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, Vector4 surface,
        float alpha = 1f)
    {
        var top = Palette.Lighten(surface, 0.10f);
        var bottom = Palette.Darken(surface, 0.14f);
        Squircle.FillVerticalGradient(drawList, min, max, radius,
            ImGui.GetColorU32(top with { W = top.W * alpha }),
            ImGui.GetColorU32(bottom with { W = bottom.W * alpha }));
    }
}
