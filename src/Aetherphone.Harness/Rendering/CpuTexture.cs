namespace Aetherphone.Harness.Rendering;

internal sealed class CpuTexture
{
    public readonly int Width;
    public readonly int Height;
    public readonly byte[] Rgba;

    public CpuTexture(int width, int height, byte[] rgba)
    {
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public static CpuTexture Solid(int width, int height, byte red, byte green, byte blue, byte alpha)
    {
        var rgba = new byte[width * height * 4];
        for (var offset = 0; offset < rgba.Length; offset += 4)
        {
            rgba[offset] = red;
            rgba[offset + 1] = green;
            rgba[offset + 2] = blue;
            rgba[offset + 3] = alpha;
        }

        return new CpuTexture(width, height, rgba);
    }

    public void SampleBilinear(float u, float v, out float red, out float green, out float blue, out float alpha)
    {
        var x = u * Width - 0.5f;
        var y = v * Height - 0.5f;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fractionX = x - x0;
        var fractionY = y - y0;
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        x0 = Math.Clamp(x0, 0, Width - 1);
        x1 = Math.Clamp(x1, 0, Width - 1);
        y0 = Math.Clamp(y0, 0, Height - 1);
        y1 = Math.Clamp(y1, 0, Height - 1);
        var row0 = y0 * Width;
        var row1 = y1 * Width;
        var index00 = (row0 + x0) * 4;
        var index10 = (row0 + x1) * 4;
        var index01 = (row1 + x0) * 4;
        var index11 = (row1 + x1) * 4;
        var weight00 = (1f - fractionX) * (1f - fractionY);
        var weight10 = fractionX * (1f - fractionY);
        var weight01 = (1f - fractionX) * fractionY;
        var weight11 = fractionX * fractionY;
        var pixels = Rgba;
        red = pixels[index00] * weight00 + pixels[index10] * weight10 + pixels[index01] * weight01 + pixels[index11] * weight11;
        green = pixels[index00 + 1] * weight00 + pixels[index10 + 1] * weight10 + pixels[index01 + 1] * weight01 + pixels[index11 + 1] * weight11;
        blue = pixels[index00 + 2] * weight00 + pixels[index10 + 2] * weight10 + pixels[index01 + 2] * weight01 + pixels[index11 + 2] * weight11;
        alpha = pixels[index00 + 3] * weight00 + pixels[index10 + 3] * weight10 + pixels[index01 + 3] * weight01 + pixels[index11 + 3] * weight11;
    }
}
