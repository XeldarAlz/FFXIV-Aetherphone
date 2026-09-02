namespace Aetherphone.Harness.Rendering;

internal sealed unsafe class SoftwareRasterizer
{
    private const float ChannelScale = 1f / 255f;
    private readonly byte[] pixels;
    private readonly float[] accumulator;

    public SoftwareRasterizer(int width, int height)
    {
        Width = width;
        Height = height;
        pixels = new byte[width * height * 4];
        accumulator = new float[width * height * 4];
    }

    public int Width { get; }

    public int Height { get; }

    public int TrianglesDrawn { get; private set; }

    public void Clear(byte red, byte green, byte blue, byte alpha)
    {
        TrianglesDrawn = 0;
        var target = accumulator;
        for (var offset = 0; offset < target.Length; offset += 4)
        {
            target[offset] = red;
            target[offset + 1] = green;
            target[offset + 2] = blue;
            target[offset + 3] = alpha;
        }
    }

    public byte[] Resolve()
    {
        var source = accumulator;
        var target = pixels;
        for (var index = 0; index < target.Length; index++)
        {
            target[index] = (byte)Math.Clamp(source[index] + 0.5f, 0f, 255f);
        }

        return target;
    }

    public void DrawTriangles(DrawVertex* vertices, ushort* indices, int elementCount, Vector4 clipRect,
        Vector2 displayOffset, CpuTexture texture)
    {
        var clipMinX = Math.Max((int)MathF.Floor(clipRect.X - displayOffset.X), 0);
        var clipMinY = Math.Max((int)MathF.Floor(clipRect.Y - displayOffset.Y), 0);
        var clipMaxX = Math.Min((int)MathF.Ceiling(clipRect.Z - displayOffset.X), Width) - 1;
        var clipMaxY = Math.Min((int)MathF.Ceiling(clipRect.W - displayOffset.Y), Height) - 1;
        if (clipMinX > clipMaxX || clipMinY > clipMaxY)
        {
            return;
        }

        for (var elementIndex = 0; elementIndex + 2 < elementCount; elementIndex += 3)
        {
            var vertex0 = vertices[indices[elementIndex]];
            var vertex1 = vertices[indices[elementIndex + 1]];
            var vertex2 = vertices[indices[elementIndex + 2]];
            DrawTriangle(in vertex0, in vertex1, in vertex2, displayOffset, texture, clipMinX, clipMinY, clipMaxX,
                clipMaxY);
        }
    }

    private void DrawTriangle(in DrawVertex vertex0, in DrawVertex vertex1, in DrawVertex vertex2,
        Vector2 displayOffset, CpuTexture texture, int clipMinX, int clipMinY, int clipMaxX, int clipMaxY)
    {
        var position0 = vertex0.Position - displayOffset;
        var position1 = vertex1.Position - displayOffset;
        var position2 = vertex2.Position - displayOffset;
        var area = Orient(position0, position1, position2);
        if (area == 0f)
        {
            return;
        }

        var first = vertex0;
        var second = vertex1;
        var third = vertex2;
        if (area < 0f)
        {
            second = vertex2;
            third = vertex1;
            (position1, position2) = (position2, position1);
            area = -area;
        }

        var minX = Math.Max((int)MathF.Floor(MathF.Min(position0.X, MathF.Min(position1.X, position2.X))), clipMinX);
        var maxX = Math.Min((int)MathF.Ceiling(MathF.Max(position0.X, MathF.Max(position1.X, position2.X))), clipMaxX);
        var minY = Math.Max((int)MathF.Floor(MathF.Min(position0.Y, MathF.Min(position1.Y, position2.Y))), clipMinY);
        var maxY = Math.Min((int)MathF.Ceiling(MathF.Max(position0.Y, MathF.Max(position1.Y, position2.Y))), clipMaxY);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        TrianglesDrawn += 1;
        var inverseArea = 1f / area;
        var topLeft0 = IsTopLeft(position1, position2);
        var topLeft1 = IsTopLeft(position2, position0);
        var topLeft2 = IsTopLeft(position0, position1);
        UnpackColor(first.Color, out var red0, out var green0, out var blue0, out var alpha0);
        UnpackColor(second.Color, out var red1, out var green1, out var blue1, out var alpha1);
        UnpackColor(third.Color, out var red2, out var green2, out var blue2, out var alpha2);
        var target = accumulator;
        var startCenter = new Vector2(minX + 0.5f, minY + 0.5f);
        var rowWeight0 = Orient(position1, position2, startCenter);
        var rowWeight1 = Orient(position2, position0, startCenter);
        var rowWeight2 = Orient(position0, position1, startCenter);
        var stepX0 = position1.Y - position2.Y;
        var stepX1 = position2.Y - position0.Y;
        var stepX2 = position0.Y - position1.Y;
        var stepY0 = position2.X - position1.X;
        var stepY1 = position0.X - position2.X;
        var stepY2 = position1.X - position0.X;
        for (var y = minY; y <= maxY; y++)
        {
            var weight0 = rowWeight0;
            var weight1 = rowWeight1;
            var weight2 = rowWeight2;
            var rowOffset = y * Width;
            for (var x = minX; x <= maxX; x++)
            {
                var inside = (weight0 > 0f || (weight0 == 0f && topLeft0)) &&
                             (weight1 > 0f || (weight1 == 0f && topLeft1)) &&
                             (weight2 > 0f || (weight2 == 0f && topLeft2));
                if (inside)
                {
                    var barycentric0 = weight0 * inverseArea;
                    var barycentric1 = weight1 * inverseArea;
                    var barycentric2 = weight2 * inverseArea;
                    var u = first.Uv.X * barycentric0 + second.Uv.X * barycentric1 + third.Uv.X * barycentric2;
                    var v = first.Uv.Y * barycentric0 + second.Uv.Y * barycentric1 + third.Uv.Y * barycentric2;
                    texture.SampleBilinear(u, v, out var texelRed, out var texelGreen, out var texelBlue,
                        out var texelAlpha);
                    var red = (red0 * barycentric0 + red1 * barycentric1 + red2 * barycentric2) * texelRed * ChannelScale;
                    var green = (green0 * barycentric0 + green1 * barycentric1 + green2 * barycentric2) * texelGreen * ChannelScale;
                    var blue = (blue0 * barycentric0 + blue1 * barycentric1 + blue2 * barycentric2) * texelBlue * ChannelScale;
                    var alpha = (alpha0 * barycentric0 + alpha1 * barycentric1 + alpha2 * barycentric2) * texelAlpha * ChannelScale * ChannelScale;
                    if (alpha > 0f)
                    {
                        var offset = (rowOffset + x) * 4;
                        var inverseAlpha = 1f - alpha;
                        target[offset] = red * alpha + target[offset] * inverseAlpha;
                        target[offset + 1] = green * alpha + target[offset + 1] * inverseAlpha;
                        target[offset + 2] = blue * alpha + target[offset + 2] * inverseAlpha;
                        target[offset + 3] = 255f * alpha + target[offset + 3] * inverseAlpha;
                    }
                }

                weight0 += stepX0;
                weight1 += stepX1;
                weight2 += stepX2;
            }

            rowWeight0 += stepY0;
            rowWeight1 += stepY1;
            rowWeight2 += stepY2;
        }
    }

    private static float Orient(Vector2 a, Vector2 b, Vector2 c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool IsTopLeft(Vector2 a, Vector2 b) => (a.Y == b.Y && b.X > a.X) || b.Y < a.Y;

    private static void UnpackColor(uint packed, out float red, out float green, out float blue, out float alpha)
    {
        red = packed & 0xFF;
        green = (packed >> 8) & 0xFF;
        blue = (packed >> 16) & 0xFF;
        alpha = (packed >> 24) & 0xFF;
    }
}
