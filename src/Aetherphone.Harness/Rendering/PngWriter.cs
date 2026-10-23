using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Aetherphone.Harness.Rendering;

internal static class PngWriter
{
    private static readonly PngEncoder FastEncoder = new() { CompressionLevel = PngCompressionLevel.Level1 };
    private static readonly PngEncoder DefaultEncoder = new();

    public static void Write(string path, byte[] rgba, int width, int height) => File.WriteAllBytes(path, Encode(rgba, width, height, false));

    public static byte[] Encode(byte[] rgba, int width, int height, bool fast)
    {
        using var image = Image.LoadPixelData<Rgba32>(rgba, width, height);
        using var stream = new MemoryStream();
        image.Save(stream, fast ? FastEncoder : DefaultEncoder);
        return stream.ToArray();
    }
}
