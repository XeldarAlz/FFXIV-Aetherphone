using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SocialChrome
{
    public const float CellPadX = 16f;
    public const float BackChipRadius = 17f;
    public const float HeaderIconRadius = 18f;
    public const float HeaderIconPitch = 40f;

    private const float BackChipInset = 12f;
    private const float BackChipHitHalf = 22f;
    private const float BackChipGlyph = 17f;
    private const float TitleGap = 10f;
    private const float BadgeHeight = 16f;
    private const float StatGap = 5f;
    private const float MetaChipPadX = 9f;
    private const float MetaChipGlyph = 14f;
    private const float MetaChipGlyphGap = 5f;
    private const float MetaChipMinLabel = 20f;

    public const float MetaChipHeight = 26f;
    public const float MetaChipGap = 8f;

    public static readonly TextStyle SubtitleStyle = new(0.8f, FontWeight.Regular);
    public static readonly TextStyle BadgeStyle = new(0.67f, FontWeight.Bold);

    public static Vector2 HeaderSlot(Rect area, int index)
    {
        var scale = UiScale.Current;
        return new Vector2(
            area.Max.X - (CellPadX + HeaderIconRadius + index * HeaderIconPitch) * scale,
            area.Min.Y + AppHeader.Height * scale * 0.5f);
    }

    public static float HeaderReserve(int slots) => slots == 0 ? 0f : slots * HeaderIconPitch - 4f;

    public static float DrawScreenHeader(Rect area, string title, SocialInk ink, Action back, in TextStyle titleStyle,
        float trailingReserve = 0f, string subtitle = "", bool showBack = true, bool centered = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var titleLeft = area.Min.X + CellPadX * scale;
        if (showBack)
        {
            var chipRadius = BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + BackChipInset * scale + chipRadius, rowCenterY);
            if (DrawBackChip(drawList, chipCenter, chipRadius, ink))
            {
                back();
            }

            titleLeft = chipCenter.X + chipRadius + TitleGap * scale;
        }

        var titleRight = area.Max.X - CellPadX * scale - trailingReserve * scale;
        var titleHeight = Typography.LineHeight(titleStyle);
        if (centered)
        {
            var reserve = MathF.Max(titleLeft - area.Min.X, area.Max.X - titleRight);
            var maxWidth = MathF.Max(1f, area.Width - reserve * 2f - 8f * scale);
            var fitted = Typography.FitText(title, maxWidth, titleStyle);
            Typography.DrawCentered(drawList, new Vector2(area.Center.X, rowCenterY), fitted, ink.TitleInk, titleStyle);
            return titleLeft;
        }

        var leftFitted = Typography.FitText(title, MathF.Max(1f, titleRight - titleLeft), titleStyle);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - titleHeight * 0.5f), leftFitted,
                ink.TitleInk, titleStyle);
            return titleLeft;
        }

        var subtitleHeight = Typography.LineHeight(SubtitleStyle);
        var blockTop = rowCenterY - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(titleLeft, blockTop), leftFitted, ink.TitleInk, titleStyle);
        Typography.Draw(drawList, new Vector2(titleLeft, blockTop + titleHeight),
            Typography.FitText(subtitle, MathF.Max(1f, titleRight - titleLeft), SubtitleStyle), ink.MutedInk,
            SubtitleStyle);
        return titleLeft;
    }

    public static bool DrawBackChip(ImDrawListPtr drawList, Vector2 center, float radius, SocialInk ink)
    {
        var scale = UiScale.Current;
        var hitHalf = BackChipHitHalf * scale;
        var hitMin = center - new Vector2(hitHalf, hitHalf);
        var hitMax = center + new Vector2(hitHalf, hitHalf);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? ink.BackChipHover : ink.BackChipFill), 32);
        PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronLeft, ink.TitleInk, BackChipGlyph * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
    }

    public static bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, float radius, string glyph,
        float iconSize, string tooltip, SocialInk ink, Vector4 idleInk, bool highlighted = false, int badge = 0,
        HoverLabelSide side = HoverLabelSide.Below)
    {
        var scale = UiScale.Current;
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        if (hovered || highlighted)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(highlighted ? ink.AccentWash : ink.FieldFill), 32);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
        }

        PhoneIcon.Draw(drawList, center, glyph, highlighted ? ink.AccentLink : idleInk, iconSize * scale);
        DrawCountBadge(drawList, center + new Vector2(10f * scale, -10f * scale), badge, ink);
        HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, side);
        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    public static void DrawCountBadge(ImDrawListPtr drawList, Vector2 center, int count, SocialInk ink)
    {
        if (count <= 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var label = count > 99 ? "99+" : count.ToString(Loc.Culture);
        var size = Typography.Measure(label, BadgeStyle);
        var height = BadgeHeight * scale;
        var width = MathF.Max(height, size.X + 9f * scale);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var border = new Vector2(2f * scale, 2f * scale);
        Squircle.Fill(drawList, min - border, max + border, (height + 4f * scale) * 0.5f,
            ImGui.GetColorU32(ink.BackdropTop));
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ink.Accent));
        Typography.DrawCentered(drawList, center, label, ink.White, BadgeStyle);
    }

    public static void DrawUnreadDot(ImDrawListPtr drawList, Vector2 center, SocialInk ink)
    {
        var scale = UiScale.Current;
        drawList.AddCircleFilled(center, 4.5f * scale, ImGui.GetColorU32(ink.BackdropTop), 20);
        drawList.AddCircleFilled(center, 3.2f * scale, ImGui.GetColorU32(ink.Accent), 20);
    }

    public static float DrawStat(ImDrawListPtr drawList, float left, float top, float lineHeight, string value,
        string label, bool tappable, float limit, SocialInk ink, in TextStyle valueStyle, in TextStyle labelStyle,
        out bool clicked)
    {
        var scale = UiScale.Current;
        var gap = StatGap * scale;
        var valueSize = Typography.Measure(value, valueStyle);
        var labelFitted = Typography.FitText(label, MathF.Max(1f, limit - left - valueSize.X - gap), labelStyle);
        var labelSize = Typography.Measure(labelFitted, labelStyle);
        var min = new Vector2(left, top);
        var max = new Vector2(left + valueSize.X + gap + labelSize.X, top + lineHeight);
        var hovered = tappable && UiInteract.Hover(min, max);
        Typography.Draw(drawList, min, value, ink.TitleInk, valueStyle);
        Typography.Draw(drawList, new Vector2(min.X + valueSize.X + gap, top + (lineHeight - labelSize.Y) * 0.5f),
            labelFitted, ink.MutedInk, labelStyle);
        if (hovered)
        {
            drawList.AddLine(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), ImGui.GetColorU32(ink.MutedInk), 1f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        clicked = tappable && UiInteract.Click(min, max, hovered);
        return max.X;
    }

    public static void DrawSectionLabel(string label, SocialInk ink, in TextStyle style)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Typography.LineHeight(style) + 12f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + CellPadX * scale, origin.Y + 8f * scale),
            Loc.Culture.TextInfo.ToUpper(label), ink.FaintInk, style);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static void DrawMetaChip(ImDrawListPtr drawList, ref float cursorX, float right, float centerY,
        string glyph, string label, SocialInk ink, in TextStyle style)
    {
        var scale = UiScale.Current;
        var padX = MetaChipPadX * scale;
        var glyphSize = glyph.Length > 0 ? MetaChipGlyph * scale : 0f;
        var glyphGap = glyph.Length > 0 ? MetaChipGlyphGap * scale : 0f;
        var available = right - cursorX - padX * 2f - glyphSize - glyphGap;
        if (available < MetaChipMinLabel * scale)
        {
            return;
        }

        var fitted = Typography.FitText(label, available, style);
        var size = Typography.Measure(fitted, style);
        var height = MetaChipHeight * scale;
        var min = new Vector2(cursorX, centerY - height * 0.5f);
        var max = new Vector2(cursorX + padX * 2f + glyphSize + glyphGap + size.X, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ink.ChipFill));
        if (glyph.Length > 0)
        {
            PhoneIcon.Draw(drawList, new Vector2(min.X + padX + glyphSize * 0.5f, centerY), glyph, ink.MutedInk,
                glyphSize);
        }

        Typography.Draw(drawList, new Vector2(min.X + padX + glyphSize + glyphGap, centerY - size.Y * 0.5f), fitted,
            ink.MutedInk, style);
        cursorX = max.X + MetaChipGap * scale;
    }

    public static void PaintBarBackdrop(AppSkin ui, ImDrawListPtr drawList, Rect bar, Rect screen)
    {
        var target = new Rect(bar.Min, new Vector2(bar.Max.X, MathF.Max(bar.Max.Y, screen.Max.Y)));
        ui.PaintGradient(drawList, target, screen, 0f);
    }
}
