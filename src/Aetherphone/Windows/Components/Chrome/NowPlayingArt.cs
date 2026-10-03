using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class NowPlayingArt
{
    private static RemoteImageCache? images;

    public static void Configure(RemoteImageCache cache) => images = cache;

    public static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, string url, string seed,
        float alpha)
    {
        if (TryDrawDisc(drawList, center, radius, url, alpha))
        {
            return;
        }

        ArtGradient.DrawDisc(drawList, center, radius, ArtGradient.FromName(seed), alpha);
    }

    public static bool TryDrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, string url, float alpha)
    {
        var texture = Texture(url, radius * 2f);
        if (texture is null)
        {
            return false;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        var corner = new Vector2(radius, radius);
        drawList.AddImageRounded(texture.Handle, center - corner, center + corner, uv0, uv1, Tint(alpha), radius,
            ImDrawFlags.RoundCornersAll);
        return true;
    }

    public static bool TryDrawSquircle(ImDrawListPtr drawList, Vector2 min, float side, float radius, string url,
        float alpha)
    {
        var texture = Texture(url, side);
        if (texture is null)
        {
            return false;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        Squircle.FillImage(drawList, min, min + new Vector2(side, side), radius, texture.Handle, Tint(alpha), uv0,
            uv1);
        return true;
    }

    private static IDalamudTextureWrap? Texture(string url, float drawnPixels)
    {
        return images is null || string.IsNullOrEmpty(url) ? null : images.Sized(url, drawnPixels);
    }

    private static uint Tint(float alpha) =>
        ImGui.GetColorU32(Palette.WithAlpha(Vector4.One, Math.Clamp(alpha, 0f, 1f)));
}
