using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell.Home;

internal sealed class WidgetEditSheet
{
    private const float MinimumFraction = 0.5f;
    private const float MaximumFraction = 0.92f;
    private const float ValueFraction = 0.45f;
    private const float ChevronReserveUnits = 26f;

    private readonly Sheet sheet = new();
    private readonly HomeLayoutService layout;
    private readonly WidgetHost widgetHost;
    private readonly List<List<WidgetChoice>> choices = new();
    private HomeTile? member;
    private int optionIndex = -1;
    private float scrollY;

    public WidgetEditSheet(HomeLayoutService layout, WidgetHost widgetHost)
    {
        this.layout = layout;
        this.widgetHost = widgetHost;
    }

    public bool Active => member is not null && (sheet.IsOpen || sheet.CapturesPointer);

    public void Open(HomeTile target)
    {
        if (target.Widget is not { } widget || widget.Options.Count == 0)
        {
            return;
        }

        member = target;
        optionIndex = -1;
        scrollY = 0f;
        var options = widget.Options;
        while (choices.Count < options.Count)
        {
            choices.Add(new List<WidgetChoice>());
        }

        for (var index = 0; index < options.Count; index++)
        {
            options[index].Choices(choices[index]);
        }

        sheet.Open();
    }

    public void CloseImmediately()
    {
        sheet.CloseImmediately();
        member = null;
    }

    public void Draw(Rect screen, PhoneTheme theme, float delta)
    {
        if (member is null)
        {
            return;
        }

        var current = member;
        if (sheet.IsOpen)
        {
            if (layout.FindWidget(current.InstanceKey) is { } fresh)
            {
                member = fresh;
                current = fresh;
            }
            else
            {
                sheet.Close();
            }
        }

        var widget = current.Widget!;
        var options = widget.Options;
        var scale = UiScale.Current;
        var detents = SheetDetents.Fitted(FittedHeight(screen, current, options.Count, scale));
        var frame = sheet.Begin(ImGui.GetWindowDrawList(), screen, theme, detents, SheetMetrics.HomeVeil);
        if (!frame.Visible)
        {
            if (!sheet.IsOpen)
            {
                member = null;
            }

            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var opacity = frame.Opacity;
        var choosing = optionIndex >= 0 && optionIndex < options.Count;
        var title = choosing ? Loc.T(options[optionIndex].Label) : widget.DisplayName;
        var action = WidgetSheetKit.Header(drawList, content, title, frame.Ink, theme.Accent, opacity, scale,
            frame.Interactive, choosing);
        if (action == SheetHeaderAction.Done)
        {
            sheet.Close();
        }
        else if (action == SheetHeaderAction.Back)
        {
            optionIndex = -1;
            scrollY = 0f;
        }

        var previewTop = content.Min.Y + WidgetSheetKit.HeaderUnits * scale;
        var preview = WidgetSheetKit.Preview(content, previewTop, current.Size, scale);
        widget.Draw(widgetHost.Tile(drawList, preview, theme, current, scale, delta, opacity, false));
        var pad = WidgetSheetKit.SidePadUnits * scale;
        var listTop = preview.Max.Y + WidgetSheetKit.SectionGapUnits * scale;
        var view = new Rect(new Vector2(content.Min.X + pad, listTop),
            new Vector2(content.Max.X - pad, content.Max.Y - WidgetSheetKit.BottomInsetUnits * scale));
        if (view.Height > 1f)
        {
            if (choosing)
            {
                DrawChoices(drawList, view, current, options[optionIndex], choices[optionIndex], frame.Ink,
                    theme.Accent, opacity, scale, frame.Interactive);
            }
            else
            {
                DrawOptions(drawList, view, current, options, frame.Ink, opacity, scale, frame.Interactive);
            }
        }

        sheet.End(in frame);
    }

    private void DrawOptions(ImDrawListPtr drawList, Rect view, HomeTile current, IReadOnlyList<WidgetOption> options,
        Vector4 ink, float opacity, float scale, bool interactive)
    {
        var rowHeight = WidgetSheetKit.RowUnits * scale;
        var card = new Rect(view.Min, new Vector2(view.Max.X, view.Min.Y + rowHeight * options.Count));
        WidgetSheetKit.WheelScroll(ref scrollY, view, card.Height, scale, interactive);
        card = card.Translate(new Vector2(0f, -scrollY));
        drawList.PushClipRect(view.Min, view.Max, true);
        WidgetSheetKit.Card(drawList, card, ink, opacity, scale);
        var labelWidth = card.Width * (1f - ValueFraction) - WidgetSheetKit.RowPadUnits * scale;
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var row = new Rect(new Vector2(card.Min.X, card.Min.Y + index * rowHeight),
                new Vector2(card.Max.X, card.Min.Y + (index + 1) * rowHeight));
            if (index > 0)
            {
                WidgetSheetKit.Hairline(drawList, card, row.Min.Y, ink, opacity, scale);
            }

            var live = interactive && row.Max.Y > view.Min.Y && row.Min.Y < view.Max.Y;
            var hovered = WidgetSheetKit.HoverWash(drawList, row, ink, opacity, scale, live);
            WidgetSheetKit.Label(drawList, row, Loc.T(option.Label), WidgetSheetKit.Faded(ink, opacity), labelWidth,
                scale);
            var chevronX = row.Max.X - WidgetSheetKit.RowPadUnits * scale;
            WidgetSheetKit.Glyph(drawList, new Vector2(chevronX - 4f * scale, row.Center.Y),
                FontAwesomeIcon.ChevronRight, WidgetSheetKit.Muted(ink, opacity), scale, 11f);
            WidgetSheetKit.TrailingText(drawList, row, CurrentDisplay(current, option, choices[index]),
                WidgetSheetKit.Muted(ink, opacity), chevronX - ChevronReserveUnits * 0.6f * scale,
                card.Width * ValueFraction - ChevronReserveUnits * scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                optionIndex = index;
                scrollY = 0f;
            }
        }

        drawList.PopClipRect();
    }

    private void DrawChoices(ImDrawListPtr drawList, Rect view, HomeTile current, WidgetOption option,
        List<WidgetChoice> list, Vector4 ink, Vector4 accent, float opacity, float scale, bool interactive)
    {
        var rowHeight = WidgetSheetKit.RowUnits * scale;
        var card = new Rect(view.Min, new Vector2(view.Max.X, view.Min.Y + rowHeight * Math.Max(1, list.Count)));
        WidgetSheetKit.WheelScroll(ref scrollY, view, card.Height, scale, interactive);
        card = card.Translate(new Vector2(0f, -scrollY));
        drawList.PushClipRect(view.Min, view.Max, true);
        WidgetSheetKit.Card(drawList, card, ink, opacity, scale);
        var selected = WidgetConfig.Get(current.Config, option.Key, option.DefaultValue);
        var labelWidth = card.Width - (WidgetSheetKit.RowPadUnits + ChevronReserveUnits) * scale;
        for (var index = 0; index < list.Count; index++)
        {
            var choice = list[index];
            var row = new Rect(new Vector2(card.Min.X, card.Min.Y + index * rowHeight),
                new Vector2(card.Max.X, card.Min.Y + (index + 1) * rowHeight));
            if (row.Max.Y < view.Min.Y || row.Min.Y > view.Max.Y)
            {
                continue;
            }

            if (index > 0)
            {
                WidgetSheetKit.Hairline(drawList, card, row.Min.Y, ink, opacity, scale);
            }

            var hovered = WidgetSheetKit.HoverWash(drawList, row, ink, opacity, scale, interactive);
            WidgetSheetKit.Label(drawList, row, choice.Display, WidgetSheetKit.Faded(ink, opacity), labelWidth,
                scale);
            if (string.Equals(choice.Value, selected, StringComparison.Ordinal))
            {
                WidgetSheetKit.Check(drawList,
                    new Vector2(row.Max.X - WidgetSheetKit.RowPadUnits * scale - 5f * scale, row.Center.Y),
                    WidgetSheetKit.Faded(accent, opacity), scale);
            }

            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                Choose(current, option, choice.Value);
            }
        }

        drawList.PopClipRect();
    }

    private void Choose(HomeTile current, WidgetOption option, string value)
    {
        var stored = string.Equals(value, option.DefaultValue, StringComparison.Ordinal) ? string.Empty : value;
        layout.SetWidgetConfig(current, WidgetConfig.Set(current.Config, option.Key, stored));
        optionIndex = -1;
        scrollY = 0f;
    }

    private static string CurrentDisplay(HomeTile current, WidgetOption option, List<WidgetChoice> list)
    {
        var value = WidgetConfig.Get(current.Config, option.Key, option.DefaultValue);
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].Value, value, StringComparison.Ordinal))
            {
                return list[index].Display;
            }
        }

        return string.Empty;
    }

    private static float FittedHeight(Rect screen, HomeTile current, int rows, float scale)
    {
        var height = (SheetMetrics.GrabberZone + WidgetSheetKit.HeaderUnits + WidgetSheetKit.SectionGapUnits +
                      WidgetSheetKit.BottomInsetUnits + WidgetSheetKit.RowUnits * rows) * scale +
                     WidgetSheetKit.PreviewHeight(screen, current.Size, scale);
        return Math.Clamp(height, screen.Height * MinimumFraction, screen.Height * MaximumFraction);
    }
}
