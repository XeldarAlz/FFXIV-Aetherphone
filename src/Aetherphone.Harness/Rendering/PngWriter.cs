using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aetherphone.Harness.Rendering;

internal static class PngWriter
{
    public static void Write(string path, byte[] rgba, int width, int height)
    {
        using var image = Image.LoadPixelData<Rgba32>(rgba, width, height);
        image.SaveAsPng(path);
    }
}
