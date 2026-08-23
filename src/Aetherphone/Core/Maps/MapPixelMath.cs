namespace Aetherphone.Core.Maps;

internal static class MapPixelMath
{
    public const float FullCanvasSize = 2048f;

    public static (float X, float Y) ToGameCoordinate(float rawX, float rawY, int sizeFactor)
    {
        var scale = 41f / (Math.Max(sizeFactor, 1) / 100f);
        var x = MathF.Round(10f * (rawX / FullCanvasSize * scale + 1f)) / 10f;
        var y = MathF.Round(10f * (rawY / FullCanvasSize * scale + 1f)) / 10f;
        return (x, y);
    }

    public static (float X, float Y) NormalizeToFullCanvas(float rawX, float rawY) =>
        (rawX / FullCanvasSize, rawY / FullCanvasSize);
}
