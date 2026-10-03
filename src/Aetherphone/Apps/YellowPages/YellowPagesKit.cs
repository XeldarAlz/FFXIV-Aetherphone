using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.YellowPages;

internal static class YellowPagesInk
{
    public static readonly SocialInk Shared = new(AppPalettes.YellowPages);
}

internal readonly record struct AdStatus(string Label, Vector4 Tint, bool Live);

internal static class YellowPagesKit
{
    public const float PillHeight = MediaOverlay.PillHeight;
    public const float GlassButtonRadius = MediaOverlay.GlassButtonRadius;

    public static readonly Vector4 OpenGreen = MediaOverlay.LiveGreen;
    public static readonly Vector4 AfterDarkPink = new(0.94f, 0.42f, 0.62f, 1f);
    public static readonly Vector4 WantedTint = AccentRing.Azure;
    public static readonly Vector4 White = MediaOverlay.White;
    public static readonly Vector4 OverlayFill = MediaOverlay.Fill;
    public static readonly Vector4 OverlayHover = MediaOverlay.HoverFill;
    public static readonly Vector4 ScrimClear = MediaOverlay.ScrimClear;
    public static readonly Vector4 ScrimDeep = MediaOverlay.ScrimDeep;

    public static Vector4 AccentOf(AdDto ad) => AdAccents.For(ad.Accent);

    public static AdStatus StatusOf(AdDto ad, long nowUnix)
    {
        if (ad.Archetype == AdArchetypes.Place)
        {
            var state = AdText.OpenState(ad, nowUnix);
            if (state.IsOpen)
            {
                return new AdStatus(Loc.T(L.YellowPages.OpenNow), OpenGreen, true);
            }

            if (state.NextOpeningUnix > 0)
            {
                return new AdStatus(
                    Loc.T(L.YellowPages.OpensAt,
                        $"{TimeText.FutureDayLabel(state.NextOpeningUnix)} {TimeText.Clock(state.NextOpeningUnix)}"),
                    Palette.WithAlpha(OpenGreen, 0.85f), false);
            }

            return new AdStatus(string.Empty, YellowPagesInk.Shared.MutedInk, false);
        }

        var accent = AccentOf(ad);
        if (ad.Archetype == AdArchetypes.Service)
        {
            return new AdStatus(
                AdCategories.IsLinkOnly(ad.Category) ? Loc.T(L.YellowPages.ModBadge) : AdText.PriceLine(ad),
                Palette.Lighten(accent, 0.22f), false);
        }

        return new AdStatus(ad.SlotsLine, Palette.Lighten(accent, 0.22f), false);
    }

    public static float PillWidth(string label, float scale, bool glyph, bool emphasis = false) =>
        MediaOverlay.PillWidth(label, scale, glyph, emphasis);

    public static float Pill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 fill, Vector4 ink,
        float scale, string glyph = "", bool emphasis = false, Vector4? stroke = null) =>
        MediaOverlay.Pill(drawList, topLeft, label, fill, ink, scale, glyph, emphasis, stroke);

    public static float LivePill(ImDrawListPtr drawList, Vector2 topLeft, string label, float scale) =>
        MediaOverlay.LivePill(drawList, topLeft, label, scale);

    public static void LiveDot(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale) =>
        MediaOverlay.LiveDot(drawList, center, color, scale);

    public static void Tile(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector4 accent, FontAwesomeIcon icon,
        float rounding, float glyphScale)
    {
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(accent, 0.12f), 0.92f)),
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Darken(accent, 0.22f), 0.92f)));
        Material.Sheen(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)),
            1f * UiScale.Current, 1.2f * UiScale.Current);
        AppSkin.Icon(drawList, (min + max) * 0.5f, IconGlyph.Of(icon), White, glyphScale);
    }

    public static void CoverFallback(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 accent,
        int category, float iconScale)
    {
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(accent, 0.05f), 0.55f)),
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Darken(accent, 0.45f), 0.70f)));
        drawList.PushClipRect(min, max, true);
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        var drift = Vector2.Lerp(new Vector2(min.X + width * 0.30f, min.Y + height * 0.45f),
            new Vector2(min.X + width * 0.70f, min.Y + height * 0.55f), Pulse.Wave(Pulse.Orbit));
        for (var ring = 3; ring >= 1; ring--)
        {
            drawList.AddCircleFilled(drift, height * 0.34f * ring,
                ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(accent, 0.35f), 0.045f)), 48);
        }

        drawList.PopClipRect();
        AppSkin.Icon(drawList, new Vector2(min.X + width * 0.5f, min.Y + height * 0.5f),
            IconGlyph.Of(AdCategories.Icon(category)), Palette.WithAlpha(White, 0.82f), iconScale);
    }

    public static void BottomScrim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float share) =>
        MediaOverlay.BottomScrim(drawList, min, max, share);

    public static bool GlassButton(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip, float scale,
        bool highlighted = false, Vector4? glyphInk = null) =>
        MediaOverlay.GlassButton(drawList, center, glyph, tooltip, scale,
            highlighted ? Palette.WithAlpha(YellowPagesInk.Shared.AccentLink, 0.9f) : null, glyphInk);

    public static string Monogram(string displayName, string handle)
    {
        var name = displayName.Length > 0 ? displayName : handle;
        return Initials.Of(name);
    }

    public static void PresenceDot(ImDrawListPtr drawList, Vector2 center, int presence, float scale)
    {
        if (presence != 1)
        {
            return;
        }

        drawList.AddCircleFilled(center, 6.5f * scale, ImGui.GetColorU32(YellowPagesInk.Shared.BackdropTop), 20);
        drawList.AddCircleFilled(center, 4.5f * scale, ImGui.GetColorU32(YellowPagesInk.Shared.PresenceGreen), 20);
    }
}
