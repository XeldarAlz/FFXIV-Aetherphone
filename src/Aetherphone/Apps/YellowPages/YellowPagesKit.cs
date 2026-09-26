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
    public const float PillHeight = 24f;
    public const float PillPadX = 10f;
    public const float PillGlyphSize = 13f;
    public const float PillGlyphGap = 5f;
    public const float GlassButtonRadius = 17f;
    public const float GlassGlyphSize = 19f;

    public static readonly Vector4 OpenGreen = new(0.24f, 0.82f, 0.44f, 1f);
    public static readonly Vector4 AfterDarkPink = new(0.94f, 0.42f, 0.62f, 1f);
    public static readonly Vector4 WantedTint = AccentRing.Azure;
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 OverlayFill = new(0.03f, 0.02f, 0.01f, 0.58f);
    public static readonly Vector4 OverlayHover = new(0.06f, 0.05f, 0.03f, 0.80f);
    public static readonly Vector4 ScrimClear = new(0f, 0f, 0f, 0f);
    public static readonly Vector4 ScrimDeep = new(0.02f, 0.015f, 0.005f, 0.86f);

    private static readonly TextStyle PillStyle = TextStyles.Caption1;
    private static readonly TextStyle PillEmphasisStyle = new(0.72f, FontWeight.SemiBold);

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
                        $"{TimeText.DayLabel(state.NextOpeningUnix)} {TimeText.Clock(state.NextOpeningUnix)}"),
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

    public static float PillWidth(string label, float scale, bool glyph, bool emphasis = false)
    {
        var size = Typography.Measure(label, emphasis ? PillEmphasisStyle : PillStyle);
        return size.X + PillPadX * 2f * scale + (glyph ? (PillGlyphSize + PillGlyphGap) * scale : 0f);
    }

    public static float Pill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 fill, Vector4 ink,
        float scale, string glyph = "", bool emphasis = false, Vector4? stroke = null)
    {
        var style = emphasis ? PillEmphasisStyle : PillStyle;
        var size = Typography.Measure(label, style);
        var height = PillHeight * scale;
        var hasGlyph = glyph.Length > 0;
        var width = size.X + PillPadX * 2f * scale + (hasGlyph ? (PillGlyphSize + PillGlyphGap) * scale : 0f);
        var max = topLeft + new Vector2(width, height);
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(fill));
        if (stroke is { } strokeInk)
        {
            Squircle.Stroke(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(strokeInk), 1f);
        }

        var textLeft = topLeft.X + PillPadX * scale;
        if (hasGlyph)
        {
            PhoneIcon.Draw(drawList, new Vector2(textLeft + PillGlyphSize * scale * 0.5f, topLeft.Y + height * 0.5f),
                glyph, ink, PillGlyphSize * scale);
            textLeft += (PillGlyphSize + PillGlyphGap) * scale;
        }

        Typography.Draw(drawList, new Vector2(textLeft, topLeft.Y + (height - size.Y) * 0.5f), label, ink, style);
        return width;
    }

    public static float LivePill(ImDrawListPtr drawList, Vector2 topLeft, string label, float scale)
    {
        var style = PillEmphasisStyle;
        var size = Typography.Measure(label, style);
        var height = PillHeight * scale;
        var dotSpace = 12f * scale;
        var width = size.X + PillPadX * 2f * scale + dotSpace;
        var max = topLeft + new Vector2(width, height);
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(OpenGreen));
        LiveDot(drawList, new Vector2(topLeft.X + PillPadX * scale + 2f * scale, topLeft.Y + height * 0.5f),
            new Vector4(0.03f, 0.10f, 0.05f, 1f), scale);
        Typography.Draw(drawList, new Vector2(topLeft.X + PillPadX * scale + dotSpace, topLeft.Y + (height - size.Y) * 0.5f),
            label, new Vector4(0.03f, 0.10f, 0.05f, 1f), style);
        return width;
    }

    public static void LiveDot(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, 5.2f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.40f * pulse)), 20,
            1.3f * scale);
        drawList.AddCircleFilled(center, 3f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.65f + 0.35f * pulse)),
            20);
    }

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

    public static void BottomScrim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float share)
    {
        var top = new Vector2(min.X, max.Y - (max.Y - min.Y) * share);
        drawList.AddRectFilledMultiColor(top, max, ImGui.GetColorU32(ScrimClear), ImGui.GetColorU32(ScrimClear),
            ImGui.GetColorU32(ScrimDeep), ImGui.GetColorU32(ScrimDeep));
    }

    public static bool GlassButton(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip, float scale,
        bool highlighted = false, Vector4? glyphInk = null)
    {
        var radius = GlassButtonRadius * scale;
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? OverlayHover : OverlayFill), 32);
        if (highlighted)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(YellowPagesInk.Shared.AccentLink, 0.9f)),
                32, 1.3f * scale);
        }

        PhoneIcon.Draw(drawList, center, glyph, glyphInk ?? White, GlassGlyphSize * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Below);
        return UiInteract.Click(center - extent, center + extent, hovered);
    }

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
