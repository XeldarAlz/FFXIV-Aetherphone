using Aetherphone.Core;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal static class HousingArt
{
    public const float SectionHeaderHeight = 40f;
    public const float SectionGap = 22f;
    public const float HeaderGap = 6f;
    public const float RowHeight = 62f;
    public const float TileSize = 40f;
    public const float TextGap = 12f;
    public const float LineGap = 2f;
    public const float BarHeight = 6f;
    public const float GlassButtonRadius = 18f;
    public const float SheetHeaderHeight = 52f;

    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
    private const float TileGlyphFraction = 0.5f;
    private const float BarTrackAlpha = 0.16f;
    private const float StateTileSize = 68f;
    private const float StateGlyphSize = 30f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = 40f;
    private const float StateActionPad = 44f;
    private const float StateActionMinWidth = 150f;
    private const float StateMaxText = 280f;
    private const float StateTextInset = 48f;
    private const float StateLift = 40f;
    private const float StackDividerInset = 8f;
    private const float GlyphScale = 0.95f;
    private const float DoneMargin = 8f;
    private const float CircleHoverMix = 0.08f;
    private const float DisabledAlpha = 0.45f;

    public static void Card(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale) =>
        ui.Card(drawList, min, max, Metrics.Radius.Widget * scale, true);

    public static FontAwesomeIcon DistrictIcon(uint districtId) => districtId switch
    {
        HousingDistricts.LavenderBedsId => FontAwesomeIcon.Leaf,
        HousingDistricts.GobletId => FontAwesomeIcon.Sun,
        HousingDistricts.ShiroganeId => FontAwesomeIcon.ToriiGate,
        HousingDistricts.EmpyreumId => FontAwesomeIcon.Snowflake,
        _ => FontAwesomeIcon.Anchor,
    };

    public static Vector4 DistrictHue(uint districtId) => districtId switch
    {
        HousingDistricts.LavenderBedsId => AccentRing.Violet,
        HousingDistricts.GobletId => AccentRing.Orange,
        HousingDistricts.ShiroganeId => AccentRing.Red,
        HousingDistricts.EmpyreumId => AccentRing.Cyan,
        _ => AccentRing.Azure,
    };

    public static void DistrictTile(ImDrawListPtr drawList, Vector2 center, float size, uint districtId,
        float alpha = 1f)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(DistrictHue(districtId)), alpha);
        ProgressRing.CenterIcon(drawList, center, DistrictIcon(districtId), AccentRing.Ink with { W = alpha },
            size * TileGlyphFraction);
    }

    public static float SectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        string trailing, AppSkin ui, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var height = SectionHeaderHeight * scale;
        var trailingSize = trailing.Length > 0 ? Typography.Measure(trailing, TextStyles.Body) : Vector2.Zero;
        var reserve = trailing.Length > 0 ? trailingSize.X + TextGap * scale : 0f;
        var fitted = Typography.FitText(title, MathF.Max(1f, width - reserve), TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + (height - size.Y) * 0.5f), fitted, ui.TitleInk,
            TextStyles.Title3);
        if (trailing.Length == 0)
        {
            return height;
        }

        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - trailingSize.X - TextGap * scale,
            origin.Y + (height - tapHeight) * 0.5f);
        var hitMax = new Vector2(origin.X + width, hitMin.Y + tapHeight);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent;
        Typography.Draw(drawList,
            new Vector2(origin.X + width - trailingSize.X, origin.Y + (height - trailingSize.Y) * 0.5f), trailing,
            ink, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        trailingClicked = UiInteract.Click(hitMin, hitMax, hovered);
        return height;
    }

    public static bool RowInteraction(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale,
        bool overlay = false)
    {
        var hovered = HousingChrome.Hover(row.Min, row.Max, overlay);
        if (!hovered)
        {
            return false;
        }

        var inset = WashInset * scale;
        var wash = ImGui.IsMouseDown(ImGuiMouseButton.Left)
            ? Palette.WithAlpha(ui.HoverTint, ui.HoverTint.W * PressedWash)
            : ui.HoverTint;
        Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
            WashRadius * scale, ImGui.GetColorU32(wash));
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    public static void Hairline(ImDrawListPtr drawList, AppSkin ui, float left, float right, float y) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);

    public static void Bar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float fraction, Vector4 fill)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(fill, BarTrackAlpha)), radius);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var width = MathF.Max(max.Y - min.Y, (max.X - min.X) * clamped);
        drawList.AddRectFilled(min, new Vector2(min.X + width, max.Y), ImGui.GetColorU32(fill), radius);
    }

    public static void Labels(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, Vector4 titleInk, Vector4 subtitleInk, float scale)
    {
        var width = MathF.Max(1f, right - left);
        var fittedTitle = Typography.FitText(title, width, TextStyles.BodyEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fittedTitle, titleInk,
                TextStyles.BodyEmphasized);
            return;
        }

        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + LineGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + LineGap * scale),
            Typography.FitText(subtitle, width, TextStyles.Footnote), subtitleInk, TextStyles.Footnote);
    }

    public static float TrailingValue(ImDrawListPtr drawList, float right, float centerY, string value,
        string caption, Vector4 ink, Vector4 captionInk, float scale)
    {
        var valueWidth = value.Length > 0 ? WidgetTextWidth(value) : 0f;
        var captionWidth = caption.Length > 0 ? Typography.Measure(caption, TextStyles.Caption1).X : 0f;
        var width = MathF.Max(valueWidth, captionWidth);
        if (width <= 0f)
        {
            return 0f;
        }

        var valueHeight = Typography.LineHeight(TextStyles.Headline);
        var captionHeight = caption.Length > 0 ? Typography.LineHeight(TextStyles.Caption1) : 0f;
        var top = centerY - (valueHeight + captionHeight) * 0.5f;
        if (value.Length > 0)
        {
            Windows.Widgets.WidgetText.Tabular(drawList, new Vector2(right - valueWidth, top), value, ink,
                TextStyles.Headline);
        }

        if (caption.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(right - captionWidth, top + valueHeight), caption, captionInk,
                TextStyles.Caption1);
        }

        return width;
    }

    public static bool StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, FontAwesomeIcon icon,
        string title, string hint, string actionLabel, float scale, bool overlay = false)
    {
        var centerX = body.Center.X;
        var tileSize = StateTileSize * scale;
        var tileTop = body.Center.Y - StateLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - StateTextInset * scale, StateMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        var bottom = hint.Length > 0
            ? Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, titleBottom + StateHintGap * scale), maxWidth)
            : titleBottom;
        if (actionLabel.Length == 0)
        {
            return false;
        }

        var natural = Typography.Measure(actionLabel, TextStyles.SubheadlineEmphasized).X + StateActionPad * scale;
        var width = Math.Clamp(natural, StateActionMinWidth * scale, MathF.Max(StateActionMinWidth * scale, maxWidth));
        var top = bottom + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top),
            new Vector2(centerX + width * 0.5f, top + StateActionHeight * scale));
        return HousingChrome.PillButton(rect, actionLabel, true, ui, overlay);
    }

    public static bool GlassCircle(ImDrawListPtr drawList, string id, Vector2 center, float radius, string glyph,
        Vector4 backdrop, Vector4 ink, string tooltip, bool active = false, Vector4 accent = default)
    {
        var scale = UiScale.Current;
        var hit = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        var drawn = radius * grow;
        Elevation.Icon(drawList, center - new Vector2(drawn, drawn), center + new Vector2(drawn, drawn), drawn,
            drawn, 0.6f);
        if (active)
        {
            Material.AccentGlass(drawList, center - new Vector2(drawn, drawn), center + new Vector2(drawn, drawn),
                drawn, scale, accent);
        }
        else
        {
            Material.ThemedGlass(drawList, center - new Vector2(drawn, drawn), center + new Vector2(drawn, drawn),
                drawn, scale, backdrop);
        }

        AppSkin.Icon(drawList, center, glyph, active ? AccentRing.Ink : ink, GlyphScale * grow);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static int GlassStack(ImDrawListPtr drawList, string id, Vector2 topCenter, float radius,
        ReadOnlySpan<string> glyphs, ReadOnlySpan<string> tooltips, Vector4 backdrop, Vector4 ink,
        Vector4 hairline, int accentIndex, Vector4 accent)
    {
        var scale = UiScale.Current;
        var cell = radius * 2f;
        var min = new Vector2(topCenter.X - radius, topCenter.Y);
        var max = new Vector2(topCenter.X + radius, topCenter.Y + cell * glyphs.Length);
        Elevation.Icon(drawList, min, max, radius, radius, 0.6f);
        Material.ThemedGlass(drawList, min, max, radius, scale, backdrop);
        var pressed = -1;
        for (var index = 0; index < glyphs.Length; index++)
        {
            var cellMin = new Vector2(min.X, min.Y + cell * index);
            var cellMax = new Vector2(max.X, cellMin.Y + cell);
            var center = (cellMin + cellMax) * 0.5f;
            if (index > 0)
            {
                var inset = StackDividerInset * scale;
                drawList.AddLine(new Vector2(min.X + inset, cellMin.Y), new Vector2(max.X - inset, cellMin.Y),
                    ImGui.GetColorU32(hairline), Metrics.Stroke.Hairline);
            }

            var hovered = UiInteract.Hover(cellMin, cellMax);
            var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var grow = PressFx.Scale(unchecked(ImGui.GetID(id) + (uint)index), down, PressFx.ControlPressedScale);
            AppSkin.Icon(drawList, center, glyphs[index], index == accentIndex ? accent : ink, GlyphScale * grow);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            HoverTooltip.Show(new Rect(cellMin, cellMax), tooltips[index], HoverLabelSide.Above);
            if (UiInteract.Click(cellMin, cellMax, hovered))
            {
                pressed = index;
            }
        }

        return pressed;
    }

    public static bool CircleAction(ImDrawListPtr drawList, uint id, Vector2 center, float radius, string glyph,
        AppSkin ui, string tooltip, bool active, bool enabled = true, bool overlay = false)
    {
        var hit = new Vector2(radius, radius);
        var hovered = enabled && HousingChrome.Hover(center - hit, center + hit, overlay);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        var fill = active ? ui.Accent : ui.FieldSurface;
        if (hovered)
        {
            fill = Palette.Mix(fill, ui.TitleInk, CircleHoverMix);
        }

        if (!enabled)
        {
            fill = Palette.WithAlpha(fill, fill.W * DisabledAlpha);
        }

        drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(fill), 32);
        var ink = active ? AccentRing.Ink : enabled ? ui.TitleInk : ui.MutedInk;
        AppSkin.Icon(drawList, center, glyph, ink, GlyphScale * grow);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        return enabled && UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static bool SheetHeader(ImDrawListPtr drawList, Rect content, string title, string doneLabel,
        Vector4 ink, Vector4 accent, float scale)
    {
        var height = SheetHeaderHeight * scale;
        var centerY = content.Min.Y + height * 0.5f;
        var pad = Metrics.Space.Lg * scale;
        var doneSize = Typography.Measure(doneLabel, TextStyles.Headline);
        var reserve = doneSize.X + pad + DoneMargin * scale;
        var fitted = Typography.FitText(title, MathF.Max(1f, content.Width - reserve * 2f), TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, centerY), fitted, ink, TextStyles.Headline);
        var hitMin = new Vector2(content.Max.X - pad - doneSize.X - DoneMargin * scale,
            centerY - Metrics.Size.TapTarget * scale * 0.5f);
        var hitMax = new Vector2(content.Max.X - pad + DoneMargin * scale,
            centerY + Metrics.Size.TapTarget * scale * 0.5f);
        var hovered = UiInteract.HoverWindowOnly(hitMin, hitMax);
        Typography.Draw(drawList, new Vector2(content.Max.X - pad - doneSize.X, centerY - doneSize.Y * 0.5f),
            doneLabel, hovered ? Palette.Lighten(accent, 0.15f) : accent, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
    }

    private static float WidgetTextWidth(string value) =>
        Windows.Widgets.WidgetText.TabularWidth(value, TextStyles.Headline);
}
