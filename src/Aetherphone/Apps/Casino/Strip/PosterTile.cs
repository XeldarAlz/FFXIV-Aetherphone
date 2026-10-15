using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal readonly record struct PosterInfo(bool Open, int Crowd, string Meta, string Return, string Badge);

internal readonly record struct PosterLayout(
    float Radius,
    float ArtHeight,
    float Pad,
    float TextTop,
    float TextBottom,
    float SignTop,
    float SignHeight,
    float SignInset,
    float CapsuleHeight,
    float CapsuleInset,
    bool CapsuleBand,
    int TitleSlots,
    int DetailSlots,
    float GlyphFraction,
    TextStyle Title,
    TextStyle TitleFallback,
    bool Centered);

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
    public const float SignFade = 14f;
    public const float GridArtHeight = 98f;
    public const float QuickArtHeight = 76f;
    public const float QuickRadius = 14f;

    private const float SkyLuminance = 0.20f;
    private const float RimAlpha = 0.45f;
    private const float GlowAlpha = 0.30f;
    private const float ScrimAlpha = 0.78f;
    private const float ClosedVeil = 0.55f;

    private static readonly Vector4 Night = new(0.027f, 0.020f, 0.055f, 1f);

    public static readonly PosterLayout Rail = new(Metrics.Radius.Widget, 0f, Pad, Pad, Pad, SignTop, SignHeight, Pad,
        CrowdHeight, Pad * 0.6f, true, TitleLines, DetailLines, GlyphFraction, TextStyles.Headline,
        TextStyles.SubheadlineEmphasized, false);

    public static readonly PosterLayout Grid = new(Metrics.Radius.Card, GridArtHeight, 9f, 6f, 9f, 8f, 12f, 7f, 18f,
        5f, true, TitleLines, 1, GlyphFraction, TextStyles.Headline, TextStyles.SubheadlineEmphasized, false);

    public static readonly PosterLayout GridBare = Grid with { CapsuleBand = false };

    public static readonly PosterLayout Quick = new(QuickRadius, QuickArtHeight, 4f, 5f, 7f, 7f, 9f, 6f, 18f, 4f, true,
        1, 1, 0.20f, TextStyles.SubheadlineEmphasized, TextStyles.FootnoteEmphasized, true);

    public static float Width(float rowWidth, float gap, float scale) =>
        Math.Clamp((rowWidth - gap) / VisibleTiles, MinWidth * scale, MaxWidth * scale);

    public static float Height(float tileWidth, float textBlock) => tileWidth * ArtAspect + textBlock;

    public static float TileHeight(in PosterLayout layout, float textBlock, float scale) =>
        layout.ArtHeight * scale + textBlock;

    public static float TextBlock(float scale) => TextBlock(Rail, scale);

    public static float TextBlock(float scale, float headlineHeight, float footnoteHeight) =>
        TextBlock(Rail, scale, headlineHeight, footnoteHeight);

    public static float TextBlock(in PosterLayout layout, float scale) =>
        TextBlock(layout, scale, Typography.LineHeight(layout.Title), Typography.LineHeight(TextStyles.Footnote));

    public static float TextBlock(in PosterLayout layout, float scale, float titleHeight, float footnoteHeight) =>
        (layout.TextTop + layout.TextBottom) * scale + titleHeight * layout.TitleSlots
        + footnoteHeight * layout.DetailSlots;

    public static float SignBandTop(float scale) => SignBandTop(Rail, scale);

    public static float SignBandTop(in PosterLayout layout, float scale) =>
        layout.CapsuleBand
            ? (layout.CapsuleInset + layout.CapsuleHeight + CapsuleGap) * scale
            : layout.SignTop * scale;

    public static float SignBandBottom(float scale) => SignBandBottom(Rail, scale);

    public static float SignBandBottom(in PosterLayout layout, float scale) =>
        SignBandTop(layout, scale) + (layout.SignHeight + CapsuleGap) * scale;

    public static void DrawSignScrim(ImDrawListPtr drawList, Rect tile, float scale) =>
        DrawSignScrim(drawList, tile, Rail, scale);

    public static void DrawSignScrim(ImDrawListPtr drawList, Rect tile, in PosterLayout layout, float scale)
    {
        var bottom = tile.Min.Y + SignBandBottom(layout, scale);
        drawList.PushClipRect(tile.Min, new Vector2(tile.Max.X, bottom), true);
        Squircle.Fill(drawList, tile.Min, tile.Max, layout.Radius * scale,
            ImGui.GetColorU32(Night with { W = ScrimAlpha }));
        drawList.PopClipRect();
        var solid = ImGui.GetColorU32(Night with { W = ScrimAlpha });
        var clear = ImGui.GetColorU32(Night with { W = 0f });
        drawList.AddRectFilledMultiColor(new Vector2(tile.Min.X, bottom),
            new Vector2(tile.Max.X, bottom + SignFade * scale), solid, solid, clear, clear);
    }

    public static Vector2 ArtCenter(Rect tile, float textBlock, float scale) =>
        ArtCenter(tile, Rail, textBlock, scale);

    public static Vector2 ArtCenter(Rect tile, in PosterLayout layout, float textBlock, float scale)
    {
        var signBottom = tile.Min.Y + SignBandTop(layout, scale) + layout.SignHeight * scale;
        var artBottom = tile.Max.Y - textBlock;
        return new Vector2(tile.Center.X, (signBottom + artBottom) * 0.5f);
    }

    public static Rect ArtBelowSign(Rect tile, in PosterLayout layout, float textBlock, float scale) =>
        new(new Vector2(tile.Min.X, tile.Min.Y + SignBandBottom(layout, scale)),
            new Vector2(tile.Max.X, tile.Max.Y - textBlock));

    public static void DrawFrame(ImDrawListPtr drawList, Rect tile, Vector4 tint, float scale) =>
        DrawFrame(drawList, tile, tint, Rail, scale);

    public static void DrawFrame(ImDrawListPtr drawList, Rect tile, Vector4 tint, in PosterLayout layout, float scale)
    {
        var sky = Palette.ShadeToLuminance(tint with { W = 1f }, SkyLuminance);
        Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, layout.Radius * scale, ImGui.GetColorU32(sky),
            ImGui.GetColorU32(Night));
        var glowCenter = new Vector2(tile.Center.X, tile.Min.Y + tile.Height * 0.38f);
        drawList.AddCircleFilled(glowCenter, tile.Width * 0.42f, ImGui.GetColorU32(tint with { W = GlowAlpha * 0.4f }),
            40);
    }

    public static void DrawSign(ImDrawListPtr drawList, Rect tile, CasinoSign sign, float phase, float scale) =>
        DrawSign(drawList, tile, sign, phase, scale, SignTop * scale);

    public static void DrawSign(ImDrawListPtr drawList, Rect tile, CasinoSign sign, float phase, float scale,
        float top) =>
        DrawSign(drawList, tile, CasinoSigns.Text(sign), phase, SignHeight * scale, Pad * scale, top);

    public static void DrawSign(ImDrawListPtr drawList, Rect tile, ReadOnlySpan<char> text, float phase,
        in PosterLayout layout, float scale) =>
        DrawSign(drawList, tile, text, phase, layout.SignHeight * scale, layout.SignInset * scale,
            SignBandTop(layout, scale));

    private static void DrawSign(ImDrawListPtr drawList, Rect tile, ReadOnlySpan<char> text, float phase,
        float maxHeight, float inset, float top)
    {
        var atOne = CasinoSigns.Measure(text, 1f);
        var room = MathF.Max(1f, tile.Width - inset * 2f);
        var height = atOne <= 0f ? maxHeight : MathF.Min(maxHeight, room / atOne);
        var center = new Vector2(tile.Center.X, tile.Min.Y + top + maxHeight * 0.5f);
        var flicker = 0.84f + 0.16f * MathF.Sin(phase * 2.7f + tile.Min.X * 0.013f);
        CasinoSigns.Draw(drawList, text, center, height, CasinoColors.LightA, flicker);
    }

    public static void DrawGlyph(ImDrawListPtr drawList, Rect tile, string gameId, float scale) =>
        DrawGlyph(drawList, tile, gameId, Rail, TextBlock(scale), scale);

    public static void DrawGlyph(ImDrawListPtr drawList, Rect tile, string gameId, in PosterLayout layout,
        float textBlock, float scale) =>
        CasinoGlyphs.Draw(drawList, gameId, ArtCenter(tile, layout, textBlock, scale),
            tile.Width * layout.GlyphFraction, ImGui.GetColorU32(CasinoArt.White), ImGui.GetColorU32(Night));

    public static void DrawIcon(ImDrawListPtr drawList, Rect tile, Dalamud.Interface.FontAwesomeIcon icon,
        float scale) =>
        AppSkin.Icon(drawList, ArtCenter(tile, TextBlock(scale), scale), IconGlyph.Of(icon), CasinoArt.White,
            IconScale);

    public static void DrawText(ImDrawListPtr drawList, Rect tile, string title, in PosterInfo info, float scale) =>
        DrawText(drawList, tile, title, info, Rail, TextBlock(scale), scale);

    public static void DrawText(ImDrawListPtr drawList, Rect tile, string title, in PosterInfo info,
        in PosterLayout layout, float textBlock, float scale)
    {
        var pad = layout.Pad * scale;
        var scrimTop = tile.Max.Y - textBlock;
        drawList.PushClipRect(new Vector2(tile.Min.X, scrimTop), tile.Max, true);
        Squircle.Fill(drawList, tile.Min, tile.Max, layout.Radius * scale,
            ImGui.GetColorU32(Night with { W = ScrimAlpha }));
        drawList.PopClipRect();
        var width = MathF.Max(1f, tile.Width - pad * 2f);
        var titleFit = TileLabel.Fit(title, width, TitleLines, layout.Title, layout.TitleFallback);
        var titleCount = titleFit.Lines.Length;
        var titleHeight = Typography.LineHeight(titleFit.Style);
        var footnoteHeight = Typography.LineHeight(TextStyles.Footnote);
        var detailBudget = Math.Max(0, layout.TitleSlots + layout.DetailSlots - titleCount);
        var returnLines = info.Return.Length > 0
            ? Typography.WrapText(info.Return, TextStyles.FootnoteEmphasized, width)
            : Array.Empty<string>();
        var returnCount = Math.Min(returnLines.Length, detailBudget);
        var metaLines = info.Meta.Length > 0
            ? Typography.WrapText(info.Meta, TextStyles.Footnote, width)
            : Array.Empty<string>();
        var metaCount = Math.Min(metaLines.Length, detailBudget - returnCount);
        var top = tile.Max.Y - layout.TextBottom * scale - titleCount * titleHeight
                  - (metaCount + returnCount) * footnoteHeight;
        for (var line = 0; line < titleCount; line++)
        {
            DrawLine(drawList, tile, layout, pad, top, titleFit.Lines[line], CasinoColors.InkTitle, titleFit.Style);
            top += titleHeight;
        }

        for (var line = 0; line < metaCount; line++)
        {
            DrawLine(drawList, tile, layout, pad, top, metaLines[line], CasinoColors.InkBody, TextStyles.Footnote);
            top += footnoteHeight;
        }

        for (var line = 0; line < returnCount; line++)
        {
            DrawLine(drawList, tile, layout, pad, top, returnLines[line], CasinoColors.Money,
                TextStyles.FootnoteEmphasized);
            top += footnoteHeight;
        }
    }

    private static void DrawLine(ImDrawListPtr drawList, Rect tile, in PosterLayout layout, float pad, float top,
        string text, Vector4 ink, in TextStyle style)
    {
        var left = layout.Centered ? tile.Center.X - Typography.Measure(text, style).X * 0.5f : tile.Min.X + pad;
        Typography.Draw(drawList, new Vector2(left, top), text, ink, style);
    }

    public static void DrawCrowd(ImDrawListPtr drawList, Rect tile, int crowd, float scale) =>
        DrawCrowd(drawList, tile, crowd, Rail, scale);

    public static void DrawCrowd(ImDrawListPtr drawList, Rect tile, int crowd, in PosterLayout layout, float scale)
    {
        if (crowd <= 0)
        {
            return;
        }

        var label = Games.Framework.GameNumber.Label(crowd);
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(label, style);
        var height = layout.CapsuleHeight * scale;
        var inset = layout.CapsuleInset * scale;
        var dot = CasinoArt.LiveDotRadius * scale;
        var width = size.X + dot * 2f + height * 0.75f;
        var max = new Vector2(tile.Max.X - inset, tile.Min.Y + inset + height);
        var min = new Vector2(max.X - width, max.Y - height);
        StageText.Capsule(drawList, min, max);
        var dotCenter = new Vector2(min.X + height * 0.4f + dot, (min.Y + max.Y) * 0.5f);
        CasinoArt.LiveDot(drawList, dotCenter, scale, CasinoColors.LightA, true);
        Typography.Draw(drawList, new Vector2(dotCenter.X + dot + Metrics.Space.Xs * scale, dotCenter.Y - size.Y * 0.5f),
            label, CasinoColors.InkTitle, style);
    }

    public static void DrawBadge(ImDrawListPtr drawList, Rect tile, string badge, float scale) =>
        DrawBadge(drawList, tile, badge, Rail, scale);

    public static void DrawBadge(ImDrawListPtr drawList, Rect tile, string badge, in PosterLayout layout, float scale)
    {
        if (badge.Length == 0)
        {
            return;
        }

        var style = TextStyles.FootnoteEmphasized;
        var height = layout.CapsuleHeight * scale;
        var inset = layout.CapsuleInset * scale;
        var label = Typography.FitText(badge, MathF.Max(1f, tile.Width - inset * 2f - height * 0.8f), style);
        var size = Typography.Measure(label, style);
        var min = new Vector2(tile.Min.X + inset, tile.Min.Y + inset);
        var max = new Vector2(min.X + size.X + height * 0.8f, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(CasinoColors.Money));
        Typography.Draw(drawList, (min + max) * 0.5f - size * 0.5f, label, Night, style);
    }

    public static void DrawClosed(ImDrawListPtr drawList, Rect tile, float scale) =>
        DrawClosed(drawList, tile, Rail, scale);

    public static void DrawClosed(ImDrawListPtr drawList, Rect tile, in PosterLayout layout, float scale)
    {
        Squircle.Fill(drawList, tile.Min, tile.Max, layout.Radius * scale,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ClosedVeil)));
        StageText.Plate(drawList, new Vector2(tile.Center.X, tile.Min.Y + tile.Height * 0.42f),
            Loc.T(L.Strip.NotOpenYet), tile.Width - layout.Pad * 2f * scale, CasinoColors.InkTitle,
            TextStyles.FootnoteEmphasized, scale);
    }

    public static void DrawRim(ImDrawListPtr drawList, Rect tile, Vector4 tint, bool hovered, float scale) =>
        DrawRim(drawList, tile, tint, hovered, Rail, scale);

    public static void DrawRim(ImDrawListPtr drawList, Rect tile, Vector4 tint, bool hovered, in PosterLayout layout,
        float scale)
    {
        Squircle.Stroke(drawList, tile.Min, tile.Max, layout.Radius * scale,
            ImGui.GetColorU32(tint with { W = hovered ? 0.85f : RimAlpha }), MathF.Max(1f, 1.2f * scale));
    }
}
