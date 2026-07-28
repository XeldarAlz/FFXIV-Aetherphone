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

    /// Decodes <paramref name="bytes"/> and uploads the pixels as a texture.
    ///
    /// The pixel buffer is a dedicated array on purpose. Do not switch it to a
    /// pooled one: the shared array pool is process-wide, and the JPEG encoder
    /// takes its scratch buffers from the same pool, so a buffer handed back
    /// even slightly early can be written by this decode while an unrelated
    /// photo is being encoded out of it. That baked 16-byte slivers of one
    /// image into another image's upload.
    ///
    /// Recycling would only be safe given a signal that the pixels have been
    /// consumed, and the texture upload task completing is not that signal.
    /// Callers cache the decoded texture, so decodes are infrequent and the
    /// allocation is small next to the decode itself.
    public static async Task<IDalamudTextureWrap> DecodeToTextureAsync(ITextureProvider textures, byte[] bytes,
        string tag, CancellationToken token)
    {
        var (pixels, width, height) = await Task.Run(() =>
        {
            using var image = Image.Load<Rgba32>(bytes);
            var buffer = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(buffer);
            return (buffer, image.Width, image.Height);
        }, token).ConfigureAwait(false);

        return await textures.CreateFromRawAsync(RawImageSpecification.Rgba32(width, height), pixels, tag, token)
            .ConfigureAwait(false);
    }
}
