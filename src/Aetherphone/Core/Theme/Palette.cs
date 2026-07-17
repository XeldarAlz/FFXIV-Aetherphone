namespace Aetherphone.Core.Theme;

internal static class Palette
{
    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };
    public static Vector4 Mix(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, amount);
    public static float Luminance(Vector4 color) => color.X * 0.299f + color.Y * 0.587f + color.Z * 0.114f;

    public static Vector4 Lighten(Vector4 color, float amount) =>
        Vector4.Lerp(color, new Vector4(1f, 1f, 1f, color.W), amount);

    public static Vector4 Darken(Vector4 color, float amount) =>
        Vector4.Lerp(color, new Vector4(0f, 0f, 0f, color.W), amount);
}
