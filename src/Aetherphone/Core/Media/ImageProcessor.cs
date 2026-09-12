using System.Buffers;
using System.Buffers.Binary;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
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
    private const JpegChromaSubsampling JpegSubsampling = JpegChromaSubsampling.Ratio420;

    public const long MaxDecodePixels = 4096L * 4096L;
    public const long MaxLocalDecodePixels = 8192L * 8192L;
    public const int MaxAnimationDimension = 480;
    public const long MaxAnimationPixels = 9_000_000L;
    private const long MaxAnimationSourcePixels = 40_000_000L;
    private const int MaxAnimationSourceFrames = 300;
    internal static readonly DecoderOptions SingleFrame = new() { MaxFrames = 1 };
    private static ReadOnlySpan<byte> PngSignature => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static void EnsureDecodable(Stream stream, long maxPixels)
    {
        var info = Image.Identify(stream);
        stream.Position = 0;
        EnsureWithin(info.Width, info.Height, maxPixels);
    }

    private static void EnsureWithin(int width, int height, long maxPixels)
    {
        if (width <= 0 || height <= 0 || (long)width * height > maxPixels)
        {
            throw new InvalidImageContentException(
                $"Image dimensions {width}x{height} exceed the {maxPixels} pixel limit.");
        }
    }

    private static (int Width, int Height) PngDimensions(ReadOnlySpan<byte> bytes)
    {
        const int widthOffset = 16;
        const int heightOffset = 20;
        if (bytes.Length < heightOffset + 4 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidImageContentException("PNG data has no IHDR chunk.");
        }

        return (BinaryPrimitives.ReadInt32BigEndian(bytes[widthOffset..]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[heightOffset..]));
    }

    private static Image<Rgba32> LoadRgba32(Stream stream, long maxPixels, int maxDimension, out int length)
    {
        EnsureDecodable(stream, maxPixels);
        var image = Image.Load<Rgba32>(SingleFrame, stream);
        image.Mutate(context => context.AutoOrient());
        ScaleWithin(image, maxDimension);
        length = checked(image.Width * image.Height * 4);
        return image;
    }

    public static PixelImage DecodeLocalRgba32(string path, int maxDimension)
    {
        using var stream = File.OpenRead(path);
        using var image = LoadRgba32(stream, MaxLocalDecodePixels, maxDimension, out var length);
        var pixels = new byte[length];
        image.CopyPixelDataTo(pixels);
        return new PixelImage(pixels, image.Width, image.Height);
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
        using var image = Image.Load<Rgba32>(SingleFrame, sourceStream);
        image.Mutate(context => context.AutoOrient());
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
        return new BakedImage(EncodeJpeg(image), containedWidth, containedHeight);
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
        using var image = Image.Load<Rgba32>(SingleFrame, sourceStream);
        image.Mutate(context => context.AutoOrient());
        ScaleWithin(image, maxDimension);
        return new BakedImage(EncodeJpeg(image), image.Width, image.Height);
    }

    private static byte[] EncodeJpeg(Image<Rgba32> image)
    {
        var length = checked(image.Width * image.Height * 4);
        var pixels = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            image.CopyPixelDataTo(pixels.AsSpan(0, length));
            return ScalarJpegEncoder.Encode(pixels.AsSpan(0, length), image.Width, image.Height, JpegQuality,
                JpegSubsampling);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
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

    public static AnimationKind AnimationKindOf(ReadOnlySpan<byte> bytes)
    {
        if (IsGif(bytes))
        {
            return AnimationKind.Gif;
        }

        if (IsAnimatedWebp(bytes))
        {
            return AnimationKind.Webp;
        }

        if (IsAnimatedPng(bytes))
        {
            return AnimationKind.Png;
        }

        return AnimationKind.None;
    }

    private static bool IsAnimatedWebp(ReadOnlySpan<byte> bytes)
    {
        const int flagsOffset = 20;
        const byte animationFlag = 0x02;
        return bytes.Length > flagsOffset
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes[8..16].SequenceEqual("WEBPVP8X"u8)
            && (bytes[flagsOffset] & animationFlag) != 0;
    }

    private static bool IsAnimatedPng(ReadOnlySpan<byte> bytes)
    {
        const int chunkHeaderLength = 8;
        const int chunkCrcLength = 4;
        if (bytes.Length < PngSignature.Length || !bytes[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return false;
        }

        long offset = PngSignature.Length;
        while (offset + chunkHeaderLength <= bytes.Length)
        {
            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(bytes[(int)offset..]);
            var type = bytes.Slice((int)offset + 4, 4);
            if (type.SequenceEqual("acTL"u8))
            {
                return true;
            }

            if (type.SequenceEqual("IDAT"u8))
            {
                return false;
            }

            offset += chunkHeaderLength + dataLength + chunkCrcLength;
        }

        return false;
    }

    public static (int Width, int Height) IdentifyDimensions(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var info = Image.Identify(stream);
        return (info.Width, info.Height);
    }

    public static async Task<AnimatedImage> DecodeAnimationAsync(ITextureProvider textures, byte[] bytes,
        AnimationKind kind, string tag, int maxDimension, CancellationToken token)
    {
        var (frames, width, height, delays) = await Task
            .Run(() => DecodeAnimationFrames(bytes, kind, maxDimension), token)
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

    internal static (byte[][] Frames, int Width, int Height, float[] Delays) DecodeAnimationFrames(byte[] bytes,
        AnimationKind kind, int maxDimension)
    {
        var (sourceWidth, sourceHeight) = kind == AnimationKind.Png ? PngDimensions(bytes) : IdentifyDimensions(bytes);
        EnsureWithin(sourceWidth, sourceHeight, MaxDecodePixels);
        using var stream = new MemoryStream(bytes);
        var pixelsPerFrame = Math.Max(1L, (long)sourceWidth * sourceHeight);
        var maxSourceFrames = (int)Math.Clamp(MaxAnimationSourcePixels / pixelsPerFrame, 1L,
            MaxAnimationSourceFrames);
        var options = new DecoderOptions { MaxFrames = (uint)maxSourceFrames };
        using var image = Image.Load<Rgba32>(options, stream);
        var rawDelays = new float[image.Frames.Count];
        for (var index = 0; index < rawDelays.Length; index++)
        {
            rawDelays[index] = FrameDelaySeconds(image.Frames[index], kind);
        }

        ScaleWithin(image, AnimationDimension(maxDimension));
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

    private static int AnimationDimension(int maxDimension)
    {
        return maxDimension <= 0 ? MaxAnimationDimension : Math.Min(maxDimension, MaxAnimationDimension);
    }

    private static float FrameDelaySeconds(ImageFrame<Rgba32> frame, AnimationKind kind)
    {
        switch (kind)
        {
            case AnimationKind.Webp:
                return frame.Metadata.GetWebpMetadata().FrameDelay / 1000f;
            case AnimationKind.Png:
                var seconds = frame.Metadata.GetPngMetadata().FrameDelay.ToDouble();
                return double.IsFinite(seconds) ? (float)seconds : 0f;
            default:
                return frame.Metadata.GetGifMetadata().FrameDelay / 100f;
        }
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
