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

    public const long MaxDecodePixels = 4096L * 4096L;
    public const long MaxLocalDecodePixels = 8192L * 8192L;
    internal static readonly DecoderOptions SingleFrame = new() { MaxFrames = 1 };

    // The gap around a contain-fit photo (see BakeCroppedJpeg) - only Portrait ever reveals below
    // a cover crop (see the aspects[index] == PostAspect.Portrait checks in
    // AethergramStore.CreateGram / VelvetStore.CreatePost), so this is the only case that ever
    // needs a fill. Matches ImageFit.LetterboxFill, the live-preview equivalent.
    private static readonly Rgba32 LetterboxColor = new(0, 0, 0, 255);

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

    private static Image<Rgba32> LoadRgba32(Stream stream, long maxPixels, out int length)
    {
        EnsureDecodable(stream, maxPixels);
        var image = Image.Load<Rgba32>(SingleFrame, stream);
        length = checked(image.Width * image.Height * 4);
        return image;
    }

    // Reads just the header, not the full pixel data - callers that need an image's pixel size
    // ahead of baking it (see AethergramStore.CreateGram, WallpaperCrop.MinZoomToReveal) don't
    // need a full decode just to compute a minZoom bound.
    public static Vector2 ReadSize(string sourcePath)
    {
        var info = Image.Identify(sourcePath);
        return new Vector2(info.Width, info.Height);
    }

    public static BakedImage BakeSquareJpeg(string sourcePath, WallpaperCrop crop, int target)
    {
        return BakeCroppedJpeg(sourcePath, crop, target, target);
    }

    // minZoom lets crop legitimately go below WallpaperCrop.MinZoom (see
    // WallpaperCrop.MinZoomToReveal) to reveal more of the source image than a plain cover crop
    // would. When that happens, the cropped region's own aspect no longer matches
    // targetWidth/targetHeight, so it is contain-fit onto a letterboxed canvas of exactly that
    // size instead of being stretched to fill it (which would distort the image).
    public static BakedImage BakeCroppedJpeg(string sourcePath, WallpaperCrop crop, int targetWidth, int targetHeight,
        float minZoom = WallpaperCrop.MinZoom)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        EnsureDecodable(sourceStream, MaxLocalDecodePixels);
        using var image = Image.Load(SingleFrame, sourceStream);
        var size = new Vector2(image.Width, image.Height);
        var aspect = (float)targetWidth / targetHeight;
        var clamped = crop.Clamped(size, aspect, minZoom);
        var (uv0, uv1) = clamped.ComputeUv(size, aspect);
        var x = Math.Clamp((int)MathF.Round(uv0.X * image.Width), 0, Math.Max(0, image.Width - 1));
        var y = Math.Clamp((int)MathF.Round(uv0.Y * image.Height), 0, Math.Max(0, image.Height - 1));
        var width = Math.Clamp((int)MathF.Round((uv1.X - uv0.X) * image.Width), 1, image.Width - x);
        var height = Math.Clamp((int)MathF.Round((uv1.Y - uv0.Y) * image.Height), 1, image.Height - y);
        image.Mutate(context => context.Crop(new Rectangle(x, y, width, height)));

        var (containedWidth, containedHeight) = ContainSize(width, height, targetWidth, targetHeight);
        using var stream = new MemoryStream();
        if (containedWidth == targetWidth && containedHeight == targetHeight)
        {
            // Cropped region already matches the target aspect (today's ordinary cover-crop
            // case) - a plain resize, no letterbox canvas needed.
            image.Mutate(context => context.Resize(targetWidth, targetHeight));
            image.SaveAsJpeg(stream, new JpegEncoder { Quality = JpegQuality });
            return new BakedImage(stream.ToArray(), targetWidth, targetHeight);
        }

        image.Mutate(context => context.Resize(containedWidth, containedHeight));
        using var canvas = new Image<Rgba32>(targetWidth, targetHeight, LetterboxColor);
        var pasteX = (targetWidth - containedWidth) / 2;
        var pasteY = (targetHeight - containedHeight) / 2;
        canvas.Mutate(context => context.DrawImage(image, new Point(pasteX, pasteY), 1f));
        canvas.SaveAsJpeg(stream, new JpegEncoder { Quality = JpegQuality });
        return new BakedImage(stream.ToArray(), targetWidth, targetHeight);
    }

    private static (int Width, int Height) ContainSize(int width, int height, int targetWidth, int targetHeight)
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
        var width = image.Width;
        var height = image.Height;
        if (width > maxDimension || height > maxDimension)
        {
            var factor = MathF.Min((float)maxDimension / width, (float)maxDimension / height);
            width = Math.Max(1, (int)MathF.Round(width * factor));
            height = Math.Max(1, (int)MathF.Round(height * factor));
            image.Mutate(context => context.Resize(width, height));
        }

        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = JpegQuality });
        return new BakedImage(stream.ToArray(), width, height);
    }

    private static (byte[] Pixels, int Length, int Width, int Height) DecodeRgba32Pooled(Stream stream, long maxPixels)
    {
        using var image = LoadRgba32(stream, maxPixels, out var length);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        image.CopyPixelDataTo(buffer.AsSpan(0, length));
        return (buffer, length, image.Width, image.Height);
    }

    public static (byte[] Pixels, int Width, int Height) DecodeRgba32(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var image = LoadRgba32(stream, MaxDecodePixels, out var length);
        var pixels = new byte[length];
        image.CopyPixelDataTo(pixels);
        return (pixels, image.Width, image.Height);
    }

    public static async Task<IDalamudTextureWrap> DecodeToTextureAsync(ITextureProvider textures, byte[] bytes,
        string tag, long maxPixels, CancellationToken token)
    {
        var (pixels, length, width, height) = await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes);
            return DecodeRgba32Pooled(stream, maxPixels);
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
