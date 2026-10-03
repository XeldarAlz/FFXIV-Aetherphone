using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct ActionSheetStyle(
    Vector4 Panel,
    Vector4 Stroke,
    Vector4 Ink,
    Vector4 Danger,
    Vector4 Accent,
    Vector4 Hairline,
    PhoneTheme? Theme = null)
{
    public static ActionSheetStyle From(PhoneTheme theme) => new(
        Palette.WithAlpha(Palette.Lighten(theme.AppBackground, 0.10f), 0.92f),
        Palette.WithAlpha(theme.TextStrong, 0.12f),
        theme.TextStrong,
        theme.Danger,
        theme.Accent,
        theme.Hairline,
        theme);

    public static ActionSheetStyle From(AppSkin ui) => new(
        Palette.WithAlpha(Palette.Lighten(ui.Palette.BackdropTop, 0.10f), 0.92f),
        Palette.WithAlpha(ui.TitleInk, 0.12f),
        ui.TitleInk,
        ui.Theme.Danger,
        ui.Accent,
        ui.Hairline,
        ui.Theme);
}

internal sealed class ActionSheet
{
    public readonly record struct Item(string Label, string Glyph = "", bool Danger = false, bool Selected = false,
        bool Checkable = false);

    private const float RowHeight = 50f;
    private const float CancelHeight = 52f;
    private const float CancelGap = 8f;
    private const float BottomInset = Metrics.Size.HomeIndicatorInset;
    private const float RowInset = 10f;
    private const float PadX = 18f;
    private const float GlyphReserve = 30f;
    private const float CheckReserve = 26f;
    private const float HeaderPadY = 13f;
    private const float HeaderInkAlpha = 0.78f;
    private const float RowHoverAlpha = 0.07f;

    private static readonly TextStyle RowStyle = new(1.07f, FontWeight.SemiBold);
    private static readonly TextStyle CancelStyle = new(1.07f, FontWeight.Bold);
    private static readonly TextStyle HeaderStyle = new(1.02f, FontWeight.SemiBold);

    private readonly Sheet sheet = new();

    public bool IsOpen => sheet.IsOpen;

    public bool CapturesPointer => sheet.CapturesPointer;

    public void Open() => sheet.Open();

    public void Close() => sheet.Close();

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public int Draw(Rect screen, in ActionSheetStyle style, ReadOnlySpan<Item> items, string cancelLabel,
        bool keepOpen, string title = "")
    {
        if (items.Length == 0)
        {
            sheet.CloseImmediately();
            return -1;
        }

        var theme = style.Theme ?? PhoneTheme.Default;
        var scale = UiScale.Current;
        var rowHeight = RowHeight * scale;
        var cancelHeight = CancelHeight * scale;
        var padX = PadX * scale;
        var headerWidth = MathF.Max(1f, screen.Width - (RowInset + PadX) * 2f * scale);
        var titleHeight = title.Length > 0 ? Typography.MeasureWrappedBlock(title, HeaderStyle, headerWidth).Y : 0f;
        var headerHeight = titleHeight > 0f ? titleHeight + HeaderPadY * 2f * scale : 0f;
        var fittedHeight = SheetMetrics.GrabberZone * scale + headerHeight + items.Length * rowHeight +
                           CancelGap * scale + cancelHeight + BottomInset * scale;
        var frame = sheet.Begin(ImGui.GetForegroundDrawList(), screen, theme, SheetDetents.Fitted(fittedHeight),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return -1;
        }

        var drawList = frame.DrawList;
        var opacity = frame.Opacity;
        var content = frame.Content;
        var left = content.Min.X + RowInset * scale;
        var right = content.Max.X - RowInset * scale;
        if (headerHeight > 0f)
        {
            var headerInk = Palette.WithAlpha(style.Ink, style.Ink.W * HeaderInkAlpha * opacity);
            Typography.DrawWrappedCentered(drawList,
                new Vector2(content.Center.X, content.Min.Y + HeaderPadY * scale + titleHeight * 0.5f), title,
                headerInk, HeaderStyle, headerWidth);
        }

        var anyGlyph = false;
        var anyCheck = false;
        for (var index = 0; index < items.Length; index++)
        {
            anyGlyph |= items[index].Glyph.Length > 0;
            anyCheck |= items[index].Checkable;
        }

        var hairline = ImGui.GetColorU32(Palette.WithAlpha(style.Hairline, style.Hairline.W * opacity));
        var hoverFill = ImGui.GetColorU32(Palette.WithAlpha(style.Ink, RowHoverAlpha * opacity));
        var rowsTop = content.Min.Y + headerHeight;
        var picked = -1;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var rowMin = new Vector2(left, rowsTop + index * rowHeight);
            var rowMax = new Vector2(right, rowMin.Y + rowHeight);
            var hovered = frame.Interactive && UiInteract.HoverWindowOnly(rowMin, rowMax);
            if (hovered)
            {
                Squircle.Fill(drawList, rowMin, rowMax, Metrics.Radius.Md * scale, hoverFill);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (index > 0 || headerHeight > 0f)
            {
                drawList.AddLine(new Vector2(rowMin.X + padX, rowMin.Y), new Vector2(rowMax.X - padX, rowMin.Y),
                    hairline, Metrics.Stroke.Hairline);
            }

            var ink = item.Danger ? style.Danger : item.Checkable && item.Selected ? style.Accent : style.Ink;
            var faded = Palette.WithAlpha(ink, ink.W * opacity);
            var centerY = (rowMin.Y + rowMax.Y) * 0.5f;
            var textLeft = rowMin.X + padX;
            if (anyGlyph)
            {
                if (item.Glyph.Length > 0)
                {
                    AppSkin.Icon(drawList, new Vector2(textLeft + 9f * scale, centerY), item.Glyph, faded, 0.95f);
                }

                textLeft += GlyphReserve * scale;
            }

            var textRight = rowMax.X - padX - (anyCheck ? CheckReserve * scale : 0f);
            var label = Typography.FitText(item.Label, MathF.Max(1f, textRight - textLeft), RowStyle);
            var labelSize = Typography.Measure(label, RowStyle);
            var labelX = anyGlyph || anyCheck ? textLeft : (rowMin.X + rowMax.X - labelSize.X) * 0.5f;
            Typography.Draw(drawList, new Vector2(labelX, centerY - labelSize.Y * 0.5f), label, faded, RowStyle);
            if (item.Checkable && item.Selected)
            {
                DrawCheck(drawList, new Vector2(rowMax.X - padX - 6f * scale, centerY), style.Accent, opacity, scale);
            }

            if (UiInteract.Click(rowMin, rowMax, hovered))
            {
                picked = index;
            }
        }

        var cancelMin = new Vector2(left, rowsTop + items.Length * rowHeight + CancelGap * scale);
        var cancelMax = new Vector2(right, cancelMin.Y + cancelHeight);
        drawList.AddLine(new Vector2(left + padX, cancelMin.Y - CancelGap * scale * 0.5f),
            new Vector2(right - padX, cancelMin.Y - CancelGap * scale * 0.5f), hairline, Metrics.Stroke.Hairline);
        var cancelHovered = frame.Interactive && UiInteract.HoverWindowOnly(cancelMin, cancelMax);
        if (cancelHovered)
        {
            Squircle.Fill(drawList, cancelMin, cancelMax, Metrics.Radius.Md * scale, hoverFill);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, new Vector2((cancelMin.X + cancelMax.X) * 0.5f,
                (cancelMin.Y + cancelMax.Y) * 0.5f), cancelLabel, Palette.WithAlpha(style.Ink, style.Ink.W * opacity),
            CancelStyle);
        var cancelClicked = UiInteract.Click(cancelMin, cancelMax, cancelHovered);
        sheet.End(in frame);

        if (picked >= 0)
        {
            if (!keepOpen)
            {
                Close();
            }

            return picked;
        }

        if (cancelClicked)
        {
            Close();
        }

        return -1;
    }

    private static void DrawCheck(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float alpha, float scale)
    {
        var color = ImGui.GetColorU32(Palette.WithAlpha(accent, accent.W * alpha));
        var thickness = 2f * scale;
        drawList.AddLine(center + new Vector2(-5f * scale, 0f), center + new Vector2(-1.5f * scale, 3.6f * scale),
            color, thickness);
        drawList.AddLine(center + new Vector2(-1.5f * scale, 3.6f * scale),
            center + new Vector2(5.2f * scale, -4f * scale), color, thickness);
    }
}
