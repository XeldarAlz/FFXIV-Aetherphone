using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Media;

internal static class IconBake
{
    public const float WhiteLuminanceFloor = 0.86f;
    public const float WhiteShareFloor = 0.90f;
    public const float LightTintDarkening = 0.30f;
    private const byte VisibleAlpha = 128;
    private const float ByteScale = 1f / 255f;
    public static readonly Vector4 GraphiteTop = new(58f / 255f, 58f / 255f, 60f / 255f, 1f);
    public static readonly Vector4 GraphiteBottom = new(28f / 255f, 28f / 255f, 30f / 255f, 1f);
    public static readonly Vector4 PaperTop = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 PaperBottom = new(242f / 255f, 242f / 255f, 247f / 255f, 1f);

    public static bool IsWhiteForeground(in PixelImage foreground)
    {
        if (foreground.IsEmpty)
        {
            return false;
        }

        var pixels = foreground.Pixels;
        var length = foreground.Length;
        var visible = 0;
        var white = 0;
        for (var offset = 0; offset + 3 < length; offset += PixelImage.BytesPerPixel)
        {
            if (pixels[offset + 3] < VisibleAlpha)
            {
                continue;
            }

            visible++;
            if (Luminance(pixels[offset], pixels[offset + 1], pixels[offset + 2]) >= WhiteLuminanceFloor)
            {
                white++;
            }
        }

        return visible > 0 && white >= visible * WhiteShareFloor;
    }

    public static PixelImage BakeDark(in PixelImage foreground, Vector4 accent)
    {
        var result = Gradient(foreground.Width, foreground.Height, GraphiteTop, GraphiteBottom);
        if (IsWhiteForeground(foreground))
        {
            CompositeRecoloured(result, foreground, accent);
        }
        else
        {
            Composite(result, foreground);
        }

        return result;
    }

    public static PixelImage BakeTinted(in PixelImage foreground, Vector4 accent, bool light)
    {
        var result = light
            ? Gradient(foreground.Width, foreground.Height, PaperTop, PaperBottom)
            : Gradient(foreground.Width, foreground.Height, GraphiteTop, GraphiteBottom);
        var ink = light ? Palette.Darken(accent, LightTintDarkening) : accent;
        CompositeMask(result, foreground, ink);
        return result;
    }

    public static PixelImage BakeMask(in PixelImage foreground)
    {
        var length = foreground.Length;
        var source = foreground.Pixels;
        var pixels = new byte[length];
        for (var offset = 0; offset + 3 < length; offset += PixelImage.BytesPerPixel)
        {
            pixels[offset] = 255;
            pixels[offset + 1] = 255;
            pixels[offset + 2] = 255;
            pixels[offset + 3] = ToByte(Coverage(source, offset));
        }

        return new PixelImage(pixels, foreground.Width, foreground.Height);
    }

    public static PixelImage Gradient(int width, int height, Vector4 top, Vector4 bottom)
    {
        var pixels = new byte[width * height * PixelImage.BytesPerPixel];
        var lastRow = MathF.Max(height - 1, 1);
        for (var row = 0; row < height; row++)
        {
            var colour = Vector4.Lerp(top, bottom, row / lastRow);
            var red = ToByte(colour.X);
            var green = ToByte(colour.Y);
            var blue = ToByte(colour.Z);
            var rowOffset = row * width * PixelImage.BytesPerPixel;
            for (var column = 0; column < width; column++)
            {
                var offset = rowOffset + column * PixelImage.BytesPerPixel;
                pixels[offset] = red;
                pixels[offset + 1] = green;
                pixels[offset + 2] = blue;
                pixels[offset + 3] = 255;
            }
        }

        return new PixelImage(pixels, width, height);
    }

    private static void Composite(in PixelImage target, in PixelImage foreground)
    {
        var source = foreground.Pixels;
        var pixels = target.Pixels;
        var length = Math.Min(target.Length, foreground.Length);
        for (var offset = 0; offset + 3 < length; offset += PixelImage.BytesPerPixel)
        {
            var alpha = source[offset + 3] * ByteScale;
            if (alpha <= 0f)
            {
                continue;
            }

            Blend(pixels, offset, source[offset], source[offset + 1], source[offset + 2], alpha);
        }
    }

    private static void CompositeRecoloured(in PixelImage target, in PixelImage foreground, Vector4 accent)
    {
        var source = foreground.Pixels;
        var pixels = target.Pixels;
        var length = Math.Min(target.Length, foreground.Length);
        for (var offset = 0; offset + 3 < length; offset += PixelImage.BytesPerPixel)
        {
            var alpha = source[offset + 3] * ByteScale;
            if (alpha <= 0f)
            {
                continue;
            }

            var luminance = Luminance(source[offset], source[offset + 1], source[offset + 2]);
            Blend(pixels, offset, ToByte(accent.X * luminance), ToByte(accent.Y * luminance),
                ToByte(accent.Z * luminance), alpha);
        }
    }

    private static void CompositeMask(in PixelImage target, in PixelImage foreground, Vector4 ink)
    {
        var source = foreground.Pixels;
        var pixels = target.Pixels;
        var length = Math.Min(target.Length, foreground.Length);
        var red = ToByte(ink.X);
        var green = ToByte(ink.Y);
        var blue = ToByte(ink.Z);
        for (var offset = 0; offset + 3 < length; offset += PixelImage.BytesPerPixel)
        {
            var coverage = Coverage(source, offset);
            if (coverage <= 0f)
            {
                continue;
            }

            Blend(pixels, offset, red, green, blue, coverage);
        }
    }

    private static void Blend(byte[] pixels, int offset, byte red, byte green, byte blue, float alpha)
    {
        var inverse = 1f - alpha;
        pixels[offset] = ToByte((red * alpha + pixels[offset] * inverse) * ByteScale);
        pixels[offset + 1] = ToByte((green * alpha + pixels[offset + 1] * inverse) * ByteScale);
        pixels[offset + 2] = ToByte((blue * alpha + pixels[offset + 2] * inverse) * ByteScale);
        pixels[offset + 3] = 255;
    }

    private static float Coverage(byte[] source, int offset) =>
        Luminance(source[offset], source[offset + 1], source[offset + 2]) * (source[offset + 3] * ByteScale);

    private static float Luminance(byte red, byte green, byte blue) =>
        (red * 0.299f + green * 0.587f + blue * 0.114f) * ByteScale;

    private static byte ToByte(float unit) => (byte)Math.Clamp((int)MathF.Round(unit * 255f), 0, 255);
}
