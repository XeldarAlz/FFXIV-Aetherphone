namespace Aetherphone.Core.Emulation;

internal static class NearestNeighborScaler
{
    public static int IntegerScale(int sourceWidth, int sourceHeight, float availableWidth, float availableHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return 1;
        }

        var horizontal = (int)MathF.Floor(availableWidth / sourceWidth);
        var vertical = (int)MathF.Floor(availableHeight / sourceHeight);
        return Math.Max(1, Math.Min(horizontal, vertical));
    }

    public static void ScaleBgra(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight,
        Span<byte> destination, int destinationWidth, int destinationHeight)
    {
        var sourceLength = checked(sourceWidth * sourceHeight * 4);
        var destinationLength = checked(destinationWidth * destinationHeight * 4);
        if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0 ||
            source.Length < sourceLength || destination.Length < destinationLength)
        {
            throw new ArgumentException("Pixel buffers do not match the supplied dimensions.");
        }

        for (var destinationY = 0; destinationY < destinationHeight; destinationY++)
        {
            var sourceY = destinationY * sourceHeight / destinationHeight;
            var sourceRow = sourceY * sourceWidth * 4;
            var destinationRow = destinationY * destinationWidth * 4;
            for (var destinationX = 0; destinationX < destinationWidth; destinationX++)
            {
                var sourcePixel = sourceRow + destinationX * sourceWidth / destinationWidth * 4;
                var destinationPixel = destinationRow + destinationX * 4;
                destination[destinationPixel] = source[sourcePixel];
                destination[destinationPixel + 1] = source[sourcePixel + 1];
                destination[destinationPixel + 2] = source[sourcePixel + 2];
                destination[destinationPixel + 3] = source[sourcePixel + 3];
            }
        }
    }

    public static void ScaleSharpBilinearBgra(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight,
        Span<byte> destination, int destinationWidth, int destinationHeight)
    {
        ValidateBuffers(source, sourceWidth, sourceHeight, destination, destinationWidth, destinationHeight);

        var scaleX = destinationWidth / (float)sourceWidth;
        var scaleY = destinationHeight / (float)sourceHeight;
        for (var destinationY = 0; destinationY < destinationHeight; destinationY++)
        {
            var sourceY = SharpBilinearCoordinate(destinationY, scaleY);
            var sourceFloorY = MathF.Floor(sourceY);
            var y0 = Math.Clamp((int)sourceFloorY, 0, sourceHeight - 1);
            var y1 = Math.Min(y0 + 1, sourceHeight - 1);
            var blendY = Math.Clamp(sourceY - sourceFloorY, 0f, 1f);
            var sourceRow0 = y0 * sourceWidth * 4;
            var sourceRow1 = y1 * sourceWidth * 4;
            var destinationRow = destinationY * destinationWidth * 4;

            for (var destinationX = 0; destinationX < destinationWidth; destinationX++)
            {
                var sourceX = SharpBilinearCoordinate(destinationX, scaleX);
                var sourceFloorX = MathF.Floor(sourceX);
                var x0 = Math.Clamp((int)sourceFloorX, 0, sourceWidth - 1);
                var x1 = Math.Min(x0 + 1, sourceWidth - 1);
                var blendX = Math.Clamp(sourceX - sourceFloorX, 0f, 1f);
                var topLeft = sourceRow0 + x0 * 4;
                var topRight = sourceRow0 + x1 * 4;
                var bottomLeft = sourceRow1 + x0 * 4;
                var bottomRight = sourceRow1 + x1 * 4;
                var destinationPixel = destinationRow + destinationX * 4;

                for (var channel = 0; channel < 4; channel++)
                {
                    var top = Lerp(source[topLeft + channel], source[topRight + channel], blendX);
                    var bottom = Lerp(source[bottomLeft + channel], source[bottomRight + channel], blendX);
                    destination[destinationPixel + channel] = (byte)(Lerp(top, bottom, blendY) + 0.5f);
                }
            }
        }
    }

    private static float SharpBilinearCoordinate(int destinationPixel, float scale)
    {
        var sourcePosition = (destinationPixel + 0.5f) / scale;
        if (scale <= 1f)
        {
            return sourcePosition - 0.5f;
        }

        var texel = MathF.Floor(sourcePosition);
        var fraction = sourcePosition - texel;
        var flatRegion = 0.5f - 0.5f / scale;
        var centerDistance = fraction - 0.5f;
        var edgeBlend = (centerDistance - Math.Clamp(centerDistance, -flatRegion, flatRegion)) * scale + 0.5f;
        return texel + edgeBlend - 0.5f;
    }

    private static float Lerp(float left, float right, float amount) => left + (right - left) * amount;

    private static void ValidateBuffers(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight,
        Span<byte> destination, int destinationWidth, int destinationHeight)
    {
        var sourceLength = checked(sourceWidth * sourceHeight * 4);
        var destinationLength = checked(destinationWidth * destinationHeight * 4);
        if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0 ||
            source.Length < sourceLength || destination.Length < destinationLength)
        {
            throw new ArgumentException("Pixel buffers do not match the supplied dimensions.");
        }
    }
}
