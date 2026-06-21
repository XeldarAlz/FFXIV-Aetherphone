using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class WallpaperPainter
{
    public static byte[] Rasterize(WallpaperStyle style, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var area = new Rect(Vector2.Zero, new Vector2(width, height));

        switch (style)
        {
            case WallpaperStyle.Aurora:
                VerticalGradient(pixels, width, height, new Vector4(0.18f, 0.13f, 0.32f, 1f), new Vector4(0.05f, 0.09f, 0.15f, 1f));
                Blob(pixels, width, height, new Vector2(area.Min.X + area.Width * 0.22f, area.Min.Y + area.Height * 0.18f), area.Width * 0.62f, new Vector4(0.50f, 0.32f, 0.88f, 1f));
                Blob(pixels, width, height, new Vector2(area.Max.X - area.Width * 0.18f, area.Min.Y + area.Height * 0.52f), area.Width * 0.58f, new Vector4(0.20f, 0.62f, 0.60f, 1f));
                break;
            case WallpaperStyle.Ocean:
                VerticalGradient(pixels, width, height, new Vector4(0.10f, 0.21f, 0.42f, 1f), new Vector4(0.03f, 0.06f, 0.14f, 1f));
                Blob(pixels, width, height, new Vector2(area.Center.X, area.Max.Y - area.Height * 0.12f), area.Width * 0.80f, new Vector4(0.22f, 0.50f, 0.85f, 1f));
                break;
            case WallpaperStyle.Ember:
                VerticalGradient(pixels, width, height, new Vector4(0.16f, 0.08f, 0.16f, 1f), new Vector4(0.30f, 0.11f, 0.07f, 1f));
                Blob(pixels, width, height, new Vector2(area.Center.X, area.Max.Y - area.Height * 0.08f), area.Width * 0.85f, new Vector4(0.92f, 0.45f, 0.20f, 1f));
                break;
            case WallpaperStyle.Mono:
                VerticalGradient(pixels, width, height, new Vector4(0.13f, 0.13f, 0.15f, 1f), new Vector4(0.05f, 0.05f, 0.06f, 1f));
                Blob(pixels, width, height, new Vector2(area.Center.X, area.Min.Y + area.Height * 0.30f), area.Width * 0.70f, new Vector4(0.32f, 0.32f, 0.38f, 1f));
                break;
            default:
                VerticalGradient(pixels, width, height, new Vector4(0.15f, 0.13f, 0.27f, 1f), new Vector4(0.04f, 0.04f, 0.08f, 1f));
                Blob(pixels, width, height, new Vector2(area.Center.X, area.Min.Y + area.Height * 0.12f), area.Width * 0.70f, new Vector4(0.45f, 0.38f, 0.90f, 1f));
                break;
        }

        return pixels;
    }

    private static void VerticalGradient(byte[] pixels, int width, int height, Vector4 top, Vector4 bottom)
    {
        var lastRow = height > 1 ? height - 1 : 1;
        for (var y = 0; y < height; y++)
        {
            var color = Vector4.Lerp(top, bottom, y / (float)lastRow);
            var red = ToByte(color.X);
            var green = ToByte(color.Y);
            var blue = ToByte(color.Z);
            var rowStart = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var index = rowStart + x * 4;
                pixels[index] = red;
                pixels[index + 1] = green;
                pixels[index + 2] = blue;
                pixels[index + 3] = 255;
            }
        }
    }

    private static void Blob(byte[] pixels, int width, int height, Vector2 center, float radius, Vector4 color)
    {
        for (var ring = 3; ring >= 1; ring--)
        {
            FillCircle(pixels, width, height, center, radius * (0.45f + ring * 0.18f), color, 0.05f * ring);
        }
    }

    private static void FillCircle(byte[] pixels, int width, int height, Vector2 center, float radius, Vector4 color, float alpha)
    {
        var minX = Math.Max(0, (int)MathF.Floor(center.X - radius - 1f));
        var maxX = Math.Min(width - 1, (int)MathF.Ceiling(center.X + radius + 1f));
        var minY = Math.Max(0, (int)MathF.Floor(center.Y - radius - 1f));
        var maxY = Math.Min(height - 1, (int)MathF.Ceiling(center.Y + radius + 1f));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var deltaX = x + 0.5f - center.X;
                var deltaY = y + 0.5f - center.Y;
                var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                var coverage = Math.Clamp(radius - distance + 0.5f, 0f, 1f);
                if (coverage <= 0f)
                {
                    continue;
                }

                var weight = alpha * coverage;
                var index = (y * width + x) * 4;
                pixels[index] = ToByte(Lerp(pixels[index] / 255f, color.X, weight));
                pixels[index + 1] = ToByte(Lerp(pixels[index + 1] / 255f, color.Y, weight));
                pixels[index + 2] = ToByte(Lerp(pixels[index + 2] / 255f, color.Z, weight));
            }
        }
    }

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static byte ToByte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);
}
