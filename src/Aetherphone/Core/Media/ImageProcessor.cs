using System.Buffers;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherphone.Core.Media;

internal readonly struct BakedImage
{
    public readonly byte[] Bytes;
    public readonly int Width;
    public readonly int Height;

    public BakedImage(byte[] bytes, int width, int height)
    {
        Bytes = bytes;
        Width = width;
        Height = height;
    }
}

internal static class ImageProcessor
{
    private const int JpegQuality = 88;
    private const int JpegQualityFallback = 91;
    private const int EncodeAttemptLimit = 3;
    private const float LumaDeltaLimit = 80f;
    private const float ChromaDeltaLimit = 64f;
    private const int CorruptSampleLimit = 1;

    public const long MaxDecodePixels = 4096L * 4096L;
    public const long MaxLocalDecodePixels = 8192L * 8192L;
    public const int MaxAnimationDimension = 480;
    public const long MaxAnimationPixels = 9_000_000L;
    private const long MaxAnimationSourcePixels = 40_000_000L;
    private const int MaxAnimationSourceFrames = 300;
    internal static readonly DecoderOptions SingleFrame = new() { MaxFrames = 1 };

    private static void EnsureDecodable(Stream stream, long maxPixels)
    {
        var info = Image.Identify(stream);
        stream.Position = 0;

        if ((long)info.Width * info.Height > maxPixels)
        {
            throw new InvalidImageContentException(
                $"Image dimensions {info.Width}x{info.Height} exceed the {maxPixels} pixel limit.");
        }
    }

    private static Image<Rgba32> LoadRgba32(Stream stream, long maxPixels, int maxDimension, out int length)
    {
        EnsureDecodable(stream, maxPixels);
        var image = Image.Load<Rgba32>(SingleFrame, stream);
        ScaleWithin(image, maxDimension);
        length = checked(image.Width * image.Height * 4);
        return image;
    }

    private static void ScaleWithin(Image image, int maxDimension)
    {
        if (maxDimension <= 0 || (image.Width <= maxDimension && image.Height <= maxDimension))
        {
            return;
        }

        var factor = MathF.Min((float)maxDimension / image.Width, (float)maxDimension / image.Height);
        var width = Math.Max(1, (int)MathF.Round(image.Width * factor));
        var height = Math.Max(1, (int)MathF.Round(image.Height * factor));
        image.Mutate(context => context.Resize(width, height));
    }

    public static BakedImage BakeSquareJpeg(string sourcePath, WallpaperCrop crop, int target)
    {
        return BakeCroppedJpeg(sourcePath, crop, target, target);
    }

    // revealWholeImage lets the crop fall below WallpaperCrop.MinZoom so the whole source stays
    // visible instead of being cover-cropped. The revealed region no longer matches
    // targetWidth/targetHeight, so the result is the region contained inside that box: the bake
    // keeps real pixels only and the display side frames it (see ImageFit.DrawLetterboxed), rather
    // than burning bars into the JPEG where a cover-cropping profile grid would later cut through
    // them.
    public static BakedImage BakeCroppedJpeg(string sourcePath, WallpaperCrop crop, int targetWidth, int targetHeight,
        bool revealWholeImage = false)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        EnsureDecodable(sourceStream, MaxLocalDecodePixels);
        using var image = Image.Load(SingleFrame, sourceStream);
        var size = new Vector2(image.Width, image.Height);
        var aspect = (float)targetWidth / targetHeight;
        var minZoom = revealWholeImage
            ? WallpaperCrop.MinZoomToReveal(size, aspect)
            : WallpaperCrop.MinZoom;
        var clamped = crop.Clamped(size, aspect, minZoom);
        var (uv0, uv1) = clamped.ComputeUv(size, aspect, minZoom);
        var x = Math.Clamp((int)MathF.Round(uv0.X * image.Width), 0, Math.Max(0, image.Width - 1));
        var y = Math.Clamp((int)MathF.Round(uv0.Y * image.Height), 0, Math.Max(0, image.Height - 1));
        var width = Math.Clamp((int)MathF.Round((uv1.X - uv0.X) * image.Width), 1, image.Width - x);
        var height = Math.Clamp((int)MathF.Round((uv1.Y - uv0.Y) * image.Height), 1, image.Height - y);
        var (containedWidth, containedHeight) = ContainSize(width, height, targetWidth, targetHeight);
        image.Mutate(context => context
            .Crop(new Rectangle(x, y, width, height))
            .Resize(containedWidth, containedHeight));
        return new BakedImage(EncodeJpegVerified(image), containedWidth, containedHeight);
    }

    public static (int Width, int Height) ContainSize(int width, int height, int targetWidth, int targetHeight)
    {
        if (width <= 0 || height <= 0)
        {
            return (targetWidth, targetHeight);
        }

        var scale = MathF.Min((float)targetWidth / width, (float)targetHeight / height);
        var containedWidth = Math.Clamp((int)MathF.Round(width * scale), 1, targetWidth);
        var containedHeight = Math.Clamp((int)MathF.Round(height * scale), 1, targetHeight);
        return (containedWidth, containedHeight);
    }

    public static BakedImage BakeJpeg(string sourcePath, int maxDimension)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        EnsureDecodable(sourceStream, MaxLocalDecodePixels);
        using var image = Image.Load(SingleFrame, sourceStream);
        ScaleWithin(image, maxDimension);
        return new BakedImage(EncodeJpegVerified(image), image.Width, image.Height);
    }

    private static byte[] EncodeJpegVerified(Image image)
    {
        using var reference = image.CloneAs<Rgba32>();
        var length = checked(reference.Width * reference.Height * 4);
        var referencePixels = ArrayPool<byte>.Shared.Rent(length);
        var decodedPixels = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            reference.CopyPixelDataTo(referencePixels.AsSpan(0, length));
            var encoded = Array.Empty<byte>();
            for (var attempt = 1; attempt <= EncodeAttemptLimit; attempt++)
            {
                using var stream = new MemoryStream();
                image.SaveAsJpeg(stream, new JpegEncoder { Quality = JpegQuality });
                encoded = stream.ToArray();
                using var decodedStream = new MemoryStream(encoded);
                using var decoded = Image.Load<Rgba32>(SingleFrame, decodedStream);
                decoded.CopyPixelDataTo(decodedPixels.AsSpan(0, length));
                if (!HasEncodeCorruption(referencePixels.AsSpan(0, length), decodedPixels.AsSpan(0, length),
                        reference.Width, reference.Height))
                {
                    return encoded;
                }

                AepLog.Warning($"[Media] jpeg encode attempt {attempt} of {EncodeAttemptLimit} " +
                    "produced corrupt samples, re-encoding");
            }
            AepLog.Info("[Media] all encode attempts produced corruption, falling back to quality 91.");
            using var fallbackStream = new MemoryStream();
            image.SaveAsJpeg(fallbackStream, new JpegEncoder { Quality = JpegQualityFallback });

            return fallbackStream.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(referencePixels);
            ArrayPool<byte>.Shared.Return(decodedPixels);
        }
    }

    internal static bool HasEncodeCorruption(ReadOnlySpan<byte> sourceRgba, ReadOnlySpan<byte> decodedRgba,
        int width, int height)
    {
        var corruptSamples = 0;
        for (var cellY = 0; cellY < height; cellY += 2)
        {
            for (var cellX = 0; cellX < width; cellX += 2)
            {
                var sourceChromaBlueSum = 0f;
                var sourceChromaRedSum = 0f;
                var decodedChromaBlueSum = 0f;
                var decodedChromaRedSum = 0f;
                var sampleCount = 0;
                for (var offsetY = 0; offsetY < 2; offsetY++)
                {
                    var pixelY = cellY + offsetY;
                    if (pixelY >= height)
                    {
                        continue;
                    }

                    for (var offsetX = 0; offsetX < 2; offsetX++)
                    {
                        var pixelX = cellX + offsetX;
                        if (pixelX >= width)
                        {
                            continue;
                        }

                        var index = ((pixelY * width) + pixelX) * 4;
                        if (MathF.Abs(LumaOf(sourceRgba, index) - LumaOf(decodedRgba, index)) > LumaDeltaLimit)
                        {
                            corruptSamples++;
                        }

                        sourceChromaBlueSum += ChromaBlueOf(sourceRgba, index);
                        sourceChromaRedSum += ChromaRedOf(sourceRgba, index);
                        decodedChromaBlueSum += ChromaBlueOf(decodedRgba, index);
                        decodedChromaRedSum += ChromaRedOf(decodedRgba, index);
                        sampleCount++;
                    }
                }

                if (sampleCount > 0)
                {
                    var chromaBlueDelta = MathF.Abs(sourceChromaBlueSum - decodedChromaBlueSum) / sampleCount;
                    var chromaRedDelta = MathF.Abs(sourceChromaRedSum - decodedChromaRedSum) / sampleCount;
                    if (chromaBlueDelta > ChromaDeltaLimit || chromaRedDelta > ChromaDeltaLimit)
                    {
                        corruptSamples++;
                    }
                }

                if (corruptSamples >= CorruptSampleLimit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float LumaOf(ReadOnlySpan<byte> rgba, int index)
    {
        return (0.299f * rgba[index]) + (0.587f * rgba[index + 1]) + (0.114f * rgba[index + 2]);
    }

    private static float ChromaBlueOf(ReadOnlySpan<byte> rgba, int index)
    {
        return (-0.168736f * rgba[index]) - (0.331264f * rgba[index + 1]) + (0.5f * rgba[index + 2]);
    }

    private static float ChromaRedOf(ReadOnlySpan<byte> rgba, int index)
    {
        return (0.5f * rgba[index]) - (0.418688f * rgba[index + 1]) - (0.081312f * rgba[index + 2]);
    }

    private static (byte[] Pixels, int Length, int Width, int Height) DecodeRgba32Pooled(Stream stream, long maxPixels,
        int maxDimension)
    {
        using var image = LoadRgba32(stream, maxPixels, maxDimension, out var length);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        image.CopyPixelDataTo(buffer.AsSpan(0, length));
        return (buffer, length, image.Width, image.Height);
    }

    public static (byte[] Pixels, int Width, int Height) DecodeRgba32(byte[] bytes)
    {
        return DecodeRgba32(bytes, 0);
    }

    public static (byte[] Pixels, int Width, int Height) DecodeRgba32(byte[] bytes, int maxDimension)
    {
        using var stream = new MemoryStream(bytes);
        using var image = LoadRgba32(stream, MaxDecodePixels, maxDimension, out var length);
        var pixels = new byte[length];
        image.CopyPixelDataTo(pixels);
        return (pixels, image.Width, image.Height);
    }

    public static bool IsGif(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46
            && bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61;
    }

    public static (int Width, int Height) IdentifyDimensions(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var info = Image.Identify(stream);
        return (info.Width, info.Height);
    }

    public static async Task<AnimatedImage> DecodeAnimationAsync(ITextureProvider textures, byte[] bytes, string tag,
        CancellationToken token)
    {
        var (frames, width, height, delays) = await Task.Run(() => DecodeAnimationFrames(bytes), token)
            .ConfigureAwait(false);
        var wraps = new IDalamudTextureWrap[frames.Length];
        try
        {
            for (var index = 0; index < frames.Length; index++)
            {
                wraps[index] = await textures.CreateFromRawAsync(RawImageSpecification.Rgba32(width, height),
                    frames[index], $"{tag}#{index}", token).ConfigureAwait(false);
            }
        }
        catch
        {
            for (var index = 0; index < wraps.Length; index++)
            {
                wraps[index]?.Dispose();
            }

            throw;
        }

        return new AnimatedImage(wraps, delays);
    }

    private static (byte[][] Frames, int Width, int Height, float[] Delays) DecodeAnimationFrames(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        EnsureDecodable(stream, MaxDecodePixels);
        var info = Image.Identify(stream);
        stream.Position = 0;
        var pixelsPerFrame = Math.Max(1L, (long)info.Width * info.Height);
        var maxSourceFrames = (int)Math.Clamp(MaxAnimationSourcePixels / pixelsPerFrame, 1L,
            MaxAnimationSourceFrames);
        var options = new DecoderOptions { MaxFrames = (uint)maxSourceFrames };
        using var image = Image.Load<Rgba32>(options, stream);
        var rawDelays = new float[image.Frames.Count];
        for (var index = 0; index < rawDelays.Length; index++)
        {
            rawDelays[index] = image.Frames[index].Metadata.GetGifMetadata().FrameDelay / 100f;
        }

        ScaleWithin(image, MaxAnimationDimension);
        var (keptIndices, delays) = GifFramePlan.Plan(rawDelays, image.Width, image.Height, MaxAnimationPixels);
        var frameLength = checked(image.Width * image.Height * 4);
        var frames = new byte[keptIndices.Length][];
        for (var index = 0; index < keptIndices.Length; index++)
        {
            var pixels = new byte[frameLength];
            image.Frames[keptIndices[index]].CopyPixelDataTo(pixels);
            frames[index] = pixels;
        }

        return (frames, image.Width, image.Height, delays);
    }

    public static Task<IDalamudTextureWrap> DecodeToTextureAsync(ITextureProvider textures, byte[] bytes,
        string tag, long maxPixels, CancellationToken token)
    {
        return DecodeToTextureAsync(textures, bytes, tag, maxPixels, 0, token);
    }

    public static async Task<IDalamudTextureWrap> DecodeToTextureAsync(ITextureProvider textures, byte[] bytes,
        string tag, long maxPixels, int maxDimension, CancellationToken token)
    {
        var (pixels, length, width, height) = await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes);
            return DecodeRgba32Pooled(stream, maxPixels, maxDimension);
        }, token).ConfigureAwait(false);

        try
        {
            return await textures.CreateFromRawAsync(RawImageSpecification.Rgba32(width, height),
                pixels.AsMemory(0, length), tag, token).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }
}
