using System.Numerics;

namespace Aetherphone.Apps.Games.Framework;

internal static class GamePalette
{
    public static readonly Vector4 Board = new(0.085f, 0.095f, 0.120f, 1f);

    public static readonly Vector4 Cell = new(0.130f, 0.150f, 0.190f, 1f);

    public static readonly Vector4 CellHover = new(0.180f, 0.210f, 0.260f, 1f);

    public static readonly Vector4 CellSunken = new(0.095f, 0.105f, 0.135f, 1f);

    public static readonly Vector4 InkLight = new(0.97f, 0.97f, 0.98f, 1f);

    public static readonly Vector4 InkDark = new(0.12f, 0.13f, 0.16f, 1f);

    public static Vector4 InkOn(Vector4 fill)
    {
        var luminance = fill.X * 0.299f + fill.Y * 0.587f + fill.Z * 0.114f;
        return luminance > 0.62f ? InkDark : InkLight;
    }

    public static Vector4 Lighten(Vector4 color, float amount)
    {
        return Vector4.Lerp(color, new Vector4(1f, 1f, 1f, color.W), amount);
    }

    public static Vector4 Darken(Vector4 color, float amount)
    {
        return Vector4.Lerp(color, new Vector4(0f, 0f, 0f, color.W), amount);
    }
}
