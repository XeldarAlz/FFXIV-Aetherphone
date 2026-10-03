using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Wallet;

internal static class WalletArt
{
    public const float RowHeight = 62f;
    public const float SectionHeaderHeight = 40f;
    public const float SectionGap = 22f;
    public const float HeaderGap = 6f;
    public const float DialSize = 42f;
    public const float TextGap = 12f;
    public const float ValueGap = 10f;
    public const float LineGap = 2f;

    public static readonly Vector4 GoldInk = new(0.98f, 0.78f, 0.30f, 1f);
    public static readonly Vector4 GainInk = new(0.30f, 0.84f, 0.46f, 1f);

    private const float DialThickness = 2.8f;
    private const float DialTrackAlpha = 0.16f;
    private const float BackingInset = 3.5f;
    private const float IconFill = 0.82f;
    private const float BackingAlpha = 0.07f;
    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
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
    private const float ChartLine = 2.2f;
    private const float ChartFillAlpha = 0.16f;
    private const float ChartDot = 3.6f;
    private const float ChartGuideAlpha = 0.08f;

    public static void Card(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale) =>
        ui.Card(drawList, min, max, Metrics.Radius.Widget * scale, true);

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

    public static Vector4 LevelInk(CapLevel level, Vector4 accent) =>
        level is CapLevel.Near or CapLevel.Full ? GoldInk : accent;

    public static void Dial(ImDrawListPtr drawList, ITextureProvider textures, uint iconId, Vector2 center,
        float size, float fraction, Vector4 tint, bool showRing, Vector4 backing, float scale)
    {
        var radius = size * 0.5f;
        var thickness = DialThickness * scale * (size / (DialSize * scale));
        if (showRing)
        {
            var ringRadius = radius - thickness * 0.5f;
            ProgressRing.Track(drawList, center, ringRadius, thickness, Palette.WithAlpha(tint, DialTrackAlpha));
            ProgressRing.Fill(drawList, center, ringRadius, thickness, Math.Clamp(fraction, 0f, 1f), tint);
        }

        var backingRadius = showRing ? radius - thickness - BackingInset * scale : radius;
        drawList.AddCircleFilled(center, backingRadius, ImGui.GetColorU32(backing), 32);
        Icon(drawList, textures, iconId, center, backingRadius * 2f * IconFill);
    }

    public static Vector4 Backing(Vector4 titleInk) => Palette.WithAlpha(titleInk, BackingAlpha);

    public static void Icon(ImDrawListPtr drawList, ITextureProvider textures, uint iconId, Vector2 center,
        float size)
    {
        if (iconId == 0)
        {
            return;
        }

        var wrap = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
        var half = new Vector2(size * 0.5f, size * 0.5f);
        drawList.AddImage(wrap.Handle, center - half, center + half);
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

    public static float Value(ImDrawListPtr drawList, float right, float centerY, string value, Vector4 ink)
    {
        if (value.Length == 0)
        {
            return 0f;
        }

        var width = WidgetTextWidth(value);
        var height = Typography.LineHeight(TextStyles.Headline);
        WidgetText.Tabular(drawList, new Vector2(right - width, centerY - height * 0.5f), value, ink,
            TextStyles.Headline);
        return width;
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

    public static void Bar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float fraction, Vector4 track,
        Vector4 fill)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(track), radius);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var width = MathF.Max(max.Y - min.Y, (max.X - min.X) * clamped);
        drawList.AddRectFilled(min, new Vector2(min.X + width, max.Y), ImGui.GetColorU32(fill), radius);
    }

    public static void Chart(ImDrawListPtr drawList, Rect area, ReadOnlySpan<long> values, Vector4 line,
        Vector4 guide, float scale)
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
            low = Math.Min(low, values[index]);
            high = Math.Max(high, values[index]);
        }

        var range = (double)(high - low);
        var inset = ChartDot * scale;
        var usable = area.Height - inset * 2f;
        var stepX = (area.Width - inset * 2f) / (count - 1);
        Span<Vector2> points = stackalloc Vector2[count];
        for (var index = 0; index < count; index++)
        {
            var normalized = range <= 0d ? 0.5f : (float)((values[index] - low) / range);
            points[index] = new Vector2(area.Min.X + inset + stepX * index, area.Max.Y - inset - normalized * usable);
        }

        var guideColor = ImGui.GetColorU32(Palette.WithAlpha(guide, ChartGuideAlpha));
        drawList.AddLine(new Vector2(area.Min.X, area.Max.Y), area.Max, guideColor, Metrics.Stroke.Hairline);
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), guideColor, Metrics.Stroke.Hairline);
        var fill = ImGui.GetColorU32(Palette.WithAlpha(line, ChartFillAlpha));
        for (var index = 0; index < count - 1; index++)
        {
            var left = points[index];
            var right = points[index + 1];
            drawList.AddQuadFilled(left, right, new Vector2(right.X, area.Max.Y), new Vector2(left.X, area.Max.Y),
                fill);
        }

        var lineColor = ImGui.GetColorU32(line);
        for (var index = 0; index < count - 1; index++)
        {
            drawList.AddLine(points[index], points[index + 1], lineColor, ChartLine * scale);
        }

        drawList.AddCircleFilled(points[count - 1], ChartDot * scale, lineColor, 16);
    }

    private static float WidgetTextWidth(string value) =>
        WidgetText.TabularWidth(value, TextStyles.Headline);

    private static float PanelTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Space.Lg * 2f * scale - (StateTileSize + StateGap) * scale);
}
