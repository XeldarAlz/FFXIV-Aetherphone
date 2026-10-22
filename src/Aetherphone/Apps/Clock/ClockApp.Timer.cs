using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const float TimerWheelWidth = 50f;
    private const float TimerUnitGap = 6f;
    private const float TimerRingMax = 270f;
    private const float TimerRingFraction = 0.78f;
    private const float TimerRingThickness = 8f;
    private const float TimerRingTextFraction = 1.45f;
    private const float TimerEndGlyph = 14f;
    private const float TimerEndGap = 5f;
    private const float TimerLineGap = 4f;
    private const float RecentRowHeight = 72f;
    private const float RecentPlayRadius = 18f;
    private const float RecentPlayGlyph = 0.42f;
    private const float RecentFillAlpha = 0.24f;
    private const float RecentHoverAlpha = 0.36f;
    private const int DefaultTimerMinutes = 5;

    private static readonly TextStyle[] TimerLadder =
    {
        TextStyles.WidgetDisplay, TextStyles.WidgetDisplayCompact, TextStyles.Title1, TextStyles.Title2,
    };

    private static readonly string[] Hours24Timer = BuildPairs(24);

    private readonly ClockWheel timerHourWheel = new("timer.hour");
    private readonly ClockWheel timerMinuteWheel = new("timer.minute");
    private readonly ClockWheel timerSecondWheel = new("timer.second");
    private readonly CachedText[] recentWords = new CachedText[CountdownTimer.RecentCapacity];
    private int pickerHours;
    private int pickerMinutes = DefaultTimerMinutes;
    private int pickerSeconds;
    private bool primeTimerPicker = true;
    private bool timerWheelHovered;
    private string timerLabelDraft = string.Empty;
    private bool timerLabelPrimed;

    private void DrawTimer(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var utcNow = DateTime.UtcNow;
        var phase = CountdownTimer.Phase(configuration, utcNow, out var remaining);
        var active = phase is CountdownPhase.Running or CountdownPhase.Paused;
        if (!timerLabelPrimed)
        {
            timerLabelPrimed = true;
            timerLabelDraft = configuration.TimerLabel;
        }

        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("clock.timer"))
        using (var surface = AppSurface.Begin(navBar.Body, !active && timerWheelHovered))
        {
            var width = ImGui.GetContentRegionAvail().X;
            if (active)
            {
                timerWheelHovered = false;
                DrawTimerRing(phase, remaining, utcNow, width, scale);
                DrawTimerActiveControls(phase, width, scale);
            }
            else
            {
                var wheelsActive = DrawTimerPicker(width, scale);
                DrawTimerIdleControls(utcNow, width, scale);
                DrawTimerLabel(width, scale);
                DrawRecents(utcNow, width, scale);
                if (wheelsActive)
                {
                    surface.CancelDrag();
                }
            }

            ImGui.Dummy(new Vector2(0f, ClockArt.BottomPad * scale));
        }

        AppHeader.EndLargeTitle(in navBar, context, "clock.timer.nav", Loc.T(L.Clock.TabTimer), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private bool DrawTimerPicker(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = WheelCardPad * scale;
        var height = ClockWheel.HeightUnits * scale + pad * 2f;
        var card = new Rect(origin, origin + new Vector2(width, height));
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        DrawWheelBand(drawList, card, scale);
        if (primeTimerPicker)
        {
            primeTimerPicker = false;
            timerHourWheel.Snap(pickerHours);
            timerMinuteWheel.Snap(pickerMinutes);
            timerSecondWheel.Snap(pickerSeconds);
        }

        var columnWidth = (width - pad * 2f) / 3f;
        var style = new WheelStyle(ui.TitleInk, ui.MutedInk, TextStyles.Title2);
        pickerHours = DrawTimerColumn(drawList, timerHourWheel, card, 0, columnWidth, pickerHours, Hours24Timer,
            Loc.T(L.Clock.Hours), style, scale);
        pickerMinutes = DrawTimerColumn(drawList, timerMinuteWheel, card, 1, columnWidth, pickerMinutes, Minutes60,
            Loc.T(L.Clock.Minutes), style, scale);
        pickerSeconds = DrawTimerColumn(drawList, timerSecondWheel, card, 2, columnWidth, pickerSeconds, Minutes60,
            Loc.T(L.Clock.Seconds), style, scale);
        timerWheelHovered = timerHourWheel.Hovered || timerMinuteWheel.Hovered || timerSecondWheel.Hovered;
        ClockArt.Advance(origin, width, height, EditorGap, scale);
        return timerHourWheel.Active || timerMinuteWheel.Active || timerSecondWheel.Active;
    }

    private static int DrawTimerColumn(ImDrawListPtr drawList, ClockWheel wheel, Rect card, int column,
        float columnWidth, int value, string[] labels, string unit, in WheelStyle style, float scale)
    {
        var pad = WheelCardPad * scale;
        var columnLeft = card.Min.X + pad + column * columnWidth;
        var wheelWidth = TimerWheelWidth * scale;
        var unitWidth = MathF.Max(1f, columnWidth - wheelWidth - TimerUnitGap * scale);
        var fittedUnit = Typography.FitText(unit, unitWidth, TextStyles.SubheadlineEmphasized);
        var unitTextWidth = Typography.Measure(fittedUnit, TextStyles.SubheadlineEmphasized).X;
        var blockWidth = wheelWidth + TimerUnitGap * scale + unitTextWidth;
        var wheelLeft = columnLeft + MathF.Max(0f, (columnWidth - blockWidth) * 0.5f);
        var wheelRect = new Rect(new Vector2(wheelLeft, card.Min.Y + pad),
            new Vector2(wheelLeft + wheelWidth, card.Max.Y - pad));
        var result = wheel.Draw(drawList, wheelRect, value, labels, true, style);
        var unitHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var unitLeft = wheelRect.Max.X + TimerUnitGap * scale;
        Typography.Draw(drawList, new Vector2(unitLeft, card.Center.Y - unitHeight * 0.5f), fittedUnit, style.Ink,
            TextStyles.SubheadlineEmphasized);
        return result;
    }

    private void DrawTimerIdleControls(DateTime utcNow, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var radius = ClockArt.ControlRadius * scale;
        var inset = ControlsSideInset * scale;
        var drawList = ImGui.GetWindowDrawList();
        var gapInk = ui.Palette.BackdropBottom;
        var cancelCenter = new Vector2(origin.X + inset + radius, origin.Y + radius);
        ClockArt.ControlButton(drawList, "clock.timer.cancel", cancelCenter, radius, Loc.T(L.Clock.Cancel),
            ClockArt.NeutralGray, gapInk, false);
        var total = pickerHours * 3600 + pickerMinutes * 60 + pickerSeconds;
        if (ClockArt.ControlButton(drawList, "clock.timer.start",
                new Vector2(origin.X + width - inset - radius, origin.Y + radius), radius, Loc.T(L.Clock.Start),
                ClockArt.StartGreen, gapInk, total > 0))
        {
            StartTimer(total, utcNow);
        }

        ClockArt.Advance(origin, width, radius * 2f, ControlsGap, scale);
    }

    private void DrawTimerLabel(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = OptionRowHeight * scale;
        var row = new Rect(origin, origin + new Vector2(width, rowHeight));
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);
        var inset = Metrics.Space.Lg * scale;
        DrawOptionTitle(drawList, row.Min.X + inset, row, Loc.T(L.Clock.Label));
        DrawLabelInput(row, row.Max.X - inset, width, "##clockTimerLabel", Loc.T(L.Clock.TimerTitle),
            ref timerLabelDraft);
        ClockArt.Advance(origin, width, rowHeight, ClockArt.SectionGap, scale);
    }

    private void DrawRecents(DateTime utcNow, float width, float scale)
    {
        var recents = configuration.ClockRecentTimers;
        var count = Math.Min(recents.Count, CountdownTimer.RecentCapacity);
        if (count == 0)
        {
            return;
        }

        CardSectionHeader.Flow(Loc.T(L.Clock.Recents), ui.TitleInk);
        var rowHeight = RecentRowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var max = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var started = -1;
        var loaded = -1;
        for (var index = 0; index < count; index++)
        {
            var row = RowRect(origin, width, index * rowHeight, rowHeight);
            if (index > 0)
            {
                FeedCell.Hairline(drawList, origin.X + Metrics.Space.Lg * scale, max.X, row.Min.Y, ui.Hairline);
            }

            var action = DrawRecentRow(drawList, row, index, recents[index], scale);
            if (action == 2)
            {
                started = index;
            }
            else if (action == 1)
            {
                loaded = index;
            }
        }

        ClockArt.Advance(origin, width, max.Y - origin.Y, 0f, scale);
        if (started >= 0)
        {
            StartTimer(recents[started], utcNow);
        }
        else if (loaded >= 0)
        {
            LoadPicker(recents[loaded]);
        }
    }

    private int DrawRecentRow(ImDrawListPtr drawList, Rect row, int index, int seconds, float scale)
    {
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return 0;
        }

        var inset = Metrics.Space.Lg * scale;
        var left = row.Min.X + inset;
        var right = row.Max.X - inset;
        var playRadius = RecentPlayRadius * scale;
        var playCenter = new Vector2(right - playRadius, row.Center.Y);
        var playCorner = new Vector2(MathF.Max(playRadius, Metrics.Size.TapTarget * 0.5f * scale));
        var overPlay = UiInteract.Hover(playCenter - playCorner, playCenter + playCorner);
        var grow = PressFx.Scale(ImGui.GetID($"clock.recent.play.{index}"),
            overPlay && ImGui.IsMouseDown(ImGuiMouseButton.Left), Motion.PressScaleControl);
        drawList.AddCircleFilled(playCenter, playRadius * grow,
            ImGui.GetColorU32(ClockArt.StartGreen with { W = overPlay ? RecentHoverAlpha : RecentFillAlpha }),
            32);
        DrawPlayGlyph(drawList, playCenter, playRadius * RecentPlayGlyph * grow, ClockArt.StartGreen);

        var bigHeight = Typography.LineHeight(TextStyles.WidgetDisplayCompact);
        var wordsHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (bigHeight + wordsHeight) * 0.5f;
        ClockArt.DrawDuration(drawList, new Vector2(left, top), seconds / 3600, seconds / 60 % 60, seconds % 60, -1,
            false, TextStyles.WidgetDisplayCompact, ui.TitleInk);
        Typography.Draw(drawList, new Vector2(left, top + bigHeight), RecentWords(index, seconds), ui.MutedInk,
            TextStyles.Footnote);

        if (overPlay)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(playCenter - playCorner, playCenter + playCorner, overPlay))
        {
            return 2;
        }

        var hovered = !overPlay && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return !overPlay && UiInteract.Click(row.Min, row.Max, hovered) ? 1 : 0;
    }

    private static void DrawPlayGlyph(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color)
    {
        var offset = size * 0.18f;
        drawList.AddTriangleFilled(new Vector2(center.X - size * 0.8f + offset, center.Y - size),
            new Vector2(center.X - size * 0.8f + offset, center.Y + size),
            new Vector2(center.X + size + offset, center.Y), ImGui.GetColorU32(color));
    }

    private string RecentWords(int index, int seconds)
    {
        ref var cache = ref recentWords[index];
        if (cache.IsCurrent(seconds))
        {
            return cache.Value;
        }

        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var rest = seconds % 60;
        var text = string.Empty;
        if (hours > 0)
        {
            text = Loc.T(L.Clock.DurationHours, hours);
        }

        if (minutes > 0)
        {
            text = Join(text, Loc.T(L.Clock.DurationMinutes, minutes));
        }

        if (rest > 0)
        {
            text = Join(text, Loc.T(L.Clock.DurationSeconds, rest));
        }

        return cache.Store(seconds, text);
    }

    private static string Join(string head, string tail) => head.Length == 0 ? tail : string.Concat(head, " ", tail);

    private void DrawTimerRing(CountdownPhase phase, double remaining, DateTime utcNow, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var diameter = MathF.Min(TimerRingMax * scale, width * TimerRingFraction);
        var radius = diameter * 0.5f;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + radius);
        var drawList = ImGui.GetWindowDrawList();
        var thickness = TimerRingThickness * scale;
        var ringRadius = radius - thickness * 0.5f;
        ProgressRing.Track(drawList, center, ringRadius, thickness, Palette.WithAlpha(ui.TitleInk, 0.12f));
        ProgressRing.Fill(drawList, center, ringRadius, thickness, CountdownTimer.Fraction(configuration, remaining),
            phase == CountdownPhase.Paused ? Palette.WithAlpha(ui.Accent, 0.55f) : ui.Accent);

        var whole = Math.Max(0, (int)Math.Ceiling(remaining));
        var hours = whole / 3600;
        var minutes = whole / 60 % 60;
        var seconds = whole % 60;
        var maxWidth = ringRadius * TimerRingTextFraction;
        var style = TimerLadder[^1];
        for (var index = 0; index < TimerLadder.Length; index++)
        {
            if (ClockArt.DurationWidth(hours, minutes, seconds, -1, false, TimerLadder[index]) <= maxWidth)
            {
                style = TimerLadder[index];
                break;
            }
        }

        var digitsWidth = ClockArt.DurationWidth(hours, minutes, seconds, -1, false, style);
        var digitsHeight = Typography.LineHeight(style);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var label = configuration.TimerLabel;
        var digitsTop = center.Y - digitsHeight * 0.5f;
        if (label.Length > 0)
        {
            var fitted = Typography.FitText(label, maxWidth, TextStyles.Subheadline);
            var labelWidth = Typography.Measure(fitted, TextStyles.Subheadline).X;
            Typography.Draw(drawList,
                new Vector2(center.X - labelWidth * 0.5f, digitsTop - lineHeight - TimerLineGap * scale), fitted,
                ui.MutedInk, TextStyles.Subheadline);
        }

        ClockArt.DrawDuration(drawList, new Vector2(center.X - digitsWidth * 0.5f, digitsTop), hours, minutes,
            seconds, -1, false, style, phase == CountdownPhase.Paused ? ui.MutedInk : ui.TitleInk);
        DrawTimerEndLine(drawList, phase, utcNow, remaining, center.X, digitsTop + digitsHeight + TimerLineGap * scale,
            maxWidth, scale);
        ClockArt.Advance(origin, width, diameter, ControlsGap, scale);
    }

    private void DrawTimerEndLine(ImDrawListPtr drawList, CountdownPhase phase, DateTime utcNow, double remaining,
        float centerX, float top, float maxWidth, float scale)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        if (phase == CountdownPhase.Paused)
        {
            var paused = Typography.FitText(Loc.T(L.Clock.Paused), maxWidth, TextStyles.Subheadline);
            var pausedWidth = Typography.Measure(paused, TextStyles.Subheadline).X;
            Typography.Draw(drawList, new Vector2(centerX - pausedWidth * 0.5f, top), paused, ui.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        var end = TimeText.Clock(utcNow.AddSeconds(remaining).ToLocalTime());
        var glyph = TimerEndGlyph * scale;
        var gap = TimerEndGap * scale;
        var textWidth = Typography.Measure(end, TextStyles.Subheadline).X;
        var left = centerX - (glyph + gap + textWidth) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(left + glyph * 0.5f, top + lineHeight * 0.5f), PhoneIcons.BellFilled,
            ui.MutedInk, glyph);
        Typography.Draw(drawList, new Vector2(left + glyph + gap, top), end, ui.MutedInk, TextStyles.Subheadline);
    }

    private void DrawTimerActiveControls(CountdownPhase phase, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var radius = ClockArt.ControlRadius * scale;
        var inset = ControlsSideInset * scale;
        var drawList = ImGui.GetWindowDrawList();
        var gapInk = ui.Palette.BackdropBottom;
        if (ClockArt.ControlButton(drawList, "clock.timer.cancel",
                new Vector2(origin.X + inset + radius, origin.Y + radius), radius, Loc.T(L.Clock.Cancel),
                ClockArt.NeutralGray, gapInk, true))
        {
            CountdownTimer.Cancel(configuration);
            primeTimerPicker = true;
            configuration.Save();
        }

        var paused = phase == CountdownPhase.Paused;
        var rightCenter = new Vector2(origin.X + width - inset - radius, origin.Y + radius);
        if (ClockArt.ControlButton(drawList, "clock.timer.toggle", rightCenter, radius,
                Loc.T(paused ? L.Clock.Resume : L.Clock.Pause), paused ? ClockArt.StartGreen : ClockArt.PauseOrange,
                gapInk, true))
        {
            if (paused)
            {
                CountdownTimer.Resume(configuration, DateTime.UtcNow);
            }
            else
            {
                CountdownTimer.Pause(configuration, DateTime.UtcNow);
            }

            configuration.Save();
        }

        ClockArt.Advance(origin, width, radius * 2f, 0f, scale);
    }

    private void StartTimer(int seconds, DateTime utcNow)
    {
        CountdownTimer.Start(configuration, seconds, timerLabelDraft.Trim(), utcNow);
        LoadPicker(seconds);
        configuration.Save();
        UiFeedback.Play(UiSound.Success);
    }

    private void LoadPicker(int seconds)
    {
        pickerHours = Math.Min(23, seconds / 3600);
        pickerMinutes = seconds / 60 % 60;
        pickerSeconds = seconds % 60;
    }
}
