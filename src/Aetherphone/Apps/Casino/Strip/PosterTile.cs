using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal readonly record struct PosterInfo(bool Open, int Crowd, string Meta, string Return, string Badge);

internal static class PosterTile
{
    public const float VisibleTiles = 2.3f;
    public const float ArtAspect = 0.9f;
    public const int TitleLines = 2;
    public const int DetailLines = 2;
    public const float CapsuleGap = 4f;
    public const float MinWidth = 128f;
    public const float MaxWidth = 176f;
    public const float Pad = 10f;
    public const float SignHeight = 15f;
    public const float SignTop = 12f;
    public const float GlyphFraction = 0.22f;
    public const float CrowdHeight = 24f;
    public const float IconScale = 2.4f;

    private const float SkyLuminance = 0.20f;
    private const float RimAlpha = 0.45f;
    private const float GlowAlpha = 0.30f;
    private const float ScrimAlpha = 0.78f;
    private const float ClosedVeil = 0.55f;

    private static readonly Vector4 Night = new(0.027f, 0.020f, 0.055f, 1f);

    public static float Width(float rowWidth, float gap, float scale) =>
        Math.Clamp((rowWidth - gap) / VisibleTiles, MinWidth * scale, MaxWidth * scale);

    public static float Height(float tileWidth, float textBlock) => tileWidth * ArtAspect + textBlock;

    public static float TextBlock(float scale) =>
        TextBlock(scale, Typography.LineHeight(TextStyles.Headline), Typography.LineHeight(TextStyles.Footnote));

    public static float TextBlock(float scale, float headlineHeight, float footnoteHeight) =>
        Pad * scale * 2f + headlineHeight * TitleLines + footnoteHeight * DetailLines;

    public static float SignBandTop(float scale) => (Pad * 0.6f + CrowdHeight + CapsuleGap) * scale;

    public static Vector2 ArtCenter(Rect tile, float textBlock, float scale)
    {
        var signBottom = tile.Min.Y + SignBandTop(scale) + SignHeight * scale;
        var artBottom = tile.Max.Y - textBlock;
        return new Vector2(tile.Center.X, (signBottom + artBottom) * 0.5f);
    }

    public static void DrawFrame(ImDrawListPtr drawList, Rect tile, Vector4 tint, float scale)
    {
        var radius = Metrics.Radius.Widget * scale;
        var sky = Palette.ShadeToLuminance(tint with { W = 1f }, SkyLuminance);
        Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(sky),
            ImGui.GetColorU32(Night));
        var glowCenter = new Vector2(tile.Center.X, tile.Min.Y + tile.Height * 0.38f);
        drawList.AddCircleFilled(glowCenter, tile.Width * 0.42f, ImGui.GetColorU32(tint with { W = GlowAlpha * 0.4f }),
            40);
    }

    public static void DrawSign(ImDrawListPtr drawList, Rect tile, CasinoSign sign, float phase, float scale) =>
        DrawSign(drawList, tile, sign, phase, scale, SignTop * scale);

    public static void DrawSign(ImDrawListPtr drawList, Rect tile, CasinoSign sign, float phase, float scale,
        float top)
    {
        var maxHeight = SignHeight * scale;
        var height = CasinoSigns.HeightToFit(sign, tile.Width - Pad * 2f * scale, maxHeight);
        var center = new Vector2(tile.Center.X, tile.Min.Y + top + maxHeight * 0.5f);
        var flicker = 0.84f + 0.16f * MathF.Sin(phase * 2.7f + tile.Min.X * 0.013f);
        CasinoSigns.Draw(drawList, sign, center, height, CasinoColors.LightA, flicker);
    }

    public static void DrawGlyph(ImDrawListPtr drawList, Rect tile, string gameId, float scale) =>
        CasinoGlyphs.Draw(drawList, gameId, ArtCenter(tile, TextBlock(scale), scale), tile.Width * GlyphFraction,
            ImGui.GetColorU32(CasinoArt.White), ImGui.GetColorU32(Night));

    public static void DrawIcon(ImDrawListPtr drawList, Rect tile, Dalamud.Interface.FontAwesomeIcon icon,
        float scale) =>
        AppSkin.Icon(drawList, ArtCenter(tile, TextBlock(scale), scale), IconGlyph.Of(icon), CasinoArt.White,
            IconScale);

    public static void DrawText(ImDrawListPtr drawList, Rect tile, string title, in PosterInfo info, float scale)
    {
        var pad = Pad * scale;
        var block = TextBlock(scale);
        var radius = Metrics.Radius.Widget * scale;
        var scrimTop = tile.Max.Y - block;
        drawList.PushClipRect(new Vector2(tile.Min.X, scrimTop), tile.Max, true);
        Squircle.Fill(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(Night with { W = ScrimAlpha }));
        drawList.PopClipRect();
        var width = MathF.Max(1f, tile.Width - pad * 2f);
        var left = tile.Min.X + pad;
        var titleStyle = TitleStyle(title, width);
        var titleLines = Typography.WrapText(title, titleStyle, width);
        var titleCount = Math.Min(titleLines.Length, TitleLines);
        var titleHeight = Typography.LineHeight(titleStyle);
        var footnoteHeight = Typography.LineHeight(TextStyles.Footnote);
        var metaLines = info.Meta.Length > 0 && info.Return.Length == 0
            ? Typography.WrapText(info.Meta, TextStyles.Footnote, width)
            : Array.Empty<string>();
        var metaCount = info.Return.Length > 0
            ? (info.Meta.Length > 0 ? 1 : 0) + 1
            : Math.Min(metaLines.Length, DetailLines);
        var top = tile.Max.Y - pad - titleCount * titleHeight - metaCount * footnoteHeight;
        for (var line = 0; line < titleCount; line++)
        {
            Typography.Draw(drawList, new Vector2(left, top), titleLines[line], CasinoColors.InkTitle, titleStyle);
            top += titleHeight;
        }

        if (info.Return.Length > 0)
        {
            if (info.Meta.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(left, top),
                    Typography.FitText(info.Meta, width, TextStyles.Footnote), CasinoColors.InkBody,
                    TextStyles.Footnote);
                top += footnoteHeight;
            }

            Typography.Draw(drawList, new Vector2(left, top),
                Typography.FitText(info.Return, width, TextStyles.FootnoteEmphasized), CasinoColors.Money,
                TextStyles.FootnoteEmphasized);
            return;
        }

        for (var line = 0; line < metaCount; line++)
        {
            Typography.Draw(drawList, new Vector2(left, top), metaLines[line], CasinoColors.InkBody,
                TextStyles.Footnote);
            top += footnoteHeight;
        }
    }

    private static TextStyle TitleStyle(string title, float width) =>
        Typography.WrapText(title, TextStyles.Headline, width).Length <= TitleLines
            ? TextStyles.Headline
            : TextStyles.SubheadlineEmphasized;

    public static void DrawCrowd(ImDrawListPtr drawList, Rect tile, int crowd, float scale)
    {
        if (crowd <= 0)
        {
            return;
        }

        var label = Games.Framework.GameNumber.Label(crowd);
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(label, style);
        var height = CrowdHeight * scale;
        var dot = CasinoArt.LiveDotRadius * scale;
        var width = size.X + dot * 2f + height * 0.75f;
        var max = new Vector2(tile.Max.X - Pad * 0.6f * scale, tile.Min.Y + Pad * 0.6f * scale + height);
        var min = new Vector2(max.X - width, max.Y - height);
        StageText.Capsule(drawList, min, max);
        var dotCenter = new Vector2(min.X + height * 0.4f + dot, (min.Y + max.Y) * 0.5f);
        CasinoArt.LiveDot(drawList, dotCenter, scale, CasinoColors.LightA, true);
        Typography.Draw(drawList, new Vector2(dotCenter.X + dot + Metrics.Space.Xs * scale, dotCenter.Y - size.Y * 0.5f),
            label, CasinoColors.InkTitle, style);
    }

    public static void DrawBadge(ImDrawListPtr drawList, Rect tile, string badge, float scale)
    {
        if (badge.Length == 0)
        {
            return;
        }

        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(badge, style);
        var height = CrowdHeight * scale;
        var min = new Vector2(tile.Min.X + Pad * 0.6f * scale, tile.Min.Y + Pad * 0.6f * scale);
        var max = new Vector2(min.X + size.X + height * 0.8f, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(CasinoColors.Money));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, badge, Night, style);
    }

    public static void DrawClosed(ImDrawListPtr drawList, Rect tile, float scale)
    {
        Squircle.Fill(drawList, tile.Min, tile.Max, Metrics.Radius.Widget * scale,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ClosedVeil)));
        StageText.Plate(drawList, new Vector2(tile.Center.X, tile.Min.Y + tile.Height * 0.42f),
            Loc.T(L.Strip.NotOpenYet), tile.Width - Pad * 2f * scale, CasinoColors.InkTitle,
            TextStyles.FootnoteEmphasized, scale);
    }

    public static void DrawRim(ImDrawListPtr drawList, Rect tile, Vector4 tint, bool hovered, float scale)
    {
        var radius = Metrics.Radius.Widget * scale;
        Squircle.Stroke(drawList, tile.Min, tile.Max, radius,
            ImGui.GetColorU32(tint with { W = hovered ? 0.85f : RimAlpha }), MathF.Max(1f, 1.2f * scale));
    }
}
