namespace Aetherphone.Core.Media;

internal static class PhotoEditor
{
    private const int LutLength = 256;
    private const float LumaRed = 0.299f;
    private const float LumaGreen = 0.587f;
    private const float LumaBlue = 0.114f;
    private const float WarmthRedGain = 0.15f;
    private const float WarmthGreenGain = 0.03f;
    private const float WarmthBlueGain = 0.15f;
    private const float BrightnessReach = 0.35f;
    private const float ContrastReach = 0.9f;
    private const float VignetteStart = 0.35f;
    private const float VignetteDepth = 0.85f;
    private const float DegreesToRadians = MathF.PI / 180f;

    public static PixelImage Apply(in PixelImage source, in PhotoEdit edit)
    {
        if (source.IsEmpty)
        {
            return source;
        }

        var oriented = Orient(source, edit.QuarterTurns, edit.Mirrored);
        var straightened = edit.Straighten != 0f ? Straighten(oriented, edit.Straighten) : oriented;
        return edit.ChangesColor ? Grade(straightened, edit) : straightened;
    }

    public static PixelImage Orient(in PixelImage source, int quarterTurns, bool mirrored)
    {
        var turns = ((quarterTurns % 4) + 4) % 4;
        if (turns == 0 && !mirrored)
        {
            return source;
        }

        var swapsAxes = (turns & 1) == 1;
        var width = swapsAxes ? source.Height : source.Width;
        var height = swapsAxes ? source.Width : source.Height;
        var pixels = new byte[width * height * PixelImage.BytesPerPixel];
        var sourcePixels = source.Pixels;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var rotatedX = mirrored ? width - 1 - x : x;
                int sourceX;
                int sourceY;
                switch (turns)
                {
                    case 1:
                        sourceX = y;
                        sourceY = source.Height - 1 - rotatedX;
                        break;
                    case 2:
                        sourceX = source.Width - 1 - rotatedX;
                        sourceY = source.Height - 1 - y;
                        break;
                    case 3:
                        sourceX = source.Width - 1 - y;
                        sourceY = rotatedX;
                        break;
                    default:
                        sourceX = rotatedX;
                        sourceY = y;
                        break;
                }

                var sourceIndex = ((sourceY * source.Width) + sourceX) * PixelImage.BytesPerPixel;
                var targetIndex = ((y * width) + x) * PixelImage.BytesPerPixel;
                pixels[targetIndex] = sourcePixels[sourceIndex];
                pixels[targetIndex + 1] = sourcePixels[sourceIndex + 1];
                pixels[targetIndex + 2] = sourcePixels[sourceIndex + 2];
                pixels[targetIndex + 3] = sourcePixels[sourceIndex + 3];
            }
        }

        return new PixelImage(pixels, width, height);
    }

    public static float InscribedScale(int width, int height, float degrees)
    {
        if (width <= 0 || height <= 0)
        {
            return 1f;
        }

        var radians = degrees * DegreesToRadians;
        var sine = MathF.Abs(MathF.Sin(radians));
        var cosine = MathF.Abs(MathF.Cos(radians));
        var byWidth = width / ((width * cosine) + (height * sine));
        var byHeight = height / ((width * sine) + (height * cosine));
        return MathF.Min(1f, MathF.Min(byWidth, byHeight));
    }

    public static (int Width, int Height) StraightenedSize(int width, int height, float degrees)
    {
        var scale = InscribedScale(width, height, degrees);
        return (Math.Max(1, (int)MathF.Floor(width * scale)), Math.Max(1, (int)MathF.Floor(height * scale)));
    }

    public static PixelImage Straighten(in PixelImage source, float degrees)
    {
        if (degrees == 0f || source.IsEmpty)
        {
            return source;
        }

        var (width, height) = StraightenedSize(source.Width, source.Height, degrees);
        var radians = degrees * DegreesToRadians;
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        var sourceCenterX = source.Width * 0.5f;
        var sourceCenterY = source.Height * 0.5f;
        var halfWidth = width * 0.5f;
        var halfHeight = height * 0.5f;
        var pixels = new byte[width * height * PixelImage.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            var offsetY = y + 0.5f - halfHeight;
            for (var x = 0; x < width; x++)
            {
                var offsetX = x + 0.5f - halfWidth;
                var sourceX = sourceCenterX + (offsetX * cosine) + (offsetY * sine) - 0.5f;
                var sourceY = sourceCenterY - (offsetX * sine) + (offsetY * cosine) - 0.5f;
                SampleBilinear(source, sourceX, sourceY, pixels, ((y * width) + x) * PixelImage.BytesPerPixel);
            }
        }

        return new PixelImage(pixels, width, height);
    }

    private static void SampleBilinear(in PixelImage source, float sourceX, float sourceY, byte[] target,
        int targetIndex)
    {
        var clampedX = Math.Clamp(sourceX, 0f, source.Width - 1f);
        var clampedY = Math.Clamp(sourceY, 0f, source.Height - 1f);
        var leftX = (int)clampedX;
        var topY = (int)clampedY;
        var rightX = Math.Min(leftX + 1, source.Width - 1);
        var bottomY = Math.Min(topY + 1, source.Height - 1);
        var weightX = clampedX - leftX;
        var weightY = clampedY - topY;
        var topLeft = ((topY * source.Width) + leftX) * PixelImage.BytesPerPixel;
        var topRight = ((topY * source.Width) + rightX) * PixelImage.BytesPerPixel;
        var bottomLeft = ((bottomY * source.Width) + leftX) * PixelImage.BytesPerPixel;
        var bottomRight = ((bottomY * source.Width) + rightX) * PixelImage.BytesPerPixel;
        var pixels = source.Pixels;
        for (var channel = 0; channel < PixelImage.BytesPerPixel; channel++)
        {
            var top = pixels[topLeft + channel] + ((pixels[topRight + channel] - pixels[topLeft + channel]) * weightX);
            var bottom = pixels[bottomLeft + channel]
                + ((pixels[bottomRight + channel] - pixels[bottomLeft + channel]) * weightX);
            var value = top + ((bottom - top) * weightY);
            target[targetIndex + channel] = (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
        }
    }

    public static PixelImage Grade(in PixelImage source, in PhotoEdit edit)
    {
        if (source.IsEmpty || !edit.ChangesColor)
        {
            return source;
        }

        var look = edit.AppliesLook ? PhotoLooks.Of(edit.Look).Scaled(edit.LookStrength) : LookParameters.Neutral;
        var brightness = Math.Clamp(edit.Brightness + look.Brightness, -1f, 1f);
        var contrast = Math.Clamp(edit.Contrast + look.Contrast, -1f, 1f);
        var saturation = Math.Clamp(edit.Saturation + look.Saturation, -1f, 1f);
        var warmth = Math.Clamp(edit.Warmth + look.Warmth, -1f, 1f);
        var vignette = Math.Clamp(edit.Vignette + look.Vignette, 0f, 1f);
        var lift = Math.Clamp(look.Lift, 0f, 1f);

        Span<byte> redLut = stackalloc byte[LutLength];
        Span<byte> greenLut = stackalloc byte[LutLength];
        Span<byte> blueLut = stackalloc byte[LutLength];
        BuildChannelLut(redLut, 1f + (WarmthRedGain * warmth), brightness, contrast, lift);
        BuildChannelLut(greenLut, 1f + (WarmthGreenGain * warmth), brightness, contrast, lift);
        BuildChannelLut(blueLut, 1f - (WarmthBlueGain * warmth), brightness, contrast, lift);

        var width = source.Width;
        var height = source.Height;
        var sourcePixels = source.Pixels;
        var pixels = new byte[source.Length];
        var saturationGain = 1f + saturation;
        var appliesSaturation = saturation != 0f;
        var appliesVignette = vignette > 0f;
        var halfDiagonal = MathF.Sqrt((width * width) + (height * height)) * 0.5f;
        var centerX = width * 0.5f;
        var centerY = height * 0.5f;
        for (var y = 0; y < height; y++)
        {
            var offsetY = y + 0.5f - centerY;
            for (var x = 0; x < width; x++)
            {
                var index = ((y * width) + x) * PixelImage.BytesPerPixel;
                float red = redLut[sourcePixels[index]];
                float green = greenLut[sourcePixels[index + 1]];
                float blue = blueLut[sourcePixels[index + 2]];
                if (appliesSaturation)
                {
                    var luma = (LumaRed * red) + (LumaGreen * green) + (LumaBlue * blue);
                    red = luma + ((red - luma) * saturationGain);
                    green = luma + ((green - luma) * saturationGain);
                    blue = luma + ((blue - luma) * saturationGain);
                }

                if (appliesVignette)
                {
                    var offsetX = x + 0.5f - centerX;
                    var distance = MathF.Sqrt((offsetX * offsetX) + (offsetY * offsetY)) / halfDiagonal;
                    var reach = Math.Clamp((distance - VignetteStart) / (1f - VignetteStart), 0f, 1f);
                    var factor = 1f - (vignette * VignetteDepth * reach * reach);
                    red *= factor;
                    green *= factor;
                    blue *= factor;
                }

                pixels[index] = ToByte(red);
                pixels[index + 1] = ToByte(green);
                pixels[index + 2] = ToByte(blue);
                pixels[index + 3] = sourcePixels[index + 3];
            }
        }

        return new PixelImage(pixels, width, height);
    }

    private static void BuildChannelLut(Span<byte> lut, float gain, float brightness, float contrast, float lift)
    {
        var contrastGain = 1f + (contrast * ContrastReach);
        for (var index = 0; index < LutLength; index++)
        {
            var value = index / 255f * gain;
            value += brightness * BrightnessReach;
            value = ((value - 0.5f) * contrastGain) + 0.5f;
            value = lift + (value * (1f - lift));
            lut[index] = ToByte(value * 255f);
        }
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }
}
