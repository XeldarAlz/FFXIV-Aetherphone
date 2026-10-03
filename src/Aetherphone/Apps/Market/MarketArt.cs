using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Market;

internal static class MarketArt
{
    public const float RowHeight = 64f;
    public const float CompactRowHeight = 58f;
    public const float SectionHeaderHeight = 40f;
    public const float SectionGap = 22f;
    public const float HeaderGap = 6f;
    public const float IconSize = 38f;
    public const float TextGap = 12f;
    public const float ValueGap = 10f;
    public const float LineGap = 2f;
    public const float SparkWidth = 58f;
    public const float SparkHeight = 26f;
    public const float PillHeight = 22f;
    public const float PillMinWidth = 62f;

    private const float IconInset = 3f;
    private const float IconBackingAlpha = 0.08f;
    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
    private const float PillPadX = 8f;
    private const float SparkLine = 1.6f;
    private const float SparkFillAlpha = 0.20f;
    private const float BaselineDash = 2f;
    private const float BaselineGap = 3f;
    private const float BaselineAlpha = 0.35f;
    private const float BadgePadX = 5f;
    private const float BadgePadY = 1.5f;
    private const float StateTileSize = 48f;
    private const float StateGlyphSize = 22f;
    private const float StateGap = 14f;
    private const float StateLineGap = 4f;
    private const float ScreenTileSize = 68f;
    private const float ScreenGlyphSize = 30f;
    private const float ScreenTitleGap = 18f;
    private const float ScreenHintGap = 6f;
    private const float ScreenMaxText = 280f;
    private const float ScreenTextInset = 48f;
    private const float ScreenLift = 36f;

    public static readonly Vector4 HqInk = new(0.98f, 0.78f, 0.30f, 1f);
    private static readonly Vector4 PillText = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HqBadgeText = new(0.12f, 0.09f, 0.02f, 1f);

    public static void Card(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale) =>
        ui.Card(drawList, min, max, Metrics.Radius.Widget * scale, true);

    public static Vector4 UpInk(PhoneTheme theme) => theme.ToggleOn;

    public static Vector4 DownInk(PhoneTheme theme) => theme.Danger;

    public static Vector4 TrendInk(PhoneTheme theme, double change, Vector4 flat) =>
        change > 0d ? UpInk(theme) : change < 0d ? DownInk(theme) : flat;

    public static float SectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title, Vector4 ink,
        float trailingReserve, float scale)
    {
        var height = SectionHeaderHeight * scale;
        var fitted = Typography.FitText(title, MathF.Max(1f, width - trailingReserve), TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + (height - size.Y) * 0.5f), fitted, ink,
            TextStyles.Title3);
        return height;
    }

    public static bool RowInteraction(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
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

    public static void ItemIcon(ImDrawListPtr drawList, ITextureProvider textures, AppSkin ui, uint iconId,
        Vector2 min, float size, float scale)
    {
        var max = min + new Vector2(size, size);
        var radius = size * Metrics.Radius.TileFactor;
        var drawn = GameIconTile.Draw(drawList, textures, iconId, min, max, radius, scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, IconBackingAlpha)), IconInset * scale,
            requireIcon: false);
        if (drawn)
        {
            return;
        }

        ProgressRing.CenterIcon(drawList, (min + max) * 0.5f, FontAwesomeIcon.Coins, ui.MutedInk, size * 0.45f);
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

        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + LineGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + LineGap * scale), fittedSubtitle,
            subtitleInk, TextStyles.Footnote);
    }

    public static float PriceWidth(string value, in TextStyle style) => WidgetText.TabularWidth(value, style);

    public static float Price(ImDrawListPtr drawList, float right, float top, string value, Vector4 ink,
        in TextStyle style)
    {
        if (value.Length == 0)
        {
            return 0f;
        }

        return WidgetText.TabularRight(drawList, right, top, value, ink, style);
    }

    public static float PillWidth(string text, float scale) =>
        MathF.Max(PillMinWidth * scale, Typography.Measure(text, TextStyles.FootnoteEmphasized).X + PillPadX * 2f * scale);

    public static void ChangePill(ImDrawListPtr drawList, Vector2 min, float width, string text, Vector4 fill,
        float scale)
    {
        var height = PillHeight * scale;
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(fill));
        var textHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var textWidth = WidgetText.TabularWidth(text, TextStyles.FootnoteEmphasized);
        WidgetText.Tabular(drawList, new Vector2(max.X - PillPadX * scale - textWidth, min.Y + (height - textHeight) * 0.5f),
            text, PillText, TextStyles.FootnoteEmphasized);
    }

    public static float HqBadge(ImDrawListPtr drawList, Vector2 leftCenter, float scale)
    {
        var label = Loc.T(L.Common.Hq);
        var size = Typography.Measure(label, TextStyles.Caption2);
        var min = new Vector2(leftCenter.X, leftCenter.Y - size.Y * 0.5f - BadgePadY * scale);
        var max = new Vector2(min.X + size.X + BadgePadX * 2f * scale, leftCenter.Y + size.Y * 0.5f + BadgePadY * scale);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(HqInk), (max.Y - min.Y) * 0.5f);
        Typography.Draw(drawList, new Vector2(min.X + BadgePadX * scale, min.Y + BadgePadY * scale), label,
            HqBadgeText, TextStyles.Caption2);
        return max.X - min.X;
    }

    public static void Sparkline(ImDrawListPtr drawList, Rect area, ReadOnlySpan<float> values, float baseline,
        Vector4 ink, float scale)
    {
        var count = values.Length;
        if (count < 2 || area.Width <= 2f || area.Height <= 2f)
        {
            return;
        }

        var low = values[0];
        var high = values[0];
        for (var index = 1; index < count; index++)
        {
            low = MathF.Min(low, values[index]);
            high = MathF.Max(high, values[index]);
        }

        if (baseline > 0f)
        {
            low = MathF.Min(low, baseline);
            high = MathF.Max(high, baseline);
        }

        var range = high - low;
        var inset = SparkLine * scale;
        var usable = area.Height - inset * 2f;
        var stepX = area.Width / (count - 1);
        Span<Vector2> points = stackalloc Vector2[count];
        for (var index = 0; index < count; index++)
        {
            var normalized = range <= 0f ? 0.5f : (values[index] - low) / range;
            points[index] = new Vector2(area.Min.X + stepX * index, area.Max.Y - inset - normalized * usable);
        }

        GradientFill(drawList, points, area.Max.Y, ink with { W = SparkFillAlpha }, ink with { W = 0f });
        if (baseline > 0f && range > 0f)
        {
            var baselineY = area.Max.Y - inset - (baseline - low) / range * usable;
            DashedLine(drawList, area.Min.X, area.Max.X, baselineY, Palette.WithAlpha(ink, BaselineAlpha), scale);
        }

        var line = ImGui.GetColorU32(ink);
        for (var index = 0; index < count - 1; index++)
        {
            drawList.AddLine(points[index], points[index + 1], line, SparkLine * scale);
        }
    }

    public static void GradientFill(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, float baseY, Vector4 top,
        Vector4 bottom)
    {
        if (points.Length < 2)
        {
            return;
        }

        var topColor = ImGui.GetColorU32(top);
        var bottomColor = ImGui.GetColorU32(bottom);
        var firstVertex = drawList.VtxBuffer.Size;
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        for (var index = 0; index < points.Length - 1; index++)
        {
            var left = points[index];
            var right = points[index + 1];
            drawList.PathLineTo(left);
            drawList.PathLineTo(right);
            drawList.PathLineTo(new Vector2(right.X, baseY));
            drawList.PathLineTo(new Vector2(left.X, baseY));
            drawList.PathFillConvex(topColor);
        }

        drawList.Flags = flags;
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            if (vertices[index].Pos.Y >= baseY)
            {
                vertices[index].Col = bottomColor;
            }
        }
    }

    public static void DashedLine(ImDrawListPtr drawList, float left, float right, float y, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var dash = BaselineDash * scale;
        var gap = BaselineGap * scale;
        for (var dashStart = left; dashStart < right; dashStart += dash + gap)
        {
            drawList.AddLine(new Vector2(dashStart, y), new Vector2(MathF.Min(right, dashStart + dash), y), color,
                Metrics.Stroke.Hairline);
        }
    }

    public static float PanelHeight(string title, string body, float width, float scale)
    {
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(StateTileSize * scale, titleHeight + StateLineGap * scale + bodyHeight) +
               Metrics.Space.Lg * 2f * scale;
    }

    public static void Panel(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float height,
        FontAwesomeIcon icon, string title, string body, float scale)
    {
        Card(drawList, ui, origin, new Vector2(origin.X + width, origin.Y + height), scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = StateTileSize * scale;
        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var textLeft = tileMax.X + StateGap * scale;
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), title, ui.TitleInk,
            TextStyles.Headline, textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + StateLineGap * scale), body,
            ui.MutedInk, TextStyles.Subheadline, textWidth);
    }

    public static void StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, FontAwesomeIcon icon, string title,
        string hint, float scale)
    {
        var centerX = body.Center.X;
        var tileSize = ScreenTileSize * scale;
        var tileTop = body.Center.Y - ScreenLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, ScreenGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - ScreenTextInset * scale, ScreenMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + ScreenTitleGap * scale), maxWidth);
        Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(centerX, titleBottom + ScreenHintGap * scale), maxWidth);
    }

    private static float PanelTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Space.Lg * 2f * scale - (StateTileSize + StateGap) * scale);
}
