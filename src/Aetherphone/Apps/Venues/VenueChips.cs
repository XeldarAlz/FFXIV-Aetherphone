using System.Numerics;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal static class VenueChips
{
    private static readonly Vector4 AdultColor = new(0.86f, 0.24f, 0.46f, 1f);
    private static readonly Vector4 SfwColor = new(0.86f, 0.74f, 0.22f, 1f);

    private static readonly Vector4[] Palette =
    {
        new(0.91f, 0.49f, 0.22f, 1f),
        new(0.27f, 0.71f, 0.62f, 1f),
        new(0.36f, 0.55f, 0.92f, 1f),
        new(0.64f, 0.45f, 0.90f, 1f),
        new(0.32f, 0.74f, 0.42f, 1f),
        new(0.90f, 0.42f, 0.62f, 1f),
        new(0.86f, 0.62f, 0.24f, 1f),
        new(0.40f, 0.68f, 0.84f, 1f),
        new(0.78f, 0.40f, 0.42f, 1f),
        new(0.50f, 0.62f, 0.30f, 1f),
        new(0.55f, 0.50f, 0.86f, 1f),
        new(0.30f, 0.66f, 0.70f, 1f),
    };

    public static Vector4 Color(string tag)
    {
        if (string.Equals(tag, "18+", StringComparison.OrdinalIgnoreCase) || tag.Contains("NSFW", StringComparison.OrdinalIgnoreCase))
        {
            return AdultColor;
        }

        if (string.Equals(tag, "SFW", StringComparison.OrdinalIgnoreCase))
        {
            return SfwColor;
        }

        var hash = StableHash(tag);
        return Palette[hash % (uint)Palette.Length];
    }

    public static float Measure(string tag, float scale)
    {
        var size = Typography.Measure(tag, 0.72f);
        return size.X + 14f * scale;
    }

    public static float Height(float scale) => 19f * scale;

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string tag, float scale)
    {
        var fill = Color(tag);
        var height = Height(scale);
        var width = Measure(tag, scale);
        var min = position;
        var max = new Vector2(position.X + width, position.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill with { W = 0.92f }));

        var ink = Luminance(fill) > 0.62f ? new Vector4(0.08f, 0.08f, 0.10f, 1f) : new Vector4(1f, 1f, 1f, 0.96f);
        var textSize = Typography.Measure(tag, 0.72f);
        var textPosition = new Vector2(min.X + (width - textSize.X) * 0.5f, min.Y + (height - textSize.Y) * 0.5f);
        Typography.Draw(textPosition, tag, ink, 0.72f);
    }

    private static float Luminance(Vector4 color) => 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z;

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
