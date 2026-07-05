using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

/// <summary>
/// Pure drawing for the Music surface: the gradient backdrop and album-art thumbnails (with a
/// name-derived gradient fallback). State, playback and texture fetching stay in <see cref="MusicApp"/>.
/// </summary>
internal static class MusicArt
{
    private static readonly Vector4 BackdropTop = new(0.05f, 0.20f, 0.11f, 1f);
    private static readonly Vector4 BackdropBottom = new(0.02f, 0.04f, 0.03f, 1f);
    private static readonly Vector4 BloomTop = new(0.13f, 0.75f, 0.36f, 0.22f);
    private static readonly Vector4 BloomBottom = new(0.08f, 0.38f, 0.19f, 0f);

    public static void Backdrop(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        var rounding = context.Theme.ScreenRounding * scale;
        var dl = ImGui.GetWindowDrawList();
        Squircle.FillVerticalGradient(dl, screen.Min, screen.Max, rounding, ImGui.GetColorU32(BackdropTop),
            ImGui.GetColorU32(BackdropBottom));
        Squircle.FillVerticalGradient(dl, screen.Min, screen.Max, rounding, ImGui.GetColorU32(BloomTop),
            ImGui.GetColorU32(BloomBottom));
    }

    public static void Thumb(ImDrawListPtr dl, Vector2 min, Vector2 max, IDalamudTextureWrap? texture,
        string fallbackName, float rounding)
    {
        if (texture is not null)
        {
            dl.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        var swatch = ArtGradient.FromName(fallbackName);
        dl.AddRectFilled(min, max, ImGui.GetColorU32(swatch.Bottom), rounding);
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, (min.Y + max.Y) * 0.5f), ImGui.GetColorU32(swatch.Top),
            ImGui.GetColorU32(swatch.Top), 0u, 0u);
    }
}
