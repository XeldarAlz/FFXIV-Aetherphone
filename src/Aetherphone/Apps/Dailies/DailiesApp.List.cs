using System.Runtime.InteropServices;
using Aetherphone.Apps.Timers;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Dailies;

internal sealed partial class DailiesApp
{
    private const float RowWashRadius = 12f;
    private const float EditButtonRadius = 14f;
    private const float EditHitRadius = 20f;
    private const float EditGlyph = 13f;
    private const float EditWashAlpha = 0.14f;
    private const float HiddenAlpha = 0.45f;
    private const float UnavailableAlpha = 0.6f;
    private const FontAwesomeIcon CustomIcon = FontAwesomeIcon.ListUl;

    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    private readonly Spring[] checkSprings = new Spring[DailyCatalog.Items.Length];
    private readonly Dictionary<string, Spring> customSprings = new(StringComparer.Ordinal);
    private readonly CachedText[] subtitleTexts = new CachedText[DailyCatalog.Items.Length];
    private readonly CachedText[] valueTexts = new CachedText[DailyCatalog.Items.Length];
    private bool checksPrimed;
    private bool composing;
    private bool composeFocus;
    private string composeText = string.Empty;

    private void PrimeChecks()
    {
        checksPrimed = false;
        customSprings.Clear();
    }

    private void StartCompose()
    {
        composing = true;
        composeFocus = true;
        composeText = string.Empty;
    }

    private void EndCompose()
    {
        composing = false;
        composeFocus = false;
        composeText = string.Empty;
    }

    private void DrawSections(DateTime utcNow, float width, float scale)
    {
        if (!checksPrimed)
        {
            checksPrimed = true;
            for (var index = 0; index < checkSprings.Length; index++)
            {
                checkSprings[index].SnapTo(tracker.State(index) == DailyRowState.Done ? 1f : 0f);
            }
        }

        var autoRows = CountCatalogRows(autoGroup: true);
        var manualRows = CountCatalogRows(autoGroup: false) + CountCustomRows() + (composing ? 1 : 0);
        if (autoRows == 0 && manualRows == 0)
        {
            DrawState(CustomIcon, Loc.T(L.Dailies.EmptyTitle), Loc.T(L.Dailies.EmptyBody), width, scale);
            return;
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        if (autoRows > 0)
        {
            DrawSectionHeader(Loc.T(L.Dailies.AutoSection), width, scale);
            DrawAutoCard(autoRows, width, delta, scale);
        }

        if (manualRows > 0)
        {
            DrawSectionHeader(Loc.T(L.Dailies.ManualSection), width, scale);
            DrawManualCard(manualRows, utcNow, width, delta, scale);
        }
    }

    private bool ShowsRow(int itemIndex) => editing || tracker.State(itemIndex) != DailyRowState.Hidden;

    private int CountCatalogRows(bool autoGroup)
    {
        var items = DailyCatalog.Items;
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (InGroup(items[index], autoGroup) && ShowsRow(index))
            {
                count++;
            }
        }

        return count;
    }

    private int CountCustomRows()
    {
        var tasks = tracker.CustomTasks;
        var count = 0;
        for (var index = 0; index < tasks.Count; index++)
        {
            if (tasks[index].Cadence == cadence)
            {
                count++;
            }
        }

        return count;
    }

    private bool InGroup(in DailyItem item, bool autoGroup) =>
        item.Cadence == cadence && (item.Tracking != DailyTracking.Manual) == autoGroup;

    private void DrawSectionHeader(string title, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = TimersArt.SectionHeaderHeight * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X, origin.Y + (height - titleHeight) * 0.5f),
            Typography.FitText(title, width, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        Advance(origin, width, height, 0f, scale);
    }

    private void DrawAutoCard(int rows, float width, float delta, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = rows * TimersArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("dailies.auto", new Rect(origin, max));
        if (!ImGui.IsRectVisible(origin, max))
        {
            Advance(origin, width, height, TimersArt.SectionGap, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        TimersArt.Card(drawList, ui, origin, max, scale);
        var items = DailyCatalog.Items;
        var rowIndex = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (!InGroup(items[index], true) || !ShowsRow(index))
            {
                continue;
            }

            DrawAutoRow(drawList, CardRow(origin, max, rowIndex, scale), index, delta, scale);
            rowIndex++;
        }

        Advance(origin, width, height, TimersArt.SectionGap, scale);
    }

    private void DrawManualCard(int rows, DateTime utcNow, float width, float delta, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = rows * TimersArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var visible = ImGui.IsRectVisible(origin, max);
        var drawList = ImGui.GetWindowDrawList();
        if (visible)
        {
            TimersArt.Card(drawList, ui, origin, max, scale);
        }

        var rowIndex = 0;
        var items = DailyCatalog.Items;
        for (var index = 0; index < items.Length; index++)
        {
            if (!InGroup(items[index], false) || !ShowsRow(index))
            {
                continue;
            }

            var row = CardRow(origin, max, rowIndex, scale, visible);
            ReportFirstManual(rowIndex, row, scale);
            if (visible)
            {
                DrawManualRow(drawList, row, index, utcNow, delta, scale);
            }

            rowIndex++;
        }

        var tasks = tracker.CustomTasks;
        for (var index = 0; index < tasks.Count; index++)
        {
            var task = tasks[index];
            if (task.Cadence != cadence)
            {
                continue;
            }

            var row = CardRow(origin, max, rowIndex, scale, visible);
            ReportFirstManual(rowIndex, row, scale);
            if (visible && DrawCustomRow(drawList, row, task, delta, scale))
            {
                break;
            }

            rowIndex++;
        }

        if (composing)
        {
            DrawComposeRow(drawList, CardRow(origin, max, rows - 1, scale, visible), scale);
        }

        Advance(origin, width, height, TimersArt.SectionGap, scale);
    }

    private static void ReportFirstManual(int rowIndex, Rect row, float scale)
    {
        if (rowIndex == 0)
        {
            UiAnchors.Report("dailies.manual", WashRect(row, scale));
        }
    }

    private Rect CardRow(Vector2 cardMin, Vector2 cardMax, int index, float scale, bool drawHairline = true)
    {
        var pad = Metrics.Space.Lg * scale;
        var top = cardMin.Y + index * TimersArt.RowHeight * scale;
        if (index > 0 && drawHairline)
        {
            TimersArt.Hairline(ImGui.GetWindowDrawList(), ui, cardMin.X + pad, cardMax.X, top);
        }

        return new Rect(new Vector2(cardMin.X + pad, top),
            new Vector2(cardMax.X - pad, top + TimersArt.RowHeight * scale));
    }

    private static Rect WashRect(Rect row, float scale) =>
        new(new Vector2(row.Min.X - Metrics.Space.Sm * scale, row.Min.Y + Metrics.Space.Xxs * scale),
            new Vector2(row.Max.X + Metrics.Space.Sm * scale, row.Max.Y - Metrics.Space.Xxs * scale));

    private void DrawAutoRow(ImDrawListPtr drawList, Rect row, int itemIndex, float delta, float scale)
    {
        var item = DailyCatalog.Items[itemIndex];
        var status = tracker.Status(itemIndex);
        var state = tracker.State(itemIndex);
        var fade = state switch
        {
            DailyRowState.Hidden => HiddenAlpha,
            DailyRowState.Unavailable => UnavailableAlpha,
            _ => 1f,
        };
        var fill = StepCheck(ref checkSprings[itemIndex], state == DailyRowState.Done, delta);
        var trailingLeft = row.Max.X;
        if (editing)
        {
            trailingLeft = DrawHideButton(drawList, row, item.Id, state == DailyRowState.Hidden, scale);
        }
        else if (state == DailyRowState.Done)
        {
            var center = new Vector2(row.Max.X - DailiesArt.CheckRadius * scale, row.Center.Y);
            DailiesArt.Check(drawList, center, DailiesArt.CheckRadius * scale, fill, item.Accent, ui.MutedInk, scale);
            trailingLeft = center.X - DailiesArt.CheckRadius * scale;
        }
        else if (state is DailyRowState.Open or DailyRowState.Info && DailyProgress.ShowsCount(item.Tracking))
        {
            var valueInk = state == DailyRowState.Info ? ui.BodyInk : ui.TitleInk;
            var valueWidth = TimersArt.Value(drawList, row.Max.X, row.Center.Y, ValueText(itemIndex, status, item),
                valueInk);
            trailingLeft = row.Max.X - valueWidth;
        }

        var dialCenter = new Vector2(TimersArt.DialCenterX(row.Min.X, scale), row.Center.Y);
        var tint = Palette.WithAlpha(item.Accent, fade);
        TimersArt.Dial(drawList, dialCenter, item.Icon, tint, DailyProgress.Fraction(status, item.Tracking),
            state != DailyRowState.Unavailable, scale);
        var textLeft = row.Min.X + TimersArt.DialSpan(scale);
        var textRight = trailingLeft < row.Max.X ? TimersArt.LabelRight(trailingLeft, scale) : row.Max.X;
        TimersArt.Labels(drawList, textLeft, textRight, row.Center.Y, Loc.T(item.Label),
            AutoSubtitle(state, status, item), Palette.WithAlpha(ui.TitleInk, fade),
            Palette.WithAlpha(ui.MutedInk, fade), scale);
    }

    private void DrawManualRow(ImDrawListPtr drawList, Rect row, int itemIndex, DateTime utcNow, float delta,
        float scale)
    {
        var item = DailyCatalog.Items[itemIndex];
        var state = tracker.State(itemIndex);
        var subtitle = ManualSubtitle(itemIndex, item, utcNow);
        if (editing)
        {
            var hidden = state == DailyRowState.Hidden;
            var trailingLeft = DrawHideButton(drawList, row, item.Id, hidden, scale);
            var fade = hidden ? HiddenAlpha : 1f;
            DrawPlainRow(drawList, row, item.Icon, Palette.WithAlpha(item.Accent, fade), Loc.T(item.Label), subtitle,
                trailingLeft, fade, scale);
            return;
        }

        var isDone = state == DailyRowState.Done;
        if (CheckRow(drawList, row, ImGui.GetID($"dailies.check.{item.Id}"), ref checkSprings[itemIndex], isDone,
                item.Icon, item.Accent, Loc.T(item.Label), subtitle, delta, scale))
        {
            Toggle(item.Id, item.Cadence, isDone);
        }
    }

    private bool DrawCustomRow(ImDrawListPtr drawList, Rect row, DailyCustomTask task, float delta, float scale)
    {
        if (editing)
        {
            var center = new Vector2(row.Max.X - EditButtonRadius * scale, row.Center.Y);
            var deleted = DailiesArt.RoundButton(drawList, ImGui.GetID($"dailies.delete.{task.Id}"), center,
                EditButtonRadius * scale, EditHitRadius * scale, FontAwesomeIcon.Minus, TileInk, ui.Theme.Danger,
                1f, EditGlyph * scale, Loc.T(L.Dailies.Delete));
            DrawPlainRow(drawList, row, CustomIcon, ui.Accent, task.Title, string.Empty,
                center.X - EditButtonRadius * scale, 1f, scale);
            if (!deleted)
            {
                return false;
            }

            customSprings.Remove(task.Id);
            tracker.RemoveTask(task.Id);
            UiFeedback.Play(UiSound.ToggleOff);
            return true;
        }

        var isDone = tracker.IsCustomDone(task);
        ref var spring = ref CollectionsMarshal.GetValueRefOrAddDefault(customSprings, task.Id, out var exists);
        if (!exists)
        {
            spring.SnapTo(isDone ? 1f : 0f);
        }

        if (!CheckRow(drawList, row, ImGui.GetID($"dailies.check.{task.Id}"), ref spring, isDone, CustomIcon,
                ui.Accent, task.Title, string.Empty, delta, scale))
        {
            return false;
        }

        Toggle(task.Id, task.Cadence, isDone);
        return false;
    }

    private void Toggle(string itemId, DailyCadence itemCadence, bool wasDone)
    {
        tracker.SetChecked(itemId, itemCadence, !wasDone);
        if (wasDone)
        {
            UiFeedback.Play(UiSound.ToggleOff);
            return;
        }

        UiFeedback.Play(tracker.Remaining(itemCadence) == 0 ? UiSound.Success : UiSound.ToggleOn);
    }

    private bool CheckRow(ImDrawListPtr drawList, Rect row, uint id, ref Spring spring, bool isDone,
        FontAwesomeIcon icon, Vector4 tint, string title, string subtitle, float delta, float scale)
    {
        var wash = WashRect(row, scale);
        var hovered = UiInteract.Hover(wash.Min, wash.Max);
        if (hovered)
        {
            drawList.AddRectFilled(wash.Min, wash.Max, ImGui.GetColorU32(ui.HoverWash), RowWashRadius * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, pressed, PressFx.IconPressedScale);
        var fill = StepCheck(ref spring, isDone, delta);
        var radius = DailiesArt.CheckRadius * scale;
        var center = new Vector2(row.Max.X - radius, row.Center.Y);
        DailiesArt.Check(drawList, center, radius * grow, fill, tint, ui.MutedInk, scale);
        var dialCenter = new Vector2(TimersArt.DialCenterX(row.Min.X, scale), row.Center.Y);
        TimersArt.Dial(drawList, dialCenter, icon, tint, 0f, false, scale);
        var textLeft = row.Min.X + TimersArt.DialSpan(scale);
        var titleInk = Vector4.Lerp(ui.TitleInk, ui.MutedInk, fill);
        TimersArt.Labels(drawList, textLeft, TimersArt.LabelRight(center.X - radius, scale), row.Center.Y, title,
            subtitle, titleInk, ui.MutedInk, scale);
        return UiInteract.Click(wash.Min, wash.Max, hovered, false);
    }

    private void DrawPlainRow(ImDrawListPtr drawList, Rect row, FontAwesomeIcon icon, Vector4 tint, string title,
        string subtitle, float trailingLeft, float fade, float scale)
    {
        var dialCenter = new Vector2(TimersArt.DialCenterX(row.Min.X, scale), row.Center.Y);
        TimersArt.Dial(drawList, dialCenter, icon, tint, 0f, false, scale);
        var textLeft = row.Min.X + TimersArt.DialSpan(scale);
        TimersArt.Labels(drawList, textLeft, TimersArt.LabelRight(trailingLeft, scale), row.Center.Y, title,
            subtitle, Palette.WithAlpha(ui.TitleInk, fade), Palette.WithAlpha(ui.MutedInk, fade), scale);
    }

    private float DrawHideButton(ImDrawListPtr drawList, Rect row, string itemId, bool hidden, float scale)
    {
        var center = new Vector2(row.Max.X - EditButtonRadius * scale, row.Center.Y);
        var ink = hidden ? ui.MutedInk : ui.Accent;
        var clicked = DailiesArt.RoundButton(drawList, ImGui.GetID($"dailies.hide.{itemId}"), center,
            EditButtonRadius * scale, EditHitRadius * scale, hidden ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye,
            ink, ink, EditWashAlpha, EditGlyph * scale, Loc.T(hidden ? L.Dailies.Show : L.Dailies.Hide));
        if (clicked)
        {
            tracker.SetHidden(itemId, !hidden);
            UiFeedback.Play(hidden ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        return center.X - EditButtonRadius * scale;
    }

    private void DrawComposeRow(ImDrawListPtr drawList, Rect row, float scale)
    {
        var dialCenter = new Vector2(TimersArt.DialCenterX(row.Min.X, scale), row.Center.Y);
        TimersArt.Dial(drawList, dialCenter, FontAwesomeIcon.Plus, ui.Accent, 0f, false, scale);
        var left = row.Min.X + TimersArt.DialSpan(scale);
        var hint = Loc.T(L.Dailies.NewTask);
        ImGui.SetCursorScreenPos(new Vector2(left, row.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, row.Max.X - left));
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(composeText);
        if (composeFocus)
        {
            composeFocus = false;
            ImGui.SetKeyboardFocusHere();
        }

        bool entered;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (ImRaii.PushColor(ImGuiCol.TextDisabled, ui.MutedInk))
        {
            entered = ImGui.InputTextWithHint("##dailies.compose", hint, ref composeText,
                DailyLedger.MaxTitleLength * 4, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var deactivated = ImGui.IsItemDeactivated();
        if (entered)
        {
            if (tracker.AddTask(composeText, cadence))
            {
                UiFeedback.Play(UiSound.ToggleOn);
                StartCompose();
                return;
            }

            EndCompose();
            return;
        }

        if (!deactivated)
        {
            return;
        }

        if (tracker.AddTask(composeText, cadence))
        {
            UiFeedback.Play(UiSound.ToggleOn);
        }

        EndCompose();
    }

    private static float StepCheck(ref Spring spring, bool isDone, float delta) =>
        Math.Clamp(spring.Step(isDone ? 1f : 0f, Motion.Release, delta), 0f, 1f);

    private string ValueText(int itemIndex, in DailyAutoStatus status, in DailyItem item)
    {
        var doneCount = DailyProgress.DoneCount(status, item.Tracking);
        var key = doneCount * 100000L + status.Goal;
        ref var cache = ref valueTexts[itemIndex];
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        return cache.Store(key, string.Concat(doneCount.ToString(Loc.Culture), " / ",
            status.Goal.ToString(Loc.Culture)));
    }

    private string AutoSubtitle(DailyRowState state, in DailyAutoStatus status, in DailyItem item)
    {
        if (state == DailyRowState.Unavailable)
        {
            return tracker.HasData ? Loc.T(L.Dailies.NotUnlocked) : string.Empty;
        }

        if (item.Tracking == DailyTracking.Levequests && status.Available && status.Remaining >= status.Goal)
        {
            return Loc.T(L.Dailies.LevequestsCapped);
        }

        return string.Empty;
    }

    private string ManualSubtitle(int itemIndex, in DailyItem item, DateTime utcNow)
    {
        ref var cache = ref subtitleTexts[itemIndex];
        switch (item.Id)
        {
            case "weekly.fashionReport":
            {
                var window = GameSchedule.FashionReport(utcNow);
                var remaining = window.NextChangeUtc - utcNow;
                var key = ((long)remaining.TotalMinutes << 1) | (window.Active ? 1L : 0L);
                if (cache.IsCurrent(key))
                {
                    return cache.Value;
                }

                return cache.Store(key, window.Active
                    ? Loc.T(L.Dailies.VotingOpenCloses, TimeText.Until(remaining))
                    : Loc.T(L.Dailies.VotingOpensIn, TimeText.Until(remaining)));
            }
            case "weekly.jumboCactpot":
            {
                var remaining = GameSchedule.NextJumboCactpot(utcNow, tracker.RegionCode) - utcNow;
                var key = (long)remaining.TotalMinutes;
                if (cache.IsCurrent(key))
                {
                    return cache.Value;
                }

                return cache.Store(key, Loc.T(L.Dailies.NextDrawing, TimeText.Until(remaining)));
            }
            default:
            {
                return string.Empty;
            }
        }
    }
}
