using Aetherphone.Core;
using Aetherphone.Core.Net;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal static class VenueImage
{
    public static void Draw(ImDrawListPtr drawList, Rect rect, VenueEvent venue, MediaCache media, HttpService http,
        ArtworkCache art, float rounding)
    {
        var url = venue.BannerUrl ?? venue.IconUrl;
        if (!string.IsNullOrEmpty(url))
        {
            var result = media.GetOrRequest(url, token => http.GetBytesAsync(new Uri(url), token));
            if (result.Texture is { } texture)
            {
                var (uv0, uv1) = ImageFit.Cover(texture.Width, texture.Height, rect.Width, rect.Height);
                drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding);
                return;
            }
        }

        drawList.AddImageRounded(art.HandleForName(venue.Title), rect.Min, rect.Max, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, rounding);
        var initial = venue.Title.Length > 0 ? venue.Title.Substring(0, 1).ToUpperInvariant() : "?";
        var glyphScale = rect.Height / 64f;
        Typography.DrawCentered(rect.Center, initial, new Vector4(1f, 1f, 1f, 0.92f), glyphScale, FontWeight.SemiBold);
    }
}
