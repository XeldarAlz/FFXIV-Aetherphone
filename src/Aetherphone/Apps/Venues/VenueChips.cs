using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal static class VenueChips
{
    private const float SmallTextScale = 0.72f;
    private static readonly Vector4 AdultColor = new(0.90f, 0.26f, 0.44f, 1f);
    private static readonly Vector4 SfwColor = new(0.86f, 0.72f, 0.24f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 NeutralFill = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 NeutralInk = new(1f, 1f, 1f, 0.72f);

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

    public static void DrawNeutral(ImDrawListPtr drawList, Vector2 position, string label, float scale)
    {
        var height = Height(scale);
        var width = Measure(label, scale);
        var max = new Vector2(position.X + width, position.Y + height);
        Squircle.Fill(drawList, position, max, height * 0.5f, ImGui.GetColorU32(NeutralFill));
        var textSize = Typography.Measure(label, SmallTextScale, FontWeight.Medium);
        Typography.Draw(drawList,
            new Vector2(position.X + (width - textSize.X) * 0.5f, position.Y + (height - textSize.Y) * 0.5f), label,
            NeutralInk, SmallTextScale, FontWeight.Medium);
    }

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
