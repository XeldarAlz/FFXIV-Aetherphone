namespace Aetherphone.Core.Media;

internal readonly struct PixelImage
{
    public const int BytesPerPixel = 4;

    public readonly byte[] Pixels;
    public readonly int Width;
    public readonly int Height;

    public PixelImage(byte[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    public static PixelImage Empty => new(Array.Empty<byte>(), 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0 || Pixels.Length < Length;

    public int Length => Width * Height * BytesPerPixel;

    public Vector2 Size => new(Width, Height);

    public float Aspect => Height > 0 ? (float)Width / Height : 1f;
}
