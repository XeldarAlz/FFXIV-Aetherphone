using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal static class VenueChips
{
    private const float SmallTextScale = 0.72f;
    private const float LargeTextScale = 0.85f;
    private static readonly Vector4 AdultColor = new(0.90f, 0.26f, 0.44f, 1f);
    private static readonly Vector4 SfwColor = new(0.86f, 0.72f, 0.24f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private static readonly Vector4[] TagColors =
    {
        new(0.91f, 0.49f, 0.22f, 1f), new(0.27f, 0.71f, 0.62f, 1f), new(0.36f, 0.55f, 0.92f, 1f),
        new(0.64f, 0.45f, 0.90f, 1f), new(0.32f, 0.74f, 0.42f, 1f), new(0.90f, 0.42f, 0.62f, 1f),
        new(0.86f, 0.62f, 0.24f, 1f), new(0.40f, 0.68f, 0.84f, 1f), new(0.78f, 0.40f, 0.42f, 1f),
        new(0.50f, 0.62f, 0.30f, 1f), new(0.55f, 0.50f, 0.86f, 1f), new(0.30f, 0.66f, 0.70f, 1f),
    };

    public static Vector4 Color(string tag)
    {
        if (string.Equals(tag, "18+", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("NSFW", StringComparison.OrdinalIgnoreCase))
        {
            return AdultColor;
        }

        if (string.Equals(tag, "SFW", StringComparison.OrdinalIgnoreCase))
        {
            return SfwColor;
        }

        var hash = StableHash(tag);
        return TagColors[hash % (uint)TagColors.Length];
    }

    public static float Height(float scale) => 20f * scale;

    public static float Measure(string tag, float scale) =>
        Typography.Measure(tag, SmallTextScale, FontWeight.Medium).X + 16f * scale;

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string tag, float scale)
    {
        var hue = Color(tag);
        var height = Height(scale);
        var width = Measure(tag, scale);
        var min = position;
        var max = new Vector2(position.X + width, position.Y + height);
        var radius = height * 0.5f;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.16f)));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.38f)),
            Metrics.Stroke.Hairline);
        var ink = Palette.WithAlpha(Palette.Mix(hue, White, 0.58f), 0.98f);
        var textSize = Typography.Measure(tag, SmallTextScale, FontWeight.Medium);
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, min.Y + (height - textSize.Y) * 0.5f), tag,
            ink, SmallTextScale, FontWeight.Medium);
    }

    public static float LargeHeight(float scale) => 32f * scale;

    public static float MeasureLarge(string tag, float scale) =>
        Typography.Measure(tag, LargeTextScale, FontWeight.Medium).X + 26f * scale;

    public static void DrawLarge(ImDrawListPtr drawList, Vector2 position, string tag, bool selected, bool hovered,
        float scale)
    {
        var hue = Color(tag);
        var height = LargeHeight(scale);
        var width = MeasureLarge(tag, scale);
        var min = position;
        var max = new Vector2(position.X + width, position.Y + height);
        var radius = height * 0.5f;
        if (selected)
        {
            var fill = hovered ? Palette.Mix(hue, White, 0.10f) : hue;
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(fill, 0.94f)));
        }
        else
        {
            var fillAlpha = hovered ? 0.26f : 0.14f;
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(hue, fillAlpha)));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.40f)),
                Metrics.Stroke.Hairline);
        }

        var ink = selected ? InkOn(hue) : Palette.WithAlpha(Palette.Mix(hue, White, 0.58f), 0.98f);
        var textSize = Typography.Measure(tag, LargeTextScale, FontWeight.Medium);
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, min.Y + (height - textSize.Y) * 0.5f), tag,
            ink, LargeTextScale, FontWeight.Medium);
    }

    private static Vector4 InkOn(Vector4 fill) =>
        Palette.Luminance(fill) > 0.62f ? new Vector4(0.08f, 0.08f, 0.10f, 1f) : new Vector4(1f, 1f, 1f, 0.96f);

    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        for (var index = 0; index < value.Length; index++)
        {
            hash = (hash ^ char.ToLowerInvariant(value[index])) * 16777619u;
        }

        return hash;
    }
}
