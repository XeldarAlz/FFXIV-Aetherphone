using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal static class GameIconArt
{
    private const float ArtFraction = 0.62f;
    private const float HoleMix = 0.28f;

    public static float Radius(float side) => side * HubMetrics.IconRadiusFactor;

    public static bool Draw(ImDrawListPtr drawList, string iconId, Vector4 accent, Vector2 min, Vector2 max,
        IconAppearance? appearance, bool elevated)
    {
        var scale = UiScale.Current;
        var side = MathF.Max(max.X - min.X, max.Y - min.Y);
        var radius = Radius(side);
        var painted = appearance.HasValue
            ? AppIconTile.TryDraw(drawList, iconId, accent, min, max, radius, 1f, appearance.Value, elevated, scale)
            : AppIconTile.TryDraw(drawList, iconId, accent, min, max, radius, 1f, elevated, scale);
        if (painted)
        {
            return true;
        }

        var surface = IconTile.Surface(accent);
        if (elevated)
        {
            Elevation.IconRest(drawList, min, max, radius, scale);
        }

        IconTile.FillShaded(drawList, min, max, radius, surface);
        if (elevated)
        {
            Material.EdgeSquircle(drawList, min, max, radius, scale);
        }

        var ink = AppAccents.InkFor(iconId);
        return AppIconArt.TryDraw(drawList, iconId, (min + max) * 0.5f, side * ArtFraction, ink,
            Palette.Mix(surface, ink, HoleMix));
    }
}
