using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private const float WaterHeroHeight = 176f;
    private const float WaterVesselWidth = 78f;
    private const float WaterVesselHeight = 124f;
    private const float KindTileHeight = 64f;
    private const float KindGlyph = 30f;
    private const float KindSelectedAlpha = 0.20f;
    private const float KindHoverAlpha = 0.08f;
    private const float ServingRowHeight = 56f;
    private const float AddPillHeight = Button.LargeHeight;
    private const float LogRowHeight = 48f;
    private const float LogGlyph = 26f;
    private const float LogTimeWidth = 72f;
    private const float RemoveRadius = 13f;
    private const int MaxHydrationGoal = 30;
    private const int ReminderStepMinutes = 15;
    private const int MinReminderMinutes = 15;
    private const int MaxReminderMinutes = 360;
    private const int MinutesPerHour = 60;

    private static readonly StepperIds HydrationGoalIds = StepperIds.For("health.water.goal");
    private static readonly StepperIds ServingIds = StepperIds.For("health.water.serving");
    private static readonly StepperIds ReminderIds = StepperIds.For("health.water.every");

    private static readonly FontAwesomeIcon[] KindIcons =
    {
        FontAwesomeIcon.Tint,
        FontAwesomeIcon.Leaf,
        FontAwesomeIcon.MugHot,
        FontAwesomeIcon.Lemon,
    };

    private static readonly string[] KindTileIds =
    {
        "health.kind.water",
        "health.kind.tea",
        "health.kind.coffee",
        "health.kind.juice",
    };

    private readonly List<string> logTimes = new();
    private readonly List<string> logNames = new();
    private readonly List<string> logVolumes = new();
    private readonly List<string> logRemoveIds = new();
    private long logKey = long.MinValue;
    private Spring waterFill;
    private CachedText goalText;
    private CachedText servingText;
    private CachedText reminderText;
    private CachedText addPillText;

    private void DrawWater(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("health.water"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawWaterOverview(drawList, origin, width, scale);
            cursorY = DrawLogDrink(drawList, origin.X, cursorY, width, scale);
            cursorY = SectionTop(drawList, origin.X, cursorY, width, Loc.T(L.Health.History), scale);
            cursorY = DrawDayChart(drawList, new Vector2(origin.X, cursorY), width, HealthMetric.Water,
                Profile.DailyHydrationGoal, scale);
            cursorY = DrawTodayLog(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawReminders(drawList, origin.X, cursorY, width, scale);
            ReserveTo(origin, width, cursorY + HealthArt.BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "health.nav.water", Loc.T(L.Health.Water), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawWaterOverview(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + WaterHeroHeight * scale);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var tint = HealthArt.Tint(HealthMetric.Water);
        var pad = HealthArt.CardPad * scale;
        var count = (int)digest.Live(HealthMetric.Water);
        var goal = Math.Max(1, Profile.DailyHydrationGoal);
        var fraction = waterFill.Step(count / (float)goal, Motion.Sheet, HealthArt.FrameDelta());
        var vesselSize = new Vector2(WaterVesselWidth, WaterVesselHeight) * scale;
        var vesselMin = new Vector2(origin.X + pad + Metrics.Space.Sm * scale,
            origin.Y + (max.Y - origin.Y - vesselSize.Y) * 0.5f);
        HealthArt.Vessel(drawList, new Rect(vesselMin, vesselMin + vesselSize), fraction, tint, ui.TitleInk, scale);
        var textLeft = vesselMin.X + vesselSize.X + HeroTextGap * scale + Metrics.Space.Sm * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var top = origin.Y + pad;
        var millilitres = digest.Today?.DrinkMillilitres ?? 0d;
        var volumeKey = (long)Math.Round(millilitres) * 4L + (long)Units;
        if (!heroVolume.IsCurrent(volumeKey))
        {
            var (value, unit) = HealthFormat.VolumeParts(millilitres, Units);
            heroVolume.Store(volumeKey, value);
            heroUnit.Store(volumeKey, unit);
        }

        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.Upper(Loc.T(L.Time.Today)), textWidth, TextStyles.FootnoteEmphasized), tint,
            TextStyles.FootnoteEmphasized);
        top += Typography.LineHeight(TextStyles.FootnoteEmphasized);
        HealthArt.ValueWithUnit(drawList, new Vector2(textLeft, top), textWidth, heroVolume.Value, heroUnit.Value,
            ui.TitleInk, tint, TextStyles.WidgetDisplayCompact);
        top += Typography.LineHeight(TextStyles.WidgetDisplayCompact);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(DrinksLine(count, goal), textWidth, TextStyles.Subheadline),
            count >= goal ? tint : ui.MutedInk, TextStyles.Subheadline);
        var stepperHeight = Metrics.Size.TapTarget * scale;
        var stepper = new Rect(new Vector2(textLeft, max.Y - pad - stepperHeight), new Vector2(max.X - pad, max.Y - pad));
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, stepper.Min.Y - labelHeight),
            Typography.FitText(Loc.T(L.Health.DailyGoal), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        var delta = Stepper(drawList, stepper, HydrationGoalIds, GoalText(goal), goal > 1, goal < MaxHydrationGoal, tint,
            ui.TitleInk, TextStyles.Headline, scale);
        if (delta != 0)
        {
            Profile.DailyHydrationGoal = Math.Clamp(goal + delta, 1, MaxHydrationGoal);
            tracker.SaveNow();
        }

        return max.Y;
    }

    private string GoalText(int goal)
    {
        return goalText.IsCurrent(goal)
            ? goalText.Value
            : goalText.Store(goal, string.Concat(HealthFormat.Number(goal), " ", Loc.Plural(L.Health.UnitDrinks, goal)));
    }

    private float DrawLogDrink(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.LogDrink), scale);
        var pad = HealthArt.CardPad * scale;
        var gap = Metrics.Space.Sm * scale;
        var height = pad * 2f + (KindTileHeight + ServingRowHeight + AddPillHeight) * scale + gap * 2f;
        var origin = new Vector2(left, cursorY);
        var max = new Vector2(left + width, cursorY + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var tint = HealthArt.Tint(HealthMetric.Water);
        var inner = width - pad * 2f;
        var tileWidth = (inner - gap * (DrinkKeys.All.Length - 1)) / DrinkKeys.All.Length;
        var rowTop = cursorY + pad;
        for (var kind = 0; kind < DrinkKeys.All.Length; kind++)
        {
            var tileMin = new Vector2(left + pad + (tileWidth + gap) * kind, rowTop);
            var tile = new Rect(tileMin, tileMin + new Vector2(tileWidth, KindTileHeight * scale));
            if (DrawKindTile(drawList, tile, kind, tint, scale))
            {
                Profile.ServingKind = DrinkKeys.All[kind];
                tracker.MarkDirty();
                UiFeedback.Play(UiSound.Tap);
            }
        }

        rowTop += KindTileHeight * scale + gap;
        var serving = new Rect(new Vector2(left + pad, rowTop),
            new Vector2(max.X - pad, rowTop + ServingRowHeight * scale));
        var step = HealthFormat.ServingStep(Units);
        var amount = Profile.ServingMillilitres;
        var delta = Stepper(drawList, serving, ServingIds, ServingText(amount),
            amount - step >= HealthFormat.MinServingMillilitres - 0.5d,
            amount + step <= HealthFormat.MaxServingMillilitres + 0.5d, tint, ui.TitleInk, TextStyles.Title2, scale);
        if (delta != 0)
        {
            Profile.ServingMillilitres = HealthFormat.SnapServing(amount + delta * step, Units);
            tracker.MarkDirty();
        }

        rowTop += ServingRowHeight * scale + gap;
        var pill = new Rect(new Vector2(left + pad, rowTop), new Vector2(max.X - pad, rowTop + AddPillHeight * scale));
        if (Button.Draw(pill, AddPillLabel(), ui.Ink))
        {
            LogServing();
        }

        return max.Y;
    }

    private bool DrawKindTile(ImDrawListPtr drawList, Rect tile, int kind, Vector4 tint, float scale)
    {
        var selected = string.Equals(Profile.ServingKind, DrinkKeys.All[kind], StringComparison.Ordinal);
        var hovered = UiInteract.Hover(tile.Min, tile.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(KindTileIds[kind], pressed, PressFx.ControlPressedScale);
        var half = tile.Size * 0.5f * press;
        var min = tile.Center - half;
        var max = tile.Center + half;
        var radius = Metrics.Radius.Md * scale;
        if (selected || hovered)
        {
            Squircle.Fill(drawList, min, max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(selected ? tint : ui.TitleInk,
                    selected ? KindSelectedAlpha : KindHoverAlpha)));
        }

        if (selected)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(tint), Metrics.Stroke.Thin * scale);
        }

        var glyph = KindGlyph * scale * press;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var glyphCenter = new Vector2(tile.Center.X, tile.Center.Y - labelHeight * 0.5f);
        ProgressRing.CenterIcon(drawList, glyphCenter, KindIcons[kind], selected ? tint : ui.MutedInk, glyph * 0.6f);
        var label = Typography.FitText(DrinkName(DrinkKeys.All[kind]), tile.Width - Metrics.Space.Xs * scale,
            TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(tile.Center.X - labelSize.X * 0.5f, glyphCenter.Y + glyph * 0.4f), label,
            selected ? ui.TitleInk : ui.MutedInk, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return !selected && UiInteract.Click(tile.Min, tile.Max, hovered);
    }

    private string ServingText(double millilitres)
    {
        var key = (long)Math.Round(millilitres * 10d) * 4L + (long)Units;
        return servingText.IsCurrent(key)
            ? servingText.Value
            : servingText.Store(key, HealthFormat.Volume(millilitres, Units));
    }

    private string AddPillLabel()
    {
        var key = Array.IndexOf(DrinkKeys.All, Profile.ServingKind);
        return addPillText.IsCurrent(key)
            ? addPillText.Value
            : addPillText.Store(key, Loc.T(L.Health.AddDrink, DrinkName(Profile.ServingKind)));
    }

    private float DrawTodayLog(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Time.Today), scale);
        var day = digest.Today;
        if (day is null || day.Drinks.Count == 0)
        {
            return cursorY + HealthArt.State(drawList, ui, new Vector2(left, cursorY), width, FontAwesomeIcon.Tint,
                HealthArt.Tint(HealthMetric.Water), Loc.T(L.Health.NoDrinksTitle), Loc.T(L.Health.NoDrinksBody),
                scale);
        }

        SyncLog(day);
        var count = day.Drinks.Count;
        var rowHeight = LogRowHeight * scale;
        var max = new Vector2(left + width, cursorY + rowHeight * count);
        ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale);
        var pad = HealthArt.CardPad * scale;
        HydrationEntry? removed = null;
        for (var row = 0; row < count; row++)
        {
            var entryIndex = count - 1 - row;
            var entry = day.Drinks[entryIndex];
            var rowTop = cursorY + row * rowHeight;
            if (row > 0)
            {
                drawList.AddLine(new Vector2(left + pad, rowTop), new Vector2(max.X, rowTop),
                    ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            var centerY = rowTop + rowHeight * 0.5f;
            var removeCenter = new Vector2(max.X - pad - RemoveRadius * scale, centerY);
            if (GlyphButton(drawList, logRemoveIds[entryIndex], removeCenter, RemoveRadius * scale,
                    FontAwesomeIcon.Times, ui.Accent, ButtonStyle.Plain, true, Loc.T(L.Health.RemoveDrink), ui.MutedInk))
            {
                removed = entry;
            }

            var cursorX = left + pad;
            var timeHeight = Typography.LineHeight(TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(cursorX, centerY - timeHeight * 0.5f),
                Typography.FitText(logTimes[entryIndex], LogTimeWidth * scale, TextStyles.Subheadline), ui.MutedInk,
                TextStyles.Subheadline);
            cursorX += LogTimeWidth * scale;
            var glyph = LogGlyph * scale;
            HealthArt.GlyphTile(drawList, new Vector2(cursorX + glyph * 0.5f, centerY), glyph,
                HealthArt.Tint(HealthMetric.Water), KindIcon(entry.KindKey));
            cursorX += glyph + Metrics.Space.Md * scale;
            var volume = logVolumes[entryIndex];
            var volumeSize = Typography.Measure(volume, TextStyles.BodyEmphasized);
            var volumeRight = removeCenter.X - RemoveRadius * scale - Metrics.Space.Md * scale;
            Typography.Draw(drawList, new Vector2(volumeRight - volumeSize.X, centerY - volumeSize.Y * 0.5f), volume,
                ui.TitleInk, TextStyles.BodyEmphasized);
            var name = Typography.FitText(logNames[entryIndex],
                MathF.Max(1f, volumeRight - volumeSize.X - Metrics.Space.Sm * scale - cursorX), TextStyles.Body);
            var nameHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(cursorX, centerY - nameHeight * 0.5f), name, ui.TitleInk,
                TextStyles.Body);
        }

        if (removed is not null)
        {
            tracker.RemoveDrink(removed);
            UiFeedback.Play(UiSound.ToggleOff);
        }

        return max.Y;
    }

    private void SyncLog(HealthDay day)
    {
        var key = HashCode.Combine(day.Date, day.Drinks.Count, tracker.Revision, Units, Loc.Culture,
            TimeText.FormatVersion);
        if (key == logKey && logTimes.Count == day.Drinks.Count)
        {
            return;
        }

        logKey = key;
        logTimes.Clear();
        logNames.Clear();
        logVolumes.Clear();
        for (var index = 0; index < day.Drinks.Count; index++)
        {
            var entry = day.Drinks[index];
            logTimes.Add(TimeText.Clock(entry.Unix));
            logNames.Add(HealthFormat.DrinkKindName(entry));
            logVolumes.Add(HealthFormat.Volume(entry.Millilitres, Units));
        }

        while (logRemoveIds.Count < day.Drinks.Count)
        {
            logRemoveIds.Add("health.drink.remove." + logRemoveIds.Count);
        }
    }

    private static FontAwesomeIcon KindIcon(string kindKey)
    {
        var index = Array.IndexOf(DrinkKeys.All, kindKey);
        return index < 0 ? FontAwesomeIcon.Water : KindIcons[index];
    }

    private float DrawReminders(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.Reminders), scale);
        var enabled = Profile.HydrationRemindersEnabled;
        var rows = enabled ? 5 : 1;
        var bottom = RowsCard(drawList, new Vector2(left, cursorY), width, rows, scale, out var card);
        var row = CardRow(drawList, card, 0, scale);
        var toggled = ToggleRow(drawList, row, "health.remind.toggle", Loc.T(L.Health.RemindToDrink), enabled, scale);
        if (toggled != enabled)
        {
            Profile.HydrationRemindersEnabled = toggled;
            tracker.SaveNow();
        }

        if (enabled)
        {
            DrawReminderRows(drawList, card, scale);
        }

        return bottom + Footnote(new Vector2(left, bottom), width, Loc.T(L.Health.RemindersHint), scale);
    }

    private void DrawReminderRows(ImDrawListPtr drawList, Rect card, float scale)
    {
        var minutes = Profile.ReminderIntervalMinutes;
        var delta = StepperRow(drawList, CardRow(drawList, card, 1, scale), ReminderIds, Loc.T(L.Health.Every),
            ReminderText(minutes), minutes > MinReminderMinutes, minutes < MaxReminderMinutes, scale);
        if (delta != 0)
        {
            var snapped = (minutes + delta * ReminderStepMinutes) / ReminderStepMinutes * ReminderStepMinutes;
            Profile.ReminderIntervalMinutes = Math.Clamp(snapped, MinReminderMinutes, MaxReminderMinutes);
            tracker.SaveNow();
        }

        var from = QuietRow(drawList, CardRow(drawList, card, 2, scale), Loc.T(L.Health.QuietFrom),
            Profile.QuietStartHour * MinutesPerHour + Profile.QuietStartMinute, scale);
        if (from != Profile.QuietStartHour * MinutesPerHour + Profile.QuietStartMinute)
        {
            Profile.QuietStartHour = from / MinutesPerHour;
            Profile.QuietStartMinute = from % MinutesPerHour;
            tracker.SaveNow();
        }

        var until = QuietRow(drawList, CardRow(drawList, card, 3, scale), Loc.T(L.Health.QuietUntil),
            Profile.QuietEndHour * MinutesPerHour + Profile.QuietEndMinute, scale);
        if (until != Profile.QuietEndHour * MinutesPerHour + Profile.QuietEndMinute)
        {
            Profile.QuietEndHour = until / MinutesPerHour;
            Profile.QuietEndMinute = until % MinutesPerHour;
            tracker.SaveNow();
        }

        var pause = ToggleRow(drawList, CardRow(drawList, card, 4, scale), "health.remind.pause",
            Loc.T(L.Health.PauseDuringDuties), Profile.ReminderPauseInDuties, scale);
        if (pause != Profile.ReminderPauseInDuties)
        {
            Profile.ReminderPauseInDuties = pause;
            tracker.SaveNow();
        }
    }

    private int QuietRow(ImDrawListPtr drawList, Rect row, string label, int minuteOfDay, float scale)
    {
        var fieldWidth = MathF.Min(row.Width * 0.62f, (StepperValueWidth + StepperRadius * 6f) * scale);
        var inset = Metrics.Space.Sm * scale;
        var field = new Rect(new Vector2(row.Max.X - fieldWidth, row.Min.Y + inset),
            new Vector2(row.Max.X, row.Max.Y - inset));
        RowLabel(drawList, row, label, fieldWidth + HealthArt.TileGap * scale, ui.TitleInk);
        return TimeOfDayField.Draw(ui, field, minuteOfDay, scale);
    }

    private string ReminderText(int minutes)
    {
        if (reminderText.IsCurrent(minutes))
        {
            return reminderText.Value;
        }

        return reminderText.Store(minutes, HealthFormat.Duration(minutes * 60d));
    }
}
