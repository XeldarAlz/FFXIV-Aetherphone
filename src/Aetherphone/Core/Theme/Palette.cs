namespace Aetherphone.Core.Theme;

internal static class Palette
{
    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };
    public static Vector4 Mix(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, amount);
    public static float Luminance(Vector4 color) => color.X * 0.299f + color.Y * 0.587f + color.Z * 0.114f;

    public static float RelativeLuminance(Vector4 color) =>
        0.2126f * LinearChannel(color.X) + 0.7152f * LinearChannel(color.Y) + 0.0722f * LinearChannel(color.Z);

    public static float ContrastRatio(Vector4 first, Vector4 second)
    {
        var one = RelativeLuminance(first);
        var other = RelativeLuminance(second);
        return one > other ? (one + 0.05f) / (other + 0.05f) : (other + 0.05f) / (one + 0.05f);
    }

    public static Vector4 ShadeToLuminance(Vector4 color, float target)
    {
        var luminance = RelativeLuminance(color);
        if (luminance <= target || luminance <= 0f)
        {
            return color;
        }

        var factor = target / luminance;
        return new Vector4(
            EncodeChannel(LinearChannel(color.X) * factor),
            EncodeChannel(LinearChannel(color.Y) * factor),
            EncodeChannel(LinearChannel(color.Z) * factor),
            color.W);
    }

    private static float LinearChannel(float channel) =>
        channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

    private static float EncodeChannel(float channel) =>
        channel <= 0.0031308f ? channel * 12.92f : 1.055f * MathF.Pow(channel, 1f / 2.4f) - 0.055f;

    public static Vector4 Lighten(Vector4 color, float amount) =>
        Vector4.Lerp(color, new Vector4(1f, 1f, 1f, color.W), amount);

    public static Vector4 Darken(Vector4 color, float amount) =>
        Vector4.Lerp(color, new Vector4(0f, 0f, 0f, color.W), amount);

    public static Vector4 FromHue(float hue, float saturation, float value)
    {
        var sector = (hue - MathF.Floor(hue)) * 6f;
        var slice = (int)sector % 6;
        var fraction = sector - MathF.Floor(sector);
        var low = value * (1f - saturation);
        var falling = value * (1f - saturation * fraction);
        var rising = value * (1f - saturation * (1f - fraction));
        return slice switch
        {
            0 => new Vector4(value, rising, low, 1f),
            1 => new Vector4(falling, value, low, 1f),
            2 => new Vector4(low, value, rising, 1f),
            3 => new Vector4(low, falling, value, 1f),
            4 => new Vector4(rising, low, value, 1f),
            _ => new Vector4(value, low, falling, 1f),
        };
    }
}
