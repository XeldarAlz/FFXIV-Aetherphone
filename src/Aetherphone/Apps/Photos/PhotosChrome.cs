using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Photos;

internal static class PhotosChrome
{
    public const float SectionTitleHeight = 40f;
    public const float BadgeRadius = 13f;

    private const float GlassDiscAlpha = 0.30f;
    private const float GlassDiscHoverAlpha = 0.48f;
    private const float ArrowRadius = 18f;
    private const float ArrowGlyph = 22f;
    private const float CoverGlyph = 30f;
    private const float NewTileDiscRadius = 22f;
    private const float NewTileGlyph = 24f;
    private const float NewTileLabelGap = 9f;
    private const float BadgeGlyph = 16f;
    private const float FavoriteBadgeGlyph = 13f;
    private const float FavoriteBadgeInset = 7f;
    private const float SelectionGlyph = 22f;
    private const float SelectionInset = 6f;
    private const float SelectionVeilAlpha = 0.22f;
    private const float SelectionRingInset = 2f;
    private const float SelectionRingStroke = 2.5f;
    private const float ShadowAlpha = 0.45f;
    private const float IdleMarkAlpha = 0.85f;
    private const float TileCaptionHeight = 30f;
    private const float TileCaptionCenter = 0.42f;

    private static readonly Vector4 HoverWash = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 ScrimInk = new(0f, 0f, 0f, 0.52f);
    private static readonly Vector4 Clear = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    public static void Thumbnail(ImDrawListPtr drawList, IDalamudTextureWrap? texture, Vector2 min, Vector2 max,
        bool hovered, Vector4 placeholder, bool fit = false)
    {
        if (texture is null)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(placeholder));
        }
        else if (fit)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(placeholder));
            var frame = ImageFit.CenteredRect(new Rect(min, max), texture.Size.X / MathF.Max(1f, texture.Size.Y));
            drawList.AddImage(texture.Handle, frame.Min, frame.Max);
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            drawList.AddImage(texture.Handle, min, max, uv0, uv1);
        }

        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(HoverWash));
        }
    }

    public static void Cover(ImDrawListPtr drawList, IDalamudTextureWrap? texture, Vector2 min, Vector2 max,
        float rounding, SocialInk ink, float scale, bool hovered)
    {
        Elevation.Card(drawList, min, max, rounding, scale, hovered ? 1f : 0.75f);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ink.ThumbFill));
            PhoneIcon.Draw(drawList, (min + max) * 0.5f, PhoneIcons.Photo, ink.FaintInk, CoverGlyph * scale);
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            Squircle.FillImage(drawList, min, max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }

        if (hovered)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(HoverWash));
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ink.ChipStroke), 1f * scale);
    }

    public static bool NewAlbumTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, string label,
        SocialInk ink, float scale)
    {
        var hovered = UiInteract.Hover(min, max);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(hovered ? ink.ChipHover : ink.ChipFill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ink.GlassStroke), 1f * scale);
        var discRadius = NewTileDiscRadius * scale;
        var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var stackHeight = discRadius * 2f + NewTileLabelGap * scale + labelHeight;
        var centerX = (min.X + max.X) * 0.5f;
        var discCenter = new Vector2(centerX, (min.Y + max.Y) * 0.5f - stackHeight * 0.5f + discRadius);
        drawList.AddCircleFilled(discCenter, discRadius, ImGui.GetColorU32(ink.AccentWash), 32);
        PhoneIcon.Draw(drawList, discCenter, PhoneIcons.Plus, ink.AccentLink, NewTileGlyph * scale);
        var fitted = Typography.FitText(label, max.X - min.X - Metrics.Space.Lg * scale, TextStyles.SubheadlineEmphasized);
        Typography.DrawCentered(drawList,
            new Vector2(centerX, discCenter.Y + discRadius + NewTileLabelGap * scale + labelHeight * 0.5f), fitted,
            hovered ? ink.TitleInk : ink.BodyInk, TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static bool CoverBadge(ImDrawListPtr drawList, Vector2 center, string tooltip, SocialInk ink, float scale)
    {
        var radius = BadgeRadius * scale;
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Black, hovered ? GlassDiscHoverAlpha : GlassDiscAlpha)), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ink.GlassStroke), 24, 1f * scale);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Dots, ink.White, BadgeGlyph * scale);
        HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Below);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    public static void TileCaption(ImDrawListPtr drawList, Vector2 min, Vector2 max, string text, SocialInk ink,
        float scale)
    {
        var height = TileCaptionHeight * scale;
        var solid = ImGui.GetColorU32(ScrimInk);
        var clear = ImGui.GetColorU32(Clear);
        drawList.AddRectFilledMultiColor(new Vector2(min.X, max.Y - height), max, clear, clear, solid, solid);
        var fitted = Typography.FitText(text, max.X - min.X - Metrics.Space.Sm * scale, TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, max.Y - height * TileCaptionCenter),
            fitted, ink.White, TextStyles.FootnoteEmphasized);
    }

    public static void FavoriteBadge(ImDrawListPtr drawList, Vector2 min, Vector2 max, SocialInk ink, float scale)
    {
        var glyph = FavoriteBadgeGlyph * scale;
        var inset = FavoriteBadgeInset * scale;
        var center = new Vector2(min.X + inset + glyph * 0.5f, max.Y - inset - glyph * 0.5f);
        PhoneIcon.Draw(drawList, center + new Vector2(0f, 1f * scale), PhoneIcons.HeartFilled,
            Palette.WithAlpha(Black, ShadowAlpha), glyph);
        PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, ink.White, glyph);
    }

    public static void SelectionMark(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool selected, SocialInk ink,
        float scale)
    {
        var glyph = SelectionGlyph * scale;
        var inset = SelectionInset * scale;
        var center = new Vector2(max.X - inset - glyph * 0.5f, min.Y + inset + glyph * 0.5f);
        if (!selected)
        {
            PhoneIcon.Draw(drawList, center + new Vector2(0f, 1f * scale), PhoneIcons.Circle,
                Palette.WithAlpha(Black, ShadowAlpha), glyph);
            PhoneIcon.Draw(drawList, center, PhoneIcons.Circle, Palette.WithAlpha(ink.White, IdleMarkAlpha), glyph);
            return;
        }

        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ink.Accent, SelectionVeilAlpha)));
        var ringInset = new Vector2(SelectionRingInset * scale, SelectionRingInset * scale);
        drawList.AddRect(min + ringInset, max - ringInset, ImGui.GetColorU32(ink.Accent), 0f, ImDrawFlags.None,
            SelectionRingStroke * scale);
        drawList.AddCircleFilled(center, glyph * 0.5f, ImGui.GetColorU32(ink.White), 24);
        PhoneIcon.Draw(drawList, center, PhoneIcons.CircleCheckFilled, ink.Accent, glyph);
    }

    public static void SectionTitle(ImDrawListPtr drawList, float left, float right, float top, string title,
        SocialInk ink, float scale)
    {
        var centerY = top + SectionTitleHeight * scale * 0.5f;
        var fitted = Typography.FitText(title, MathF.Max(1f, right - left), TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, centerY - size.Y * 0.5f), fitted, ink.TitleInk, TextStyles.Title3);
    }

    public static void TopScrim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float height)
    {
        var solid = ImGui.GetColorU32(ScrimInk);
        var clear = ImGui.GetColorU32(Clear);
        drawList.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + height), solid, solid, clear, clear);
    }

    public static void BottomScrim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float height)
    {
        var solid = ImGui.GetColorU32(ScrimInk);
        var clear = ImGui.GetColorU32(Clear);
        drawList.AddRectFilledMultiColor(new Vector2(min.X, max.Y - height), max, clear, clear, solid, solid);
    }

    public static bool GlassIcon(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, float glyphSize,
        string tooltip, SocialInk ink, Vector4 glyphInk, float scale, HoverLabelSide side)
    {
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Black, hovered ? GlassDiscHoverAlpha : GlassDiscAlpha)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ink.GlassStroke), 32, 1f * scale);
        PhoneIcon.Draw(drawList, center, glyph, hovered ? ink.White : glyphInk, glyphSize * scale);
        HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, side);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    public static bool Arrow(ImDrawListPtr drawList, Vector2 center, bool pointsLeft, SocialInk ink, float scale) =>
        GlassIcon(drawList, center, ArrowRadius * scale, pointsLeft ? PhoneIcons.ChevronLeft : PhoneIcons.ChevronRight,
            ArrowGlyph, Loc.T(pointsLeft ? L.Common.Previous : L.Common.Next), ink, ink.White, scale,
            HoverLabelSide.Below);
}
