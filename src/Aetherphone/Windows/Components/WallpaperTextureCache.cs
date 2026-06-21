using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal sealed class WallpaperTextureCache : IDisposable
{
    private const int TextureWidth = 360;

    private const int TextureHeight = 720;

    private readonly ITextureProvider textures;

    private readonly Dictionary<WallpaperStyle, IDalamudTextureWrap> cache = new();

    public WallpaperTextureCache(ITextureProvider textures)
    {
        this.textures = textures;
    }

    public ImTextureID Handle(WallpaperStyle style)
    {
        if (!cache.TryGetValue(style, out var wrap))
        {
            var pixels = WallpaperPainter.Rasterize(style, TextureWidth, TextureHeight);
            wrap = textures.CreateFromRaw(RawImageSpecification.Rgba32(TextureWidth, TextureHeight), pixels, $"Aetherphone.Wallpaper.{style}");
            cache[style] = wrap;
        }

        return wrap.Handle;
    }

    public void Dispose()
    {
        foreach (var wrap in cache.Values)
        {
            wrap.Dispose();
        }

        cache.Clear();
    }
}
