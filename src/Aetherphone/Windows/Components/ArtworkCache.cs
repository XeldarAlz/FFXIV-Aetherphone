using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal sealed class ArtworkCache : IDisposable
{
    private const int Size = 256;
    private readonly ITextureProvider textures;
    private readonly Dictionary<int, IDalamudTextureWrap> cache = new();

    public ArtworkCache(ITextureProvider textures)
    {
        this.textures = textures;
    }

    public ImTextureID Handle(int seed)
    {
        if (!cache.TryGetValue(seed, out var wrap))
        {
            var pixels = Rasterize(ArtGradient.From(seed), Size);
            wrap = textures.CreateFromRaw(RawImageSpecification.Rgba32(Size, Size), pixels, $"Aetherphone.Art.{seed}");
            cache[seed] = wrap;
        }

        return wrap.Handle;
    }

    public ImTextureID HandleForName(string value) => Handle(ArtGradient.Seed(value));

    private static byte[] Rasterize(ArtGradient.Swatch swatch, int size)
    {
        var pixels = new byte[size * size * 4];
        var last = size > 1 ? size - 1 : 1;
        var glowCenter = new Vector2(size * 0.30f, size * 0.26f);
        var glowRadius = size * 0.85f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var diagonal = (x + y) / (2f * last);
                var color = Vector4.Lerp(swatch.Top, swatch.Bottom, diagonal);
                var deltaX = x - glowCenter.X;
                var deltaY = y - glowCenter.Y;
                var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                var glow = Math.Clamp(1f - distance / glowRadius, 0f, 1f);
                glow *= glow * 0.45f;
                color = Vector4.Lerp(color, swatch.Glow, glow);
                var index = (y * size + x) * 4;
                pixels[index] = ToByte(color.X);
                pixels[index + 1] = ToByte(color.Y);
                pixels[index + 2] = ToByte(color.Z);
                pixels[index + 3] = 255;
            }
        }

        return pixels;
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);

    public void Dispose()
    {
        foreach (var wrap in cache.Values)
        {
            wrap.Dispose();
        }

        cache.Clear();
    }
}
