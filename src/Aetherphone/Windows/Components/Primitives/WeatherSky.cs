using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum WeatherKind
{
    Clear,
    Clouds,
    Fog,
    Rain,
    Thunder,
    Wind,
    Sand,
    Heat,
    Snow,
    Gloom,
}

internal readonly record struct SkyPalette(Vector4 Top, Vector4 Bottom, Vector4 Glow, Vector4 Ink)
{
    private static Vector4 Paper => new(1f, 1f, 1f, 1f);
    public Vector4 InkSoft => Ink with { W = Ink.W * 0.66f };
    public Vector4 InkFaint => Ink with { W = Ink.W * 0.40f };
    public Vector4 Horizon => Vector4.Lerp(Top, Bottom, 0.5f);
    public bool LightSky => Ink.X < 0.5f;
    public Vector4 SurfaceTop => LightSky
        ? Vector4.Lerp(Horizon, Paper, 0.68f) with { W = 1f }
        : Vector4.Lerp(Horizon, Paper, 0.17f) with { W = 1f };
    public Vector4 SurfaceBottom => LightSky
        ? Vector4.Lerp(Horizon, Paper, 0.46f) with { W = 1f }
        : Vector4.Lerp(Horizon, Paper, 0.05f) with { W = 1f };
    public Vector4 CardStroke => LightSky ? Ink with { W = 0.16f } : Paper with { W = 0.22f };
    public Vector4 Sheen => LightSky ? Paper with { W = 0.70f } : Paper with { W = 0.30f };
    public Vector4 Shadow => new(0.02f, 0.03f, 0.05f, LightSky ? 0.20f : 0.28f);
}

internal static class WeatherSky
{
    private const float InkLuminanceSplit = 0.64f;
    private static readonly Vector4 InkLight = new(0.98f, 0.99f, 1.00f, 1f);
    private static readonly Vector4 InkDark = new(0.12f, 0.15f, 0.21f, 1f);
    private static readonly Vector4 TwilightHorizon = new(1.00f, 0.56f, 0.32f, 1f);
    private static readonly Vector4 TwilightGlow = new(1.00f, 0.74f, 0.46f, 1f);

    public static WeatherKind Classify(string weather)
    {
        if (string.IsNullOrEmpty(weather))
        {
            return WeatherKind.Clouds;
        }

        var name = weather.ToLowerInvariant();
        if (name.Contains("thunder"))
        {
            return WeatherKind.Thunder;
        }

        if (name.Contains("blizzard") || name.Contains("snow"))
        {
            return WeatherKind.Snow;
        }

        if (name.Contains("sand") || name.Contains("dust"))
        {
            return WeatherKind.Sand;
        }

        if (name.Contains("heat") || name.Contains("hot") || name.Contains("eruption"))
        {
            return WeatherKind.Heat;
        }

        if (name.Contains("rain") || name.Contains("shower"))
        {
            return WeatherKind.Rain;
        }

        if (name.Contains("fog"))
        {
            return WeatherKind.Fog;
        }

        if (name.Contains("gale") || name.Contains("wind") || name.Contains("tempest"))
        {
            return WeatherKind.Wind;
        }

        if (name.Contains("gloom") || name.Contains("umbral"))
        {
            return WeatherKind.Gloom;
        }

        if (name.Contains("cloud"))
        {
            return WeatherKind.Clouds;
        }

        if (name.Contains("clear") || name.Contains("fair"))
        {
            return WeatherKind.Clear;
        }

        return WeatherKind.Clouds;
    }

    public static SkyPalette Resolve(WeatherKind kind, bool isDay)
    {
        var raw = Raw(kind, isDay);
        return raw with { Ink = ReadableInk(raw.Top, raw.Bottom) };
    }

    private static SkyPalette Raw(WeatherKind kind, bool isDay)
    {
        switch (kind)
        {
            case WeatherKind.Clear:
                return isDay
                    ? new SkyPalette(new(0.09f, 0.34f, 0.74f, 1f), new(0.33f, 0.61f, 0.92f, 1f),
                        new(1.00f, 0.86f, 0.44f, 1f), InkLight)
                    : new SkyPalette(new(0.03f, 0.05f, 0.16f, 1f), new(0.09f, 0.13f, 0.29f, 1f),
                        new(0.78f, 0.84f, 1.00f, 1f), InkLight);
            case WeatherKind.Clouds:
                return isDay
                    ? new SkyPalette(new(0.30f, 0.38f, 0.48f, 1f), new(0.52f, 0.58f, 0.66f, 1f),
                        new(0.96f, 0.97f, 1.00f, 1f), InkDark)
                    : new SkyPalette(new(0.09f, 0.11f, 0.16f, 1f), new(0.18f, 0.21f, 0.28f, 1f),
                        new(0.52f, 0.57f, 0.66f, 1f), InkLight);
            case WeatherKind.Fog:
                return isDay
                    ? new SkyPalette(new(0.38f, 0.41f, 0.45f, 1f), new(0.55f, 0.57f, 0.60f, 1f),
                        new(0.93f, 0.94f, 0.96f, 1f), InkDark)
                    : new SkyPalette(new(0.11f, 0.12f, 0.15f, 1f), new(0.23f, 0.25f, 0.29f, 1f),
                        new(0.46f, 0.49f, 0.55f, 1f), InkLight);
            case WeatherKind.Rain:
                return isDay
                    ? new SkyPalette(new(0.19f, 0.27f, 0.37f, 1f), new(0.37f, 0.45f, 0.55f, 1f),
                        new(0.58f, 0.74f, 0.94f, 1f), InkLight)
                    : new SkyPalette(new(0.05f, 0.08f, 0.15f, 1f), new(0.12f, 0.17f, 0.27f, 1f),
                        new(0.42f, 0.57f, 0.80f, 1f), InkLight);
            case WeatherKind.Thunder:
                return isDay
                    ? new SkyPalette(new(0.17f, 0.17f, 0.25f, 1f), new(0.33f, 0.31f, 0.41f, 1f),
                        new(1.00f, 0.86f, 0.45f, 1f), InkLight)
                    : new SkyPalette(new(0.04f, 0.04f, 0.10f, 1f), new(0.13f, 0.11f, 0.21f, 1f),
                        new(1.00f, 0.88f, 0.50f, 1f), InkLight);
            case WeatherKind.Wind:
                return isDay
                    ? new SkyPalette(new(0.22f, 0.42f, 0.46f, 1f), new(0.44f, 0.62f, 0.64f, 1f),
                        new(0.88f, 0.98f, 0.96f, 1f), InkDark)
                    : new SkyPalette(new(0.06f, 0.12f, 0.15f, 1f), new(0.15f, 0.25f, 0.28f, 1f),
                        new(0.56f, 0.76f, 0.74f, 1f), InkLight);
            case WeatherKind.Sand:
                return isDay
                    ? new SkyPalette(new(0.48f, 0.36f, 0.20f, 1f), new(0.74f, 0.58f, 0.36f, 1f),
                        new(1.00f, 0.85f, 0.53f, 1f), InkDark)
                    : new SkyPalette(new(0.16f, 0.12f, 0.08f, 1f), new(0.31f, 0.23f, 0.15f, 1f),
                        new(0.80f, 0.64f, 0.42f, 1f), InkLight);
            case WeatherKind.Heat:
                return isDay
                    ? new SkyPalette(new(0.64f, 0.31f, 0.17f, 1f), new(0.93f, 0.57f, 0.29f, 1f),
                        new(1.00f, 0.81f, 0.41f, 1f), InkLight)
                    : new SkyPalette(new(0.20f, 0.09f, 0.07f, 1f), new(0.37f, 0.17f, 0.13f, 1f),
                        new(1.00f, 0.62f, 0.36f, 1f), InkLight);
            case WeatherKind.Snow:
                return isDay
                    ? new SkyPalette(new(0.40f, 0.50f, 0.64f, 1f), new(0.64f, 0.71f, 0.80f, 1f),
                        new(1.00f, 1.00f, 1.00f, 1f), InkDark)
                    : new SkyPalette(new(0.13f, 0.17f, 0.26f, 1f), new(0.27f, 0.33f, 0.44f, 1f),
                        new(0.82f, 0.88f, 0.97f, 1f), InkLight);
            default:
                return new SkyPalette(new(0.08f, 0.07f, 0.12f, 1f), new(0.19f, 0.16f, 0.24f, 1f),
                    new(0.52f, 0.44f, 0.62f, 1f), InkLight);
        }
    }

    public static float Daylight(float bell)
    {
        var sunrise = SmoothStep(5.25f, 6.75f, bell);
        var sunset = SmoothStep(18.25f, 19.75f, bell);
        return sunrise * (1f - sunset);
    }

    public static SkyPalette Blend(WeatherKind kind, float daylight)
    {
        if (daylight <= 0f)
        {
            return Resolve(kind, false);
        }

        if (daylight >= 1f)
        {
            return Resolve(kind, true);
        }

        var night = Resolve(kind, false);
        var day = Resolve(kind, true);
        var twilight = daylight * (1f - daylight) * 4f * TwilightStrength(kind);
        var top = Vector4.Lerp(night.Top, day.Top, daylight);
        var bottom = Vector4.Lerp(Vector4.Lerp(night.Bottom, day.Bottom, daylight), TwilightHorizon, twilight);
        var glow = Vector4.Lerp(Vector4.Lerp(night.Glow, day.Glow, daylight), TwilightGlow, twilight * 0.6f);
        return new SkyPalette(top, bottom, glow, ReadableInk(top, bottom));
    }

    public static SkyPalette Mix(in SkyPalette from, in SkyPalette to, float amount)
    {
        if (amount >= 1f)
        {
            return to;
        }

        var top = Vector4.Lerp(from.Top, to.Top, amount);
        var bottom = Vector4.Lerp(from.Bottom, to.Bottom, amount);
        return new SkyPalette(top, bottom, Vector4.Lerp(from.Glow, to.Glow, amount), ReadableInk(top, bottom));
    }

    private static Vector4 ReadableInk(Vector4 top, Vector4 bottom)
    {
        var background = Vector4.Lerp(top, bottom, 0.5f);
        return Palette.Luminance(background) > InkLuminanceSplit ? InkDark : InkLight;
    }

    private static float TwilightStrength(WeatherKind kind) => kind switch
    {
        WeatherKind.Clear => 0.45f,
        WeatherKind.Clouds or WeatherKind.Wind or WeatherKind.Snow => 0.24f,
        WeatherKind.Fog or WeatherKind.Rain => 0.12f,
        _ => 0f,
    };

    private static float SmoothStep(float edgeStart, float edgeEnd, float value)
    {
        var fraction = Math.Clamp((value - edgeStart) / (edgeEnd - edgeStart), 0f, 1f);
        return fraction * fraction * (3f - 2f * fraction);
    }

    public static void Paint(ImDrawListPtr drawList, Rect screen, float rounding, in SkyPalette palette) =>
        Squircle.FillVerticalGradient(drawList, screen.Min, screen.Max, rounding, ImGui.GetColorU32(palette.Top),
            ImGui.GetColorU32(palette.Bottom));
}
