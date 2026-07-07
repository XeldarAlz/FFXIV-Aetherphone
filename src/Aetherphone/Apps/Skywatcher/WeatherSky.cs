using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Skywatcher;

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
    public Vector4 InkSoft => Ink with { W = Ink.W * 0.66f };
    public Vector4 InkFaint => Ink with { W = Ink.W * 0.40f };
    public Vector4 Horizon => Vector4.Lerp(Top, Bottom, 0.5f);
}

internal static class WeatherSky
{
    private static readonly Vector4 InkLight = new(0.98f, 0.99f, 1.00f, 1f);
    private static readonly Vector4 InkDark = new(0.12f, 0.15f, 0.21f, 1f);

    private static readonly float[] StarX =
    {
        0.12f, 0.26f, 0.38f, 0.07f, 0.55f, 0.71f, 0.83f, 0.94f, 0.18f, 0.46f, 0.63f, 0.88f, 0.33f, 0.78f,
    };

    private static readonly float[] StarY =
    {
        0.10f, 0.22f, 0.07f, 0.31f, 0.14f, 0.06f, 0.19f, 0.11f, 0.40f, 0.35f, 0.27f, 0.33f, 0.44f, 0.42f,
    };

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
                    ? new SkyPalette(new(0.40f, 0.50f, 0.60f, 1f), new(0.66f, 0.72f, 0.78f, 1f),
                        new(0.96f, 0.97f, 1.00f, 1f), InkDark)
                    : new SkyPalette(new(0.09f, 0.11f, 0.16f, 1f), new(0.18f, 0.21f, 0.28f, 1f),
                        new(0.52f, 0.57f, 0.66f, 1f), InkLight);
            case WeatherKind.Fog:
                return isDay
                    ? new SkyPalette(new(0.52f, 0.55f, 0.58f, 1f), new(0.73f, 0.75f, 0.77f, 1f),
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
                    ? new SkyPalette(new(0.26f, 0.46f, 0.48f, 1f), new(0.51f, 0.69f, 0.69f, 1f),
                        new(0.88f, 0.98f, 0.96f, 1f), InkDark)
                    : new SkyPalette(new(0.06f, 0.12f, 0.15f, 1f), new(0.15f, 0.25f, 0.28f, 1f),
                        new(0.56f, 0.76f, 0.74f, 1f), InkLight);
            case WeatherKind.Sand:
                return isDay
                    ? new SkyPalette(new(0.52f, 0.40f, 0.22f, 1f), new(0.81f, 0.65f, 0.41f, 1f),
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
                    ? new SkyPalette(new(0.55f, 0.65f, 0.78f, 1f), new(0.83f, 0.88f, 0.94f, 1f),
                        new(1.00f, 1.00f, 1.00f, 1f), InkDark)
                    : new SkyPalette(new(0.13f, 0.17f, 0.26f, 1f), new(0.27f, 0.33f, 0.44f, 1f),
                        new(0.82f, 0.88f, 0.97f, 1f), InkLight);
            default:
                return new SkyPalette(new(0.08f, 0.07f, 0.12f, 1f), new(0.19f, 0.16f, 0.24f, 1f),
                    new(0.52f, 0.44f, 0.62f, 1f), InkLight);
        }
    }

    public static void Paint(Rect screen, float rounding, in SkyPalette palette, WeatherKind kind, bool isDay)
    {
        var drawList = ImGui.GetWindowDrawList();
        var topColor = ImGui.GetColorU32(palette.Top);
        var bottomColor = ImGui.GetColorU32(palette.Bottom);
        drawList.AddRectFilled(screen.Min, screen.Max, topColor, rounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(new Vector2(screen.Min.X, screen.Max.Y - rounding * 2f), screen.Max, bottomColor,
            rounding, ImDrawFlags.RoundCornersBottom);
        var height = screen.Height;
        var bandTop = screen.Min.Y + rounding;
        var bandBottom = screen.Max.Y - rounding;
        var step = MathF.Max(2f, 3f * ImGuiHelpers.GlobalScale);
        for (var y = bandTop; y < bandBottom; y += step)
        {
            var fraction = (y - screen.Min.Y) / height;
            var color = ImGui.GetColorU32(Vector4.Lerp(palette.Top, palette.Bottom, fraction));
            drawList.AddRectFilled(new Vector2(screen.Min.X, y),
                new Vector2(screen.Max.X, MathF.Min(y + step + 1f, bandBottom)), color);
        }

        if (!isDay && (kind == WeatherKind.Clear || kind == WeatherKind.Gloom))
        {
            DrawStars(drawList, screen, palette.Glow);
        }
    }

    private static void DrawStars(ImDrawListPtr drawList, Rect screen, Vector4 glow)
    {
        var scale = ImGuiHelpers.GlobalScale;
        for (var index = 0; index < StarX.Length; index++)
        {
            var position = new Vector2(screen.Min.X + StarX[index] * screen.Width,
                screen.Min.Y + StarY[index] * screen.Height);
            var twinkle = 0.35f + 0.45f * Pulse.Wave(2200.0 + index * 240.0);
            var radius = (0.7f + (index % 3) * 0.35f) * scale;
            drawList.AddCircleFilled(position, radius, ImGui.GetColorU32(glow with { W = twinkle }), 8);
        }
    }
}
