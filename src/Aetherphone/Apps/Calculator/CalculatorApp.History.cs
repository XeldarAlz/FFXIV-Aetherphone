using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Calculator;

internal sealed partial class CalculatorApp
{
    private const ImGuiWindowFlags SheetHostFlags = ImGuiWindowFlags.NoScrollbar |
                                                    ImGuiWindowFlags.NoScrollWithMouse |
                                                    ImGuiWindowFlags.NoBackground;

    private const float SheetHeaderHeight = 54f;
    private const float SheetPadX = 20f;
    private const float SectionHeight = 34f;
    private const float HistoryRowHeight = 62f;
    private const float HistoryLineGap = 2f;
    private const float RemoveRadius = 15f;
    private const float RemoveGlyphScale = 0.78f;
    private const float RowHoverAlpha = 0.07f;
    private const float HairlineAlpha = 0.10f;
    private const float MutedAlpha = 0.62f;
    private const float EmptyTileRadius = 34f;
    private const float EmptyGlyphScale = 1.7f;
    private const float EmptyTitleGap = 18f;
    private const float EmptyBodyGap = 6f;
    private const float EmptyBodyMaxWidth = 280f;
    private const float EmptyTileAlpha = 0.10f;
    private const int RecentWeekDays = 6;

    private readonly Sheet historySheet = new();
    private readonly List<HistoryRow> historyRows = new();
    private int historyRowsVersion = -1;
    private DateTime historyRowsDay;
    private CultureInfo? historyRowsCulture;

    private readonly record struct HistoryRow(int RecordIndex, string Label, string Expression, string Result,
        string Time);

    private void OpenHistory()
    {
        menuOpen = false;
        historyRowsVersion = -1;
        historySheet.Open();
    }

    private void RefreshHistoryRows()
    {
        var today = DateTime.Now.Date;
        var culture = Loc.Culture;
        if (historyRowsVersion == engine.HistoryVersion && historyRowsDay == today &&
            ReferenceEquals(historyRowsCulture, culture))
        {
            return;
        }

        historyRowsVersion = engine.HistoryVersion;
        historyRowsDay = today;
        historyRowsCulture = culture;
        historyRows.Clear();
        var history = engine.History;
        var currentDay = DateTime.MinValue;
        for (var index = 0; index < history.Count; index++)
        {
            var record = history[index];
            var solved = DateTimeOffset.FromUnixTimeSeconds(record.SolvedAtUnix).ToLocalTime().DateTime;
            if (solved.Date != currentDay)
            {
                currentDay = solved.Date;
                historyRows.Add(new HistoryRow(-1, DayLabel(currentDay, today, culture), string.Empty, string.Empty,
                    string.Empty));
            }

            historyRows.Add(new HistoryRow(index, string.Empty, Localize(record.Expression), Localize(record.Result),
                TimeText.Clock(solved)));
        }
    }

    private static string DayLabel(DateTime day, DateTime today, CultureInfo culture)
    {
        if (day == today)
        {
            return Loc.T(L.Time.Today);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Time.Yesterday);
        }

        if (day > today.AddDays(-RecentWeekDays))
        {
            return culture.TextInfo.ToTitleCase(day.ToString("dddd", culture));
        }

        return day.Year == today.Year ? day.ToString("M", culture) : day.ToString("D", culture);
    }

    private void DrawHistorySheet(Rect screen, PhoneTheme theme)
    {
        if (!historySheet.CapturesPointer)
        {
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##calculatorHistory", screen.Size, false, SheetHostFlags))
        {
            var frame = historySheet.Begin(ImGui.GetWindowDrawList(), screen, theme,
                SheetDetents.Standard(screen.Height), SheetMetrics.AppVeil);
            if (!frame.Visible)
            {
                return;
            }

            DrawHistoryBody(in frame);
            historySheet.End(in frame);
        }
    }

    private void DrawHistoryBody(in SheetFrame frame)
    {
        RefreshHistoryRows();
        var scale = UiScale.Current;
        var content = frame.Content;
        var drawList = frame.DrawList;
        var ink = Palette.WithAlpha(frame.Ink, frame.Ink.W * frame.Opacity);
        var muted = Palette.WithAlpha(ink, ink.W * MutedAlpha);
        var headerHeight = SheetHeaderHeight * scale;
        var headerCenterY = content.Min.Y + headerHeight * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, headerCenterY), Loc.T(L.Calculator.History),
            ink, TextStyles.Headline);
        var clearRequested = engine.History.Count > 0 && DrawClearButton(drawList, content, headerCenterY,
            frame.Interactive, frame.Opacity, scale);
        var list = new Rect(new Vector2(content.Min.X, content.Min.Y + headerHeight),
            new Vector2(content.Max.X, content.Max.Y - Metrics.Size.HomeIndicatorInset * scale));
        if (list.Height <= 0f)
        {
            return;
        }

        if (historyRows.Count == 0)
        {
            DrawHistoryEmpty(drawList, list, ink, muted, scale);
            return;
        }

        drawList.AddLine(new Vector2(list.Min.X + SheetPadX * scale, list.Min.Y),
            new Vector2(list.Max.X - SheetPadX * scale, list.Min.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ink, HairlineAlpha * frame.Opacity)), Metrics.Stroke.Hairline);
        var picked = -1;
        var removed = -1;
        using (UiScope.PushId("calculator.history"))
        using (AppSurface.BeginEdgeToEdge(list))
        using (UiScope.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            var rowsDrawList = ImGui.GetWindowDrawList();
            for (var index = 0; index < historyRows.Count; index++)
            {
                var row = historyRows[index];
                if (row.RecordIndex < 0)
                {
                    DrawHistorySection(rowsDrawList, row.Label, muted, scale);
                    continue;
                }

                var action = DrawHistoryRow(rowsDrawList, row, ink, muted, frame.Interactive, scale);
                if (action == HistoryRowAction.Recall)
                {
                    picked = row.RecordIndex;
                }
                else if (action == HistoryRowAction.Remove)
                {
                    removed = row.RecordIndex;
                }
            }

            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, Metrics.Space.Lg * scale));
        }

        if (clearRequested)
        {
            AskClearHistory();
            return;
        }

        var history = engine.History;
        if (removed >= 0 && removed < history.Count)
        {
            engine.Remove(history[removed]);
            return;
        }

        if (picked < 0 || picked >= history.Count)
        {
            return;
        }

        engine.Recall(history[picked]);
        historySheet.Close();
    }

    private bool DrawClearButton(ImDrawListPtr drawList, Rect content, float centerY, bool interactive,
        float opacity, float scale)
    {
        var label = Loc.T(L.Calculator.Clear);
        var size = Typography.Measure(label, TextStyles.Body);
        var right = content.Max.X - SheetPadX * scale;
        var halfHit = Metrics.Size.TapTarget * scale * 0.5f;
        var min = new Vector2(right - size.X - Metrics.Space.Sm * scale, centerY - halfHit);
        var max = new Vector2(right + Metrics.Space.Sm * scale, centerY + halfHit);
        var hovered = interactive && UiInteract.HoverWindowOnly(min, max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var accent = ui.Accent with { W = ui.Accent.W * opacity * (down ? DisabledAlpha : 1f) };
        Typography.Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), label, accent,
            TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return interactive && UiInteract.Click(min, max, hovered);
    }

    private static void DrawHistorySection(ImDrawListPtr drawList, string label, Vector4 muted, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = SectionHeight * scale;
        ImGui.Dummy(new Vector2(width, height));
        var padX = SheetPadX * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, width - padX * 2f), TextStyles.FootnoteEmphasized);
        var textHeight = Typography.Measure(fitted, TextStyles.FootnoteEmphasized).Y;
        Typography.Draw(drawList, new Vector2(origin.X + padX, origin.Y + height - textHeight - Metrics.Space.Xs * scale),
            fitted, muted, TextStyles.FootnoteEmphasized);
    }

    private enum HistoryRowAction : byte
    {
        None,
        Recall,
        Remove,
    }

    private HistoryRowAction DrawHistoryRow(ImDrawListPtr drawList, in HistoryRow row, Vector4 ink, Vector4 muted,
        bool interactive, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = HistoryRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ImGui.Dummy(new Vector2(width, height));
        if (!ImGui.IsRectVisible(origin, max))
        {
            return HistoryRowAction.None;
        }

        var padX = SheetPadX * scale;
        var hovered = interactive && UiInteract.HoverWindowOnly(origin, max);
        var inset = new Vector2(Metrics.Space.Sm * scale, 0f);
        if (hovered)
        {
            Squircle.Fill(drawList, origin + inset, max - inset, Metrics.Radius.Md * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ink, RowHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var removeRadius = RemoveRadius * scale;
        var removeCenter = new Vector2(max.X - padX - removeRadius, origin.Y + height * 0.5f);
        var trailing = removeRadius * 2f + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, width - padX * 2f - trailing);
        var expression = Typography.FitText(row.Expression, textWidth, TextStyles.Footnote);
        var expressionHeight = Typography.Measure(expression, TextStyles.Footnote).Y;
        var result = Typography.FitText(row.Result, textWidth, NumberStyle);
        var resultHeight = Typography.Measure(result, NumberStyle).Y;
        var blockTop = origin.Y + (height - expressionHeight - resultHeight - HistoryLineGap * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X + padX, blockTop), expression, muted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(origin.X + padX, blockTop + expressionHeight + HistoryLineGap * scale),
            result, ink, NumberStyle);

        var overRemove = false;
        if (hovered)
        {
            var offset = ImGui.GetMousePos() - removeCenter;
            overRemove = offset.LengthSquared() <= removeRadius * removeRadius;
            var wash = overRemove ? ui.Theme.Danger : ink;
            drawList.AddCircleFilled(removeCenter, removeRadius,
                ImGui.GetColorU32(Palette.WithAlpha(wash, overRemove ? EmptyTileAlpha * 2f : EmptyTileAlpha)), 24);
            AppSkin.Icon(drawList, removeCenter, IconGlyph.Of(FontAwesomeIcon.TrashAlt),
                overRemove ? ui.Theme.Danger : muted, RemoveGlyphScale);
            HoverTooltip.Show(new Rect(removeCenter - new Vector2(removeRadius, removeRadius),
                removeCenter + new Vector2(removeRadius, removeRadius)), Loc.T(L.Calculator.RemoveEntry));
        }
        else
        {
            var timeSize = Typography.Measure(row.Time, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(max.X - padX - timeSize.X, origin.Y + (height - timeSize.Y) * 0.5f),
                row.Time, muted, TextStyles.Footnote);
        }

        drawList.AddLine(new Vector2(origin.X + padX, max.Y), new Vector2(max.X - padX, max.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ink, HairlineAlpha)), Metrics.Stroke.Hairline);

        if (!interactive)
        {
            return HistoryRowAction.None;
        }

        var removeMin = removeCenter - new Vector2(removeRadius, removeRadius);
        var removeMax = removeCenter + new Vector2(removeRadius, removeRadius);
        if (UiInteract.Click(removeMin, removeMax, overRemove))
        {
            return HistoryRowAction.Remove;
        }

        return !overRemove && UiInteract.Click(origin, max, hovered) ? HistoryRowAction.Recall : HistoryRowAction.None;
    }

    private void DrawHistoryEmpty(ImDrawListPtr drawList, Rect list, Vector4 ink, Vector4 muted, float scale)
    {
        var tileRadius = EmptyTileRadius * scale;
        var tileCenter = new Vector2(list.Center.X, list.Min.Y + list.Height * 0.36f);
        drawList.AddCircleFilled(tileCenter, tileRadius, ImGui.GetColorU32(Palette.WithAlpha(ink, EmptyTileAlpha)), 40);
        AppSkin.Icon(drawList, tileCenter, IconGlyph.Of(FontAwesomeIcon.History), muted, EmptyGlyphScale);
        var titleTop = tileCenter.Y + tileRadius + EmptyTitleGap * scale;
        var maxWidth = MathF.Min(list.Width - SheetPadX * 2f * scale, EmptyBodyMaxWidth * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.Calculator.HistoryEmptyTitle),
            TextStyles.Title3, ink, new Vector2(list.Center.X, titleTop), maxWidth);
        Typography.DrawWrappedCentered(drawList, Loc.T(L.Calculator.HistoryEmptyBody), TextStyles.Subheadline, muted,
            new Vector2(list.Center.X, titleBottom + EmptyBodyGap * scale), maxWidth);
    }

    private void AskClearHistory()
    {
        historySheet.Close();
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Calculator.ClearConfirm),
            ConfirmLabel = Loc.T(L.Calculator.ClearConfirmAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = engine.ClearHistory,
        });
    }
}
