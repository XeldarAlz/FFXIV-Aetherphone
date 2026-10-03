using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Venues;

internal readonly record struct VenueArt(RemoteImageCache Images, ArtworkCache Artwork);

internal static class VenueImage
{
    private const float FallbackLogoShare = 0.42f;
    private const float FallbackLogoMax = 88f;
    private static readonly Vector4 InitialInk = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 LogoRim = new(1f, 1f, 1f, 0.22f);

    public static void Cover(ImDrawListPtr drawList, Rect rect, float rounding, VenueEvent venue, string initial,
        in VenueArt art)
    {
        var banner = art.Images.Get(venue.BannerUrl);
        if (banner is not null)
        {
            Paint(drawList, rect, rounding, banner);
            return;
        }

        PaintGradient(drawList, rect, rounding, venue, art);
        var logo = art.Images.Get(venue.LogoUrl);
        var scale = UiScale.Current;
        var side = MathF.Min(MathF.Min(rect.Width, rect.Height) * FallbackLogoShare, FallbackLogoMax * scale);
        var min = rect.Center - new Vector2(side * 0.5f, side * 0.5f);
        var max = min + new Vector2(side, side);
        if (logo is null)
        {
            Typography.DrawCentered(drawList, rect.Center, initial, InitialInk, side / (40f * scale),
                FontWeight.Bold);
            return;
        }

        var logoRounding = side * 0.24f;
        var (uv0, uv1) = ImageFit.CoverSquare(logo.Size);
        Squircle.FillImage(drawList, min, max, logoRounding, logo.Handle, 0xFFFFFFFFu, uv0, uv1);
        Squircle.Stroke(drawList, min, max, logoRounding, ImGui.GetColorU32(LogoRim), 1f * scale);
    }

    public static void Logo(ImDrawListPtr drawList, Rect rect, float rounding, VenueEvent venue, string initial,
        in VenueArt art)
    {
        var logo = art.Images.Get(venue.LogoUrl);
        if (logo is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(logo.Size);
            Squircle.FillImage(drawList, rect.Min, rect.Max, rounding, logo.Handle, 0xFFFFFFFFu, uv0, uv1);
            return;
        }

        PaintGradient(drawList, rect, rounding, venue, art);
        Typography.DrawCentered(drawList, rect.Center, initial, InitialInk, rect.Height / (40f * UiScale.Current),
            FontWeight.Bold);
    }

    private static void Paint(ImDrawListPtr drawList, Rect rect, float rounding, IDalamudTextureWrap texture)
    {
        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
        if (rounding <= 0f)
        {
            drawList.AddImage(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu);
            return;
        }

        Squircle.FillImage(drawList, rect.Min, rect.Max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
    }

    private static void PaintGradient(ImDrawListPtr drawList, Rect rect, float rounding, VenueEvent venue,
        in VenueArt art)
    {
        var handle = art.Artwork.HandleForName(venue.Title);
        if (rounding <= 0f)
        {
            drawList.AddImage(handle, rect.Min, rect.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu);
            return;
        }

        Squircle.FillImage(drawList, rect.Min, rect.Max, rounding, handle, 0xFFFFFFFFu);
    }
}
