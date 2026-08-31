using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherphone.Core.Media;

internal readonly struct BakedImage
{
    public readonly byte[] Bytes;
    public readonly int Width;
    public readonly int Height;
    public readonly string ContentType;

    public BakedImage(byte[] bytes, int width, int height, string contentType)
    {
        Bytes = bytes;
        Width = width;
        Height = height;
        ContentType = contentType;
    }
}

internal static class ImageProcessor
{
    public const string JpegContentType = "image/jpeg";
    public const string PngContentType = "image/png";

    private const int JpegQuality = 88;
    private const int JpegAttemptLimit = 3;
    private const int PngAttemptLimit = 2;
    private const float LumaDeltaLimit = 80f;
    private const float ChromaDeltaLimit = 64f;
    private const int CorruptSampleLimit = 3;
    private const int SpeckleChannelDeltaLimit = 96;
    private const int FlatSourceRangeLimit = 24;
    private const int FlatWindowRadius = 2;
    private const int ServerMaxImageBytes = 8 * 1024 * 1024;

    private static readonly string ProcessorName = ReadProcessorName();

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

    public static BakedImage BakeSquare(string sourcePath, WallpaperCrop crop, int target)
    {
        return BakeCropped(sourcePath, crop, target, target);
    }

    // revealWholeImage lets the crop fall below WallpaperCrop.MinZoom so the whole source stays
    // visible instead of being cover-cropped. The revealed region no longer matches
    // targetWidth/targetHeight, so the result is the region contained inside that box: the bake
    // keeps real pixels only and the display side frames it (see ImageFit.DrawLetterboxed), rather
    // than burning bars into the JPEG where a cover-cropping profile grid would later cut through
    // them.
    public static BakedImage BakeCropped(string sourcePath, WallpaperCrop crop, int targetWidth, int targetHeight,
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
        return EncodeVerified(image);
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

    public static BakedImage Bake(string sourcePath, int maxDimension)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        EnsureDecodable(sourceStream, MaxLocalDecodePixels);
        using var image = Image.Load(SingleFrame, sourceStream);
        ScaleWithin(image, maxDimension);
        return EncodeVerified(image);
    }

    private static BakedImage EncodeVerified(Image image)
    {
        using var normalized = PixelsOnlyRgb24(image);
        var width = normalized.Width;
        var height = normalized.Height;
        var length = checked(width * height * 4);
        var referencePixels = ArrayPool<byte>.Shared.Rent(length);
        var decodedPixels = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            using (var reference = normalized.CloneAs<Rgba32>())
            {
                reference.CopyPixelDataTo(referencePixels.AsSpan(0, length));
            }

            for (var attempt = 1; attempt <= JpegAttemptLimit; attempt++)
            {
                byte[] encoded;
                if (attempt < JpegAttemptLimit)
                {
                    encoded = EncodeJpegBytes(normalized);
                }
                else
                {
                    using var rebuilt = normalized.Clone();
                    encoded = EncodeJpegBytes(rebuilt);
                }

                if (DecodesCleanly(encoded, referencePixels, decodedPixels, length, width, height))
                {
                    return new BakedImage(encoded, width, height, JpegContentType);
                }

                AepLog.Warning($"[Media] jpeg bake attempt {attempt} of {JpegAttemptLimit} failed verification " +
                    $"({width}x{height}, cpu: {ProcessorName})");
            }

            for (var attempt = 1; attempt <= PngAttemptLimit; attempt++)
            {
                var encoded = EncodePngBytes(normalized);
                if (PngRoundTripMatches(encoded, referencePixels, decodedPixels, length))
                {
                    if (encoded.Length <= ServerMaxImageBytes)
                    {
                        AepLog.Warning($"[Media] shipping a verified lossless png bake of {encoded.Length} bytes " +
                            $"after jpeg verification failed (cpu: {ProcessorName})");
                        return new BakedImage(encoded, width, height, PngContentType);
                    }

                    AepLog.Warning($"[Media] png fallback of {encoded.Length} bytes exceeds the " +
                        $"{ServerMaxImageBytes} byte upload cap");
                    break;
                }

                AepLog.Warning($"[Media] png bake attempt {attempt} of {PngAttemptLimit} failed verification " +
                    $"(cpu: {ProcessorName})");
            }

            throw new InvalidImageContentException(
                $"Every encode attempt failed verification on this machine (cpu: {ProcessorName}); " +
                "refusing to upload a corrupt photo.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(referencePixels);
            ArrayPool<byte>.Shared.Return(decodedPixels);
        }
    }

    private static Image<Rgb24> PixelsOnlyRgb24(Image image)
    {
        using var converted = image.CloneAs<Rgb24>();
        var length = checked(converted.Width * converted.Height * 3);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            converted.CopyPixelDataTo(buffer.AsSpan(0, length));
            return Image.LoadPixelData<Rgb24>(buffer.AsSpan(0, length), converted.Width, converted.Height);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static byte[] EncodeJpegBytes(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = JpegQuality });
        return stream.ToArray();
    }

    private static byte[] EncodePngBytes(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
        return stream.ToArray();
    }

    private static bool DecodesCleanly(byte[] encoded, byte[] referencePixels, byte[] decodedPixels, int length,
        int width, int height)
    {
        using var decodedStream = new MemoryStream(encoded);
        using var decoded = Image.Load<Rgba32>(SingleFrame, decodedStream);
        decoded.CopyPixelDataTo(decodedPixels.AsSpan(0, length));
        return !HasEncodeCorruption(referencePixels.AsSpan(0, length), decodedPixels.AsSpan(0, length),
            width, height);
    }

    private static bool PngRoundTripMatches(byte[] encoded, byte[] referencePixels, byte[] decodedPixels, int length)
    {
        using var decodedStream = new MemoryStream(encoded);
        using var decoded = Image.Load<Rgba32>(SingleFrame, decodedStream);
        decoded.CopyPixelDataTo(decodedPixels.AsSpan(0, length));
        return referencePixels.AsSpan(0, length).SequenceEqual(decodedPixels.AsSpan(0, length));
    }

    private static string ReadProcessorName()
    {
        if (!X86Base.IsSupported)
        {
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        var (maxExtendedLeaf, _, _, _) = X86Base.CpuId(unchecked((int)0x80000000), 0);
        if ((uint)maxExtendedLeaf < 0x80000004)
        {
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        Span<int> registers = stackalloc int[12];
        for (var leafIndex = 0; leafIndex < 3; leafIndex++)
        {
            var (eax, ebx, ecx, edx) = X86Base.CpuId(unchecked((int)(0x80000002u + (uint)leafIndex)), 0);
            var registerIndex = leafIndex * 4;
            registers[registerIndex] = eax;
            registers[registerIndex + 1] = ebx;
            registers[registerIndex + 2] = ecx;
            registers[registerIndex + 3] = edx;
        }

        var brandBytes = MemoryMarshal.AsBytes(registers);
        var terminatorIndex = brandBytes.IndexOf((byte)0);
        var nameLength = terminatorIndex < 0 ? brandBytes.Length : terminatorIndex;
        return Encoding.ASCII.GetString(brandBytes[..nameLength]).Trim();
    }

    internal static bool HasEncodeCorruption(ReadOnlySpan<byte> sourceRgba, ReadOnlySpan<byte> decodedRgba,
        int width, int height)
    {
        return HasCorruptCells(sourceRgba, decodedRgba, width, height)
            || HasFlatAreaSpeckle(sourceRgba, decodedRgba, width, height);
    }

    internal static bool HasFlatAreaSpeckle(ReadOnlySpan<byte> sourceRgba, ReadOnlySpan<byte> decodedRgba,
        int width, int height)
    {
        for (var pixelY = 0; pixelY < height; pixelY++)
        {
            var rowStart = pixelY * width;
            for (var pixelX = 0; pixelX < width; pixelX++)
            {
                var index = (rowStart + pixelX) * 4;
                if (Math.Abs(sourceRgba[index] - decodedRgba[index]) <= SpeckleChannelDeltaLimit
                    && Math.Abs(sourceRgba[index + 1] - decodedRgba[index + 1]) <= SpeckleChannelDeltaLimit
                    && Math.Abs(sourceRgba[index + 2] - decodedRgba[index + 2]) <= SpeckleChannelDeltaLimit)
                {
                    continue;
                }

                if (SourceWindowIsFlat(sourceRgba, width, height, pixelX, pixelY))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SourceWindowIsFlat(ReadOnlySpan<byte> sourceRgba, int width, int height,
        int centerX, int centerY)
    {
        var startX = Math.Max(0, centerX - FlatWindowRadius);
        var endX = Math.Min(width - 1, centerX + FlatWindowRadius);
        var startY = Math.Max(0, centerY - FlatWindowRadius);
        var endY = Math.Min(height - 1, centerY + FlatWindowRadius);
        var minRed = 255;
        var maxRed = 0;
        var minGreen = 255;
        var maxGreen = 0;
        var minBlue = 255;
        var maxBlue = 0;
        for (var windowY = startY; windowY <= endY; windowY++)
        {
            var rowStart = windowY * width;
            for (var windowX = startX; windowX <= endX; windowX++)
            {
                var index = (rowStart + windowX) * 4;
                minRed = Math.Min(minRed, sourceRgba[index]);
                maxRed = Math.Max(maxRed, sourceRgba[index]);
                minGreen = Math.Min(minGreen, sourceRgba[index + 1]);
                maxGreen = Math.Max(maxGreen, sourceRgba[index + 1]);
                minBlue = Math.Min(minBlue, sourceRgba[index + 2]);
                maxBlue = Math.Max(maxBlue, sourceRgba[index + 2]);
            }
        }

        return maxRed - minRed < FlatSourceRangeLimit
            && maxGreen - minGreen < FlatSourceRangeLimit
            && maxBlue - minBlue < FlatSourceRangeLimit;
    }

    private static bool HasCorruptCells(ReadOnlySpan<byte> sourceRgba, ReadOnlySpan<byte> decodedRgba,
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

    public static string ImageContentTypeOf(ReadOnlySpan<byte> bytes)
    {
        if (IsGif(bytes))
        {
            return "image/gif";
        }

        if (bytes.Length >= PngSignature.Length && bytes[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return PngContentType;
        }

        return JpegContentType;
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
