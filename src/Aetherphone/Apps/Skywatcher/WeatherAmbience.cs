using Aetherphone.Core;
using Aetherphone.Apps.Skywatcher.Sky;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher;

internal static class WeatherAmbience
{
    private const float MonoStrength = 0.75f;

    public static void Draw(ImDrawListPtr drawList, in Rect bounds, float rounding, WeatherKind kind, bool isDay,
        in SkyPalette palette, float scale, float opacity) =>
        Draw(drawList, bounds, rounding, kind, isDay ? 1f : 0f, palette, scale, opacity);

    public static void Draw(ImDrawListPtr drawList, in Rect bounds, float rounding, WeatherKind kind, float daylight,
        in SkyPalette palette, float scale, float opacity)
    {
        if (opacity <= 0.02f)
        {
            return;
        }

        var canvas = new SkyCanvas(drawList, bounds, rounding, scale, opacity);
        var ink = AmbienceInk.Natural(palette, daylight);
        SkyScene.Draw(canvas, kind, daylight, SeasonalTheme.Halloween ? ink.Haunted() : ink, true);
    }

    public static void DrawMono(ImDrawListPtr drawList, in Rect bounds, float rounding, WeatherKind kind, bool isDay,
        Vector4 ink, float scale, float opacity)
    {
        if (opacity <= 0.02f)
        {
            return;
        }

        var canvas = new SkyCanvas(drawList, bounds, rounding, scale, opacity * MonoStrength);
        SkyScene.Draw(canvas, kind, isDay ? 1f : 0f, AmbienceInk.Mono(ink), false);
    }

    public static void Glow(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float alpha) =>
        SkyLayers.Glow(drawList, center, new Vector2(radius, radius), color, alpha);
}
