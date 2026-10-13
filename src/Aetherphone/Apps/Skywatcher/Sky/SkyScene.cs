using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Skywatcher.Sky;

internal static class SkyScene
{
    private static readonly Vector2 SunAnchor = new(0.80f, 0.075f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 NebulaTint = new(0.66f, 0.72f, 1.00f, 1f);

    public static void Draw(in SkyCanvas canvas, WeatherKind kind, float daylight, in AmbienceInk ink, bool natural)
    {
        var drawList = canvas.DrawList;
        drawList.PushClipRect(canvas.Bounds.Min, canvas.Bounds.Max, true);
        switch (kind)
        {
            case WeatherKind.Clear:
                Clear(canvas, daylight, ink);
                break;
            case WeatherKind.Clouds:
                Clouds(canvas, daylight, ink);
                break;
            case WeatherKind.Fog:
                Fog(canvas, daylight, ink);
                break;
            case WeatherKind.Rain:
                Rain(canvas, daylight, ink, false);
                break;
            case WeatherKind.Thunder:
                Rain(canvas, daylight, ink, true);
                break;
            case WeatherKind.Wind:
                Wind(canvas, daylight, ink);
                break;
            case WeatherKind.Sand:
                Sand(canvas, daylight, ink);
                break;
            case WeatherKind.Heat:
                Heat(canvas, daylight, ink);
                break;
            case WeatherKind.Snow:
                Snow(canvas, daylight, ink);
                break;
            default:
                Gloom(canvas, ink);
                break;
        }

        if (SeasonalTheme.Halloween)
        {
            SkyHaunt.Draw(canvas, kind, daylight, ink, natural);
        }

        if (natural)
        {
            SkyLayers.Wash(canvas, 0.62f, 1f, Black, 0f, 0.12f);
        }

        drawList.PopClipRect();
    }

    private static void Clear(in SkyCanvas canvas, float daylight, in AmbienceInk ink)
    {
        var night = 1f - daylight;
        if (night > 0.01f)
        {
            SkyLayers.Textured(canvas, SkyTexture.Mist, 0f, 0.75f, new Vector2(1.7f, 0.9f),
                new Vector2(0.0012f, 0f), new Vector2(0.21f, 0.64f), NebulaTint, 0.10f * night, 0f, 0.55f);
            SkyParticles.Stars(canvas, ink.Star, ink.Glow, 64, night);
            SkyParticles.ShootingStar(canvas, ink.Star, night);
        }

        SkyParticles.Luminary(canvas, SunAnchor, daylight, ink, 1f, SeasonalTheme.Halloween);
        if (daylight > 0.01f)
        {
            SkyParticles.SunRays(canvas, SunAnchor, ink.Glow, daylight);
            SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.48f, new Vector2(1.9f, 0.55f),
                new Vector2(0.0035f, 0f), new Vector2(0.62f, 0.18f), ink.Cloud, 0.20f * daylight, 0f);
        }
    }

    private static void Clouds(in SkyCanvas canvas, float daylight, in AmbienceInk ink)
    {
        var breath = 0.92f + 0.08f * SkyLayers.Wave(21.0);
        SkyLayers.Glow(canvas, canvas.At(0.76f, 0.10f), new Vector2(canvas.Width * 0.85f), ink.Glow,
            (0.10f + 0.12f * daylight) * canvas.Opacity);
        SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.78f, new Vector2(1.6f, 1.05f),
            new Vector2(0.0040f, 0f), new Vector2(0.13f, 0.47f), ink.Shade, 0.70f * breath, 0f);
        SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.86f, new Vector2(1.2f, 0.82f),
            new Vector2(0.0068f, 0f), new Vector2(0.52f, 0.29f), ink.Cloud, 0.62f, 0f);
        SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.44f, new Vector2(0.92f, 0.62f),
            new Vector2(0.0110f, 0f), new Vector2(0.84f, 0.71f), ink.Cloud, 0.48f * breath, 0f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.62f, 1f, new Vector2(1.7f, 0.7f),
            new Vector2(0.0060f, 0f), new Vector2(0.33f, 0.12f), ink.Fog, 0f, 0.16f);
    }

    private static void Fog(in SkyCanvas canvas, float daylight, in AmbienceInk ink)
    {
        var breath = 0.88f + 0.12f * SkyLayers.Wave(17.0);
        var swell = 0.90f + 0.10f * SkyLayers.Wave(23.0, 0.4f);
        SkyLayers.Wash(canvas, 0f, 1f, ink.Fog, 0.06f, 0.14f);
        var light = canvas.At(0.70f, 0.15f);
        SkyLayers.Glow(canvas, light, new Vector2(canvas.Width * 0.80f), ink.Glow,
            (0.10f + 0.12f * daylight) * canvas.Opacity);
        SkyLayers.Glow(canvas, light, new Vector2(canvas.Width * 0.11f), ink.Core,
            (0.16f + 0.16f * daylight) * breath * canvas.Opacity);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0f, 1f, new Vector2(2.3f, 1.15f),
            new Vector2(0.0045f, 0f), new Vector2(0.11f, 0.42f), ink.Fog, 0.20f * breath, 0.34f * breath);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.16f, 1f, new Vector2(1.55f, 0.72f),
            new Vector2(0.0085f, 0.0006f), new Vector2(0.37f, 0.61f), ink.Fog, 0f, 0.46f * swell);
        SkyLayers.Wash(canvas, 0.52f, 1f, ink.Fog, 0f, 0.30f * swell);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.42f, 1f, new Vector2(1.10f, 0.48f),
            new Vector2(0.0160f, 0f), new Vector2(0.71f, 0.13f), ink.Fog, 0f, 0.46f * breath);
    }

    private static void Rain(in SkyCanvas canvas, float daylight, in AmbienceInk ink, bool storm)
    {
        var flash = storm ? SkyParticles.Flash() : 0f;
        var ceilingAlpha = storm ? 0.82f : 0.70f;
        SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.60f, new Vector2(1.35f, 0.92f),
            new Vector2(0.0100f, 0f), new Vector2(0.27f, 0.55f), ink.Ceiling, ceilingAlpha, 0f);
        SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.46f, new Vector2(1.0f, 0.70f),
            new Vector2(0.0170f, 0f), new Vector2(0.64f, 0.08f), ink.Shade, 0.50f, 0f);
        if (flash > 0.02f)
        {
            SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.60f, new Vector2(1.35f, 0.92f),
                new Vector2(0.0100f, 0f), new Vector2(0.27f, 0.55f), ink.Flash, 0.75f * flash, 0f);
            SkyParticles.Bolt(canvas, ink.Flash, flash);
        }

        var shower = storm ? 1.15f : 1f;
        SkyLayers.Textured(canvas, SkyTexture.Rain, 0f, 1f, new Vector2(0.50f, 1.00f),
            new Vector2(0f, -0.95f), new Vector2(0.17f, 0f), ink.Rain, 0.16f * shower, 0.24f * shower, 0.14f);
        SkyLayers.Textured(canvas, SkyTexture.Rain, 0f, 1f, new Vector2(0.95f, 1.90f),
            new Vector2(0f, -1.05f), new Vector2(0.58f, 0.31f), ink.Rain, 0.26f * shower, 0.36f * shower, 0.16f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.58f, 1f, new Vector2(1.4f, 0.55f),
            new Vector2(0.0220f, 0f), new Vector2(0.44f, 0.82f), ink.Fog, 0f, 0.20f);
        if (flash > 0.02f)
        {
            SkyLayers.Wash(canvas, 0f, 1f, ink.Flash, 0.16f * flash, 0.06f * flash);
        }

        SkyParticles.Splashes(canvas, ink.Rain, storm ? 9 : 7);
    }

    private static void Snow(in SkyCanvas canvas, float daylight, in AmbienceInk ink)
    {
        SkyLayers.Glow(canvas, canvas.At(0.72f, 0.10f), new Vector2(canvas.Width * 0.75f), ink.Glow,
            (0.08f + 0.10f * daylight) * canvas.Opacity);
        SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.50f, new Vector2(1.4f, 0.9f),
            new Vector2(0.0030f, 0f), new Vector2(0.36f, 0.19f), ink.Cloud, 0.36f, 0f);
        SkyParticles.Snowfall(canvas, ink.Cloud);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.60f, 1f, new Vector2(1.6f, 0.6f),
            new Vector2(0.0070f, 0f), new Vector2(0.58f, 0.37f), ink.Cloud, 0f, 0.28f);
    }

    private static void Wind(in SkyCanvas canvas, float daylight, in AmbienceInk ink)
    {
        SkyLayers.Glow(canvas, canvas.At(0.80f, 0.08f), new Vector2(canvas.Width * 0.70f), ink.Glow,
            (0.06f + 0.12f * daylight) * canvas.Opacity);
        SkyLayers.Textured(canvas, SkyTexture.Billow, 0f, 0.55f, new Vector2(2.4f, 0.70f),
            new Vector2(0.0240f, 0f), new Vector2(0.12f, 0.44f), ink.Cloud, 0.30f, 0f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0f, 1f, new Vector2(3.4f, 0.42f),
            new Vector2(0.0520f, 0f), new Vector2(0.33f, 0.71f), ink.Cloud, 0.10f, 0.16f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.2f, 1f, new Vector2(2.4f, 0.30f),
            new Vector2(0.0900f, 0f), new Vector2(0.77f, 0.27f), ink.Glow, 0f, 0.14f);
        SkyParticles.Gusts(canvas, ink.Cloud);
    }

    private static void Sand(in SkyCanvas canvas, float daylight, in AmbienceInk ink)
    {
        SkyLayers.Glow(canvas, canvas.At(0.74f, 0.12f), new Vector2(canvas.Width * 0.62f), ink.Core,
            0.20f * daylight * canvas.Opacity);
        SkyLayers.Wash(canvas, 0f, 1f, ink.Glow, 0.08f, 0.26f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0f, 1f, new Vector2(2.0f, 0.8f),
            new Vector2(0.0340f, 0.0020f), new Vector2(0.18f, 0.52f), ink.Glow, 0.20f, 0.36f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.25f, 1f, new Vector2(1.2f, 0.5f),
            new Vector2(0.0620f, 0f), new Vector2(0.66f, 0.09f), ink.Glow, 0f, 0.34f);
        SkyParticles.Dust(canvas, ink.Glow);
    }

    private static void Heat(in SkyCanvas canvas, float daylight, in AmbienceInk ink)
    {
        SkyLayers.Glow(canvas, canvas.At(0.55f, -0.02f), new Vector2(canvas.Width * 1.15f), ink.Glow,
            (0.18f + 0.14f * daylight) * canvas.Opacity);
        SkyLayers.Glow(canvas, canvas.At(0.55f, 0.02f), new Vector2(canvas.Width * 0.16f), ink.Core,
            0.55f * daylight * canvas.Opacity);
        SkyLayers.Wash(canvas, 0.55f, 1f, ink.Ember, 0f, 0.22f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0.35f, 1f, new Vector2(1.3f, 0.36f),
            new Vector2(0.0040f, 0.0300f), new Vector2(0.29f, 0.58f), ink.Glow, 0f, 0.20f);
        SkyParticles.Embers(canvas, ink.Ember);
    }

    private static void Gloom(in SkyCanvas canvas, in AmbienceInk ink)
    {
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0f, 1f, new Vector2(1.7f, 0.95f),
            new Vector2(0.0060f, 0.0020f), new Vector2(0.41f, 0.23f), ink.Glow, 0.16f, 0.30f);
        SkyLayers.Textured(canvas, SkyTexture.Mist, 0f, 1f, new Vector2(1.2f, 0.70f),
            new Vector2(-0.0045f, 0.0010f), new Vector2(0.83f, 0.66f), ink.Glow, 0.08f, 0.22f);
        SkyLayers.Wash(canvas, 0f, 0.35f, Black, 0.20f, 0f);
        SkyParticles.Motes(canvas, ink.Glow);
    }
}
