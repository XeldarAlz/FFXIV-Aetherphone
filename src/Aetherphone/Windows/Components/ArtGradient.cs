using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ArtGradient
{
    public const int Buckets = 24;

    internal readonly struct Swatch
    {
        public readonly Vector4 Top;
        public readonly Vector4 Bottom;
        public readonly Vector4 Glow;

        public Swatch(Vector4 top, Vector4 bottom, Vector4 glow)
        {
            Top = top;
            Bottom = bottom;
            Glow = glow;
        }
    }

    public static int Seed(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        unchecked
        {
            var hash = 2166136261u;
            for (var index = 0; index < value.Length; index++)
            {
                hash = (hash ^ value[index]) * 16777619u;
            }

            return (int)(hash % Buckets);
        }
    }

    public static Swatch From(int seed)
    {
        var hue = seed / (float)Buckets;
        var top = FromHsv(hue, 0.58f, 0.96f);
        var bottom = FromHsv((hue + 0.07f) % 1f, 0.82f, 0.58f);
        var glow = FromHsv((hue + 0.93f) % 1f, 0.42f, 1f);
        return new Swatch(top, bottom, glow);
    }

    public static Swatch FromName(string value) => From(Seed(value));

    public static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, Swatch swatch, float alpha)
    {
        var bottom = ImGui.GetColorU32(Palette.WithAlpha(swatch.Bottom, alpha));
        var top = ImGui.GetColorU32(Palette.WithAlpha(swatch.Top, alpha));
        var glow = ImGui.GetColorU32(Palette.WithAlpha(swatch.Glow, 0.55f * alpha));
        drawList.AddCircleFilled(center, radius, bottom, 48);
        drawList.AddCircleFilled(center - new Vector2(0f, radius * 0.34f), radius * 0.76f, top, 48);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.32f, radius * 0.40f), radius * 0.40f, glow, 32);
    }

    public static Vector4 FromHsv(float hue, float saturation, float value)
    {
        var sector = (hue - MathF.Floor(hue)) * 6f;
        var index = (int)sector;
        var fraction = sector - index;
        var p = value * (1f - saturation);
        var q = value * (1f - saturation * fraction);
        var t = value * (1f - saturation * (1f - fraction));
        return index switch
        {
            0 => new Vector4(value, t, p, 1f),
            1 => new Vector4(q, value, p, 1f),
            2 => new Vector4(p, value, t, 1f),
            3 => new Vector4(p, q, value, 1f),
            4 => new Vector4(t, p, value, 1f),
            _ => new Vector4(value, p, q, 1f),
        };
    }
}
