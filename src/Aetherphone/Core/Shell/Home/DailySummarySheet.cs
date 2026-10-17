using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class DailySummarySheet
{
    private const float SheetTopUnits = 48f;
    private const float RowUnits = 58f;
    private const float IconUnits = 34f;
    private const float IconGapUnits = 12f;

    private readonly Sheet sheet = new();
    private readonly WidgetActions actions;
    private readonly DailySummary summary;

    public DailySummarySheet(WidgetActions actions)
    {
        this.actions = actions;
        summary = new DailySummary(actions.Services);
    }

    public bool Active => sheet.CapturesPointer;

    public void Open() => sheet.Open();

    public void CloseImmediate() => sheet.CloseImmediately();

    public void Draw(Rect screen, PhoneTheme theme)
    {
        if (!sheet.CapturesPointer)
        {
            return;
        }

        summary.Refresh();
        var scale = UiScale.Current;
        var rows = Math.Max(1, summary.Count);
        var height = (SheetMetrics.GrabberZone + WidgetSheetKit.HeaderUnits + rows * RowUnits +
                      WidgetSheetKit.BottomInsetUnits) * scale;
        var detents = SheetDetents.Fitted(Math.Clamp(height, 1f, screen.Height - SheetTopUnits * scale));
        var drawList = ImGui.GetWindowDrawList();
        var frame = sheet.Begin(drawList, screen, theme, detents, SheetMetrics.HomeVeil);
        if (!frame.Visible)
        {
            return;
        }

        var content = frame.Content;
        if (WidgetSheetKit.Header(drawList, content, Loc.T(L.WidgetsPeople.DailySummary), frame.Ink, theme.Accent,
                frame.Opacity, scale, frame.Interactive, false) == SheetHeaderAction.Done ||
            frame.Interactive && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            sheet.Close();
        }

        var pad = WidgetSheetKit.SidePadUnits * scale;
        var top = content.Min.Y + WidgetSheetKit.HeaderUnits * scale;
        var rowHeight = RowUnits * scale;
        var card = new Rect(new Vector2(content.Min.X + pad, top), new Vector2(content.Max.X - pad, top + rows * rowHeight));
        WidgetSheetKit.Card(drawList, card, frame.Ink, frame.Opacity, scale);
        var items = summary.Items;
        if (items.Length == 0)
        {
            Typography.DrawCentered(drawList, card.Center, Loc.T(L.WidgetsPeople.CaughtUp),
                WidgetSheetKit.Muted(frame.Ink, frame.Opacity), TextStyles.Body);
        }

        for (var index = 0; index < items.Length; index++)
        {
            var row = new Rect(new Vector2(card.Min.X, top + index * rowHeight),
                new Vector2(card.Max.X, top + (index + 1) * rowHeight));
            if (index > 0)
            {
                WidgetSheetKit.Hairline(drawList, card, row.Min.Y, frame.Ink, frame.Opacity, scale);
            }

            if (DrawRow(drawList, row, items[index], frame, scale))
            {
                actions.Open(items[index].Route, row);
                sheet.CloseImmediately();
            }
        }

        sheet.End(in frame);
    }

    private static bool DrawRow(ImDrawListPtr drawList, Rect row, in DailySummaryItem item, in SheetFrame frame,
        float scale)
    {
        var hovered = WidgetSheetKit.HoverWash(drawList, row, frame.Ink, frame.Opacity, scale, frame.Interactive);
        var iconSize = IconUnits * scale;
        var iconLeft = row.Min.X + WidgetSheetKit.RowPadUnits * scale;
        IconTile.DrawApp(drawList, item.AppId, new Vector2(iconLeft + iconSize * 0.5f, row.Center.Y), iconSize,
            AppAccents.For(item.AppId), frame.Opacity);
        var textLeft = iconLeft + iconSize + IconGapUnits * scale;
        var width = MathF.Max(1f, row.Max.X - WidgetSheetKit.RowPadUnits * scale - textLeft);
        var name = Typography.FitText(item.Name, width, TextStyles.Headline);
        var text = Typography.FitText(item.Text, width, TextStyles.Subheadline);
        var nameHeight = Typography.Measure(name, TextStyles.Headline).Y;
        var textHeight = Typography.Measure(text, TextStyles.Subheadline).Y;
        var nameTop = row.Center.Y - (nameHeight + textHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, nameTop), name, WidgetSheetKit.Faded(frame.Ink, frame.Opacity),
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, nameTop + nameHeight), text,
            WidgetSheetKit.Muted(frame.Ink, frame.Opacity), TextStyles.Subheadline);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }
}
