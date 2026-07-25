using System.Buffers;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
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

    public static BakedImage BakeSquareJpeg(string sourcePath, WallpaperCrop crop, int target)
    {
        return BakeCroppedJpeg(sourcePath, crop, target, target);
    }

    public static BakedImage BakeCroppedJpeg(string sourcePath, WallpaperCrop crop, int targetWidth, int targetHeight)
    {
        using var image = Image.Load(sourcePath);
        var size = new Vector2(image.Width, image.Height);
        var aspect = (float)targetWidth / targetHeight;
        var clamped = crop.Clamped(size, aspect);
        var (uv0, uv1) = clamped.ComputeUv(size, aspect);
        var x = Math.Clamp((int)MathF.Round(uv0.X * image.Width), 0, Math.Max(0, image.Width - 1));
        var y = Math.Clamp((int)MathF.Round(uv0.Y * image.Height), 0, Math.Max(0, image.Height - 1));
        var width = Math.Clamp((int)MathF.Round((uv1.X - uv0.X) * image.Width), 1, image.Width - x);
        var height = Math.Clamp((int)MathF.Round((uv1.Y - uv0.Y) * image.Height), 1, image.Height - y);
        image.Mutate(context => context.Crop(new Rectangle(x, y, width, height)).Resize(targetWidth, targetHeight));
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = JpegQuality });
        return new BakedImage(stream.ToArray(), targetWidth, targetHeight);
    }

    public static BakedImage BakeJpeg(string sourcePath, int maxDimension)
    {
        using var image = Image.Load(sourcePath);
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

    public static async Task<IDalamudTextureWrap> DecodeToTextureAsync(ITextureProvider textures, byte[] bytes,
        string tag, CancellationToken token)
    {
        var (pixels, length, width, height) = await Task.Run(() =>
        {
            using var image = Image.Load<Rgba32>(bytes);
            var length = image.Width * image.Height * 4;
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            image.CopyPixelDataTo(buffer.AsSpan(0, length));
            return (buffer, length, image.Width, image.Height);
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
