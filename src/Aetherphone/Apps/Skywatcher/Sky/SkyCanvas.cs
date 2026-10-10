using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher.Sky;

internal readonly ref struct SkyCanvas
{
    public readonly ImDrawListPtr DrawList;
    public readonly Rect Bounds;
    public readonly float Rounding;
    public readonly float Scale;
    public readonly float Opacity;

    public SkyCanvas(ImDrawListPtr drawList, Rect bounds, float rounding, float scale, float opacity)
    {
        DrawList = drawList;
        Bounds = bounds;
        Rounding = rounding;
        Scale = scale;
        Opacity = opacity;
    }

    public float Width => Bounds.Width;

    public float Height => Bounds.Height;

    public Vector2 At(float fractionX, float fractionY) =>
        new(Bounds.Min.X + fractionX * Bounds.Width, Bounds.Min.Y + fractionY * Bounds.Height);

    public float Y(float fraction) => Bounds.Min.Y + fraction * Bounds.Height;
}

internal readonly struct AmbienceInk
{
    private static readonly Vector4 DayCloud = new(0.97f, 0.98f, 1.00f, 1f);
    private static readonly Vector4 NightCloud = new(0.56f, 0.62f, 0.74f, 1f);
    private static readonly Vector4 DayFog = new(0.94f, 0.95f, 0.97f, 1f);
    private static readonly Vector4 NightFog = new(0.60f, 0.65f, 0.75f, 1f);
    private static readonly Vector4 RainTint = new(0.74f, 0.84f, 1.00f, 1f);
    private static readonly Vector4 EmberTint = new(1.00f, 0.66f, 0.36f, 1f);
    private static readonly Vector4 StarTint = new(0.95f, 0.97f, 1.00f, 1f);
    private static readonly Vector4 FlashTint = new(1.00f, 0.98f, 0.92f, 1f);
    private static readonly Vector4 SunCore = new(1.00f, 0.98f, 0.90f, 1f);
    private static readonly Vector4 MoonCore = new(0.90f, 0.93f, 1.00f, 1f);
    private static readonly Vector4 PumpkinEmber = new(1.00f, 0.50f, 0.12f, 1f);
    private static readonly Vector4 WitchFlash = new(0.86f, 0.80f, 1.00f, 1f);

    public readonly Vector4 Cloud;
    public readonly Vector4 Shade;
    public readonly Vector4 Fog;
    public readonly Vector4 Rain;
    public readonly Vector4 Glow;
    public readonly Vector4 Ember;
    public readonly Vector4 Star;
    public readonly Vector4 Flash;
    public readonly Vector4 Core;
    public readonly Vector4 Ceiling;
    public readonly Vector4 Sky;

    private AmbienceInk(Vector4 cloud, Vector4 shade, Vector4 fog, Vector4 rain, Vector4 glow, Vector4 ember,
        Vector4 star, Vector4 flash, Vector4 core, Vector4 ceiling, Vector4 sky)
    {
        Cloud = cloud;
        Shade = shade;
        Fog = fog;
        Rain = rain;
        Glow = glow;
        Ember = ember;
        Star = star;
        Flash = flash;
        Core = core;
        Ceiling = ceiling;
        Sky = sky;
    }

    public static AmbienceInk Natural(in SkyPalette palette, float daylight)
    {
        var cloud = Vector4.Lerp(NightCloud, DayCloud, daylight);
        var shade = Vector4.Lerp(palette.Top, cloud, 0.55f) with { W = 1f };
        var ceiling = Vector4.Lerp(palette.Top, new Vector4(0.50f, 0.55f, 0.63f, 1f), 0.45f + 0.15f * daylight)
            with { W = 1f };
        return new AmbienceInk(cloud, shade, Vector4.Lerp(NightFog, DayFog, daylight), RainTint,
            palette.Glow with { W = 1f }, EmberTint, StarTint, FlashTint,
            Vector4.Lerp(MoonCore, SunCore, daylight), ceiling,
            Vector4.Lerp(palette.Top, palette.Bottom, 0.08f) with { W = 1f });
    }

    public AmbienceInk Haunted() =>
        new(Cloud, Shade, Fog, Rain, Glow, PumpkinEmber, Star, WitchFlash, Core, Ceiling, Sky);

    public static AmbienceInk Mono(Vector4 ink)
    {
        var solid = ink with { W = 1f };
        return new AmbienceInk(solid, solid, solid, solid, solid, solid, solid, solid, solid, solid,
            solid with { W = 0f });
    }
}
