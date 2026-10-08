namespace Aetherphone.Apps.Casino.Stage;

internal static class StageContrast
{
    public const float Readable = 4.5f;

    public static float RelativeLuminance(Vector4 color) =>
        0.2126f * Linear(color.X) + 0.7152f * Linear(color.Y) + 0.0722f * Linear(color.Z);

    public static float Ratio(Vector4 foreground, Vector4 background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        var lighter = MathF.Max(first, second);
        var darker = MathF.Min(first, second);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    public static Vector4 Over(Vector4 top, Vector4 bottom)
    {
        var alpha = Math.Clamp(top.W, 0f, 1f);
        return new Vector4(top.X * alpha + bottom.X * (1f - alpha), top.Y * alpha + bottom.Y * (1f - alpha),
            top.Z * alpha + bottom.Z * (1f - alpha), 1f);
    }

    private static float Linear(float channel) =>
        channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
}
