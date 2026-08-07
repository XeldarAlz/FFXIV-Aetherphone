using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UserName
{
    private const float GlyphFraction = 0.72f;
    private const float GapFraction = 0.34f;

    private static BadgeCatalogStore? communityCatalog;
    private static RemoteImageCache? communityImages;

    public static void Configure(BadgeCatalogStore catalog, RemoteImageCache images)
    {
        communityCatalog = catalog;
        communityImages = images;
    }

    public static void Reset()
    {
        communityCatalog = null;
        communityImages = null;
    }

    public static float Reserve(int badges, in TextStyle style, int maxBadges = 1)
    {
        var shown = Math.Min(RoleBadges.Count(badges), maxBadges);
        if (shown == 0)
        {
            return 0f;
        }

        var lineHeight = LineHeight(style);
        return shown * lineHeight * (GlyphFraction + GapFraction);
    }

    public static float Reserve(int badges, string[]? badgeIds, in TextStyle style, int maxBadges = 1)
    {
        return CatalogServes(badgeIds)
            ? BadgeStrip.Reserve(badgeIds, style, maxBadges)
            : Reserve(badges, style, maxBadges);
    }

    private static bool CatalogServes(string[]? badgeIds)
    {
        return communityCatalog is not null && communityImages is not null && badgeIds is { Length: > 0 };
    }

    public static float Draw(string id, string name, int badges, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 nameInk, bool hovering, bool light, int maxBadges = 1) =>
        Draw(ImGui.GetWindowDrawList(), id, name, badges, boxLeft, y, maxWidth, style, nameInk, hovering, light, maxBadges);

    public static float Draw(string id, string name, int badges, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 nameInk, bool hovering, PhoneTheme theme, int maxBadges = 1) =>
        Draw(ImGui.GetWindowDrawList(), id, name, badges, boxLeft, y, maxWidth, style, nameInk, hovering,
            RoleInk.IsLight(theme), maxBadges);

    public static float Draw(string id, string name, int badges, string[]? badgeIds, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, bool light, int maxBadges = 1) =>
        Draw(ImGui.GetWindowDrawList(), id, name, badges, badgeIds, boxLeft, y, maxWidth, style, nameInk, hovering,
            light, maxBadges);

    public static float Draw(string id, string name, int badges, string[]? badgeIds, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, PhoneTheme theme, int maxBadges = 1) =>
        Draw(ImGui.GetWindowDrawList(), id, name, badges, badgeIds, boxLeft, y, maxWidth, style, nameInk, hovering,
            RoleInk.IsLight(theme), maxBadges);

    public static float Draw(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, bool light, int maxBadges = 1) =>
        Draw(drawList, id, name, badges, null, boxLeft, y, maxWidth, style, nameInk, hovering, light, maxBadges);

    public static float Draw(ImDrawListPtr drawList, string id, string name, int badges, string[]? badgeIds,
        float boxLeft, float y, float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, bool light,
        int maxBadges = 1)
    {
        var fromCatalog = CatalogServes(badgeIds);
        var shown = fromCatalog ? 0 : Math.Min(RoleBadges.Count(badges), maxBadges);
        var lineHeight = LineHeight(style);
        var ink = nameInk;
        var effect = default(TextEffect);
        if (fromCatalog)
        {
            var community = communityCatalog!.Find(badgeIds![0]);
            if (community is not null)
            {
                ink = RoleInk.For(community.Colors[0], light);
                effect = NameEffects.For(community, light);
            }
        }
        else if (shown > 0)
        {
            var top = RoleBadges.Top(badges);
            if (top.HasValue)
            {
                ink = RoleInk.For(top.Value.Kind, light);
                effect = NameEffects.For(top.Value.Kind, light);
            }
        }

        var reserve = Reserve(badges, badgeIds, style, maxBadges);
        var textWidth = MathF.Max(1f, maxWidth - reserve);
        var drawn = Marquee.DrawLeft(drawList, id, name, boxLeft, y, textWidth, style, ink, hovering, effect);

        if (fromCatalog)
        {
            BadgeStrip.Draw(drawList, id, badgeIds, communityCatalog!, communityImages!, boxLeft + drawn, y, style,
                light, maxBadges);
            return drawn + reserve;
        }

        var glyphHeight = lineHeight * GlyphFraction;
        var gap = lineHeight * GapFraction;
        var centerY = y + lineHeight * 0.5f;
        var cursor = boxLeft + drawn + gap;

        for (var index = 0; index < shown; index++)
        {
            var badge = RoleBadges.At(badges, index);
            var center = new Vector2(cursor + glyphHeight * 0.5f, centerY);
            ProgressRing.CenterIcon(drawList, center, badge.Glyph, RoleInk.For(badge.Kind, light), glyphHeight);

            var half = glyphHeight * 0.5f;
            HoverTooltip.Show(id + ".badge." + index,
                new Rect(new Vector2(center.X - half, center.Y - half),
                    new Vector2(center.X + half, center.Y + half)),
                Loc.T(badge.Tooltip));

            cursor += glyphHeight + gap;
        }

        return drawn + reserve;
    }

    public static float Draw(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, PhoneTheme theme, int maxBadges = 1) =>
        Draw(drawList, id, name, badges, boxLeft, y, maxWidth, style, nameInk, hovering, RoleInk.IsLight(theme), maxBadges);

    public static float Draw(ImDrawListPtr drawList, string id, string name, int badges, string[]? badgeIds,
        float boxLeft, float y, float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, PhoneTheme theme,
        int maxBadges = 1) =>
        Draw(drawList, id, name, badges, badgeIds, boxLeft, y, maxWidth, style, nameInk, hovering,
            RoleInk.IsLight(theme), maxBadges);

    public static float DrawAuto(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool light, int maxBadges = 1) =>
        DrawAuto(drawList, id, name, badges, null, boxLeft, y, maxWidth, style, nameInk, light, maxBadges);

    public static float DrawAuto(ImDrawListPtr drawList, string id, string name, int badges, string[]? badgeIds,
        float boxLeft, float y, float maxWidth, in TextStyle style, Vector4 nameInk, bool light, int maxBadges = 1)
    {
        var size = Typography.Measure(name, style);
        var hovering = UiInteract.Hover(new Vector2(boxLeft, y),
            new Vector2(boxLeft + MathF.Min(size.X, maxWidth), y + size.Y));
        return Draw(drawList, id, name, badges, badgeIds, boxLeft, y, maxWidth, style, nameInk, hovering, light,
            maxBadges);
    }

    public static float DrawAuto(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, PhoneTheme theme, int maxBadges = 1) =>
        DrawAuto(drawList, id, name, badges, null, boxLeft, y, maxWidth, style, nameInk, RoleInk.IsLight(theme),
            maxBadges);

    public static float DrawAuto(ImDrawListPtr drawList, string id, string name, int badges, string[]? badgeIds,
        float boxLeft, float y, float maxWidth, in TextStyle style, Vector4 nameInk, PhoneTheme theme,
        int maxBadges = 1) =>
        DrawAuto(drawList, id, name, badges, badgeIds, boxLeft, y, maxWidth, style, nameInk, RoleInk.IsLight(theme),
            maxBadges);

    private static float LineHeight(in TextStyle style)
    {
        return Typography.Measure("A", style).Y;
    }
}
