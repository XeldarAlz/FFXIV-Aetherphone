using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class BadgeStrip
{
    private const float GlyphFraction = 0.72f;
    private const float GapFraction = 0.34f;
    private const uint FullTint = 0xFFFFFFFFu;

    public static float Reserve(string[]? badgeIds, in TextStyle textStyle, int maxBadges = 2)
    {
        var shown = Shown(badgeIds, maxBadges);
        if (shown == 0)
        {
            return 0f;
        }

        return shown * Typography.Measure("A", textStyle).Y * (GlyphFraction + GapFraction);
    }

    public static float Draw(ImDrawListPtr drawList, string id, string[]? badgeIds, BadgeCatalogStore catalog,
        RemoteImageCache images, float left, float y, in TextStyle textStyle, bool light, int maxBadges = 2)
    {
        var shown = Shown(badgeIds, maxBadges);
        if (shown == 0)
        {
            return 0f;
        }

        var lineHeight = Typography.Measure("A", textStyle).Y;
        var glyphHeight = lineHeight * GlyphFraction;
        var gap = lineHeight * GapFraction;
        var centerY = y + lineHeight * 0.5f;
        var cursor = left;

        for (var index = 0; index < shown; index++)
        {
            cursor += gap;
            var center = new Vector2(cursor + glyphHeight * 0.5f, centerY);
            var badge = catalog.Find(badgeIds![index]);
            DrawOne(drawList, center, badge, images, light, glyphHeight);
            if (badge is not null)
            {
                var half = glyphHeight * 0.5f;
                HoverTooltip.Show(id + ".communitybadge." + index,
                    new Rect(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half)),
                    badge.Name);
            }

            cursor += glyphHeight;
        }

        return cursor - left;
    }

    public static void DrawOne(ImDrawListPtr drawList, Vector2 center, BadgeStyle? badge, RemoteImageCache images,
        bool light, float size)
    {
        if (badge is null)
        {
            var placeholder = light ? new Vector4(0.10f, 0.10f, 0.11f, 0.25f) : new Vector4(0.97f, 0.97f, 0.98f, 0.25f);
            drawList.AddCircleFilled(center, size * 0.22f, ImGui.GetColorU32(placeholder));
            return;
        }

        if (badge.AssetUrl.Length > 0)
        {
            var texture = images.Get(badge.AssetUrl);
            if (texture is not null)
            {
                var half = size * 0.5f;
                drawList.AddImage(texture.Handle, center - new Vector2(half, half), center + new Vector2(half, half),
                    Vector2.Zero, Vector2.One, FullTint);
                return;
            }
        }

        ProgressRing.CenterIconRamp(drawList, center, badge.Glyph, badge.Colors, light, size);
    }

    private static int Shown(string[]? badgeIds, int maxBadges)
    {
        if (badgeIds is null)
        {
            return 0;
        }

        return Math.Min(badgeIds.Length, maxBadges);
    }
}
