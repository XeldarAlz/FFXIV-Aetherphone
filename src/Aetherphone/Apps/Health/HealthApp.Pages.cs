using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private readonly string[] drinkKinds = new string[4];
    private readonly string[] unitLabels = new string[3];
    private readonly string[] scopeLabels = new string[4];
    private readonly string[] setupSubtitles = new string[SetupSteps];

    private string[] DrinkKinds()
    {
        drinkKinds[0] = Loc.T(L.Health.DrinkKindWater);
        drinkKinds[1] = Loc.T(L.Health.DrinkKindTea);
        drinkKinds[2] = Loc.T(L.Health.DrinkKindCoffee);
        drinkKinds[3] = Loc.T(L.Health.DrinkKindJuice);
        return drinkKinds;
    }

    private string[] UnitLabels()
    {
        unitLabels[0] = Loc.T(L.Health.UnitEorzean);
        unitLabels[1] = Loc.T(L.Health.UnitMetric);
        unitLabels[2] = Loc.T(L.Health.UnitImperial);
        return unitLabels;
    }

    private string[] ScopeLabels()
    {
        scopeLabels[0] = Loc.T(L.Health.ScopeDaily);
        scopeLabels[1] = Loc.T(L.Health.ScopeWeekly);
        scopeLabels[2] = Loc.T(L.Health.ScopeSession);
        scopeLabels[3] = Loc.T(L.Health.ScopeAllTime);
        return scopeLabels;
    }

    private const int SetupSteps = 5;
    private const int GoalTypeCount = (int)HealthGoalType.Calories + 1;
    private int setupStep;

    private string[] SetupSubtitles()
    {
        setupSubtitles[0] = Loc.T(L.Health.SetupSub1);
        setupSubtitles[1] = Loc.T(L.Health.SetupSub2);
        setupSubtitles[2] = Loc.T(L.Health.SetupSub3);
        setupSubtitles[3] = Loc.T(L.Health.SetupSub4);
        setupSubtitles[4] = Loc.T(L.Health.SetupSub5);
        return setupSubtitles;
    }

    private void DrawSetup(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        DrawStepDots(scale);

        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 14f * scale), Loc.T(L.Health.WelcomeAdventurer),
            Pal.TitleInk,
            TextStyles.Title2);
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 40f * scale), SetupSubtitles()[setupStep], Pal.MutedInk,
            TextStyles.Subheadline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 60f * scale));

        switch (setupStep)
        {
            case 0: SetupUnits(scale); break;
            case 1: SetupGoals(scale); break;
            case 2: SetupEnergy(scale); break;
            case 3: SetupMovement(scale); break;
            default: SetupReview(scale); break;
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        SetupNav(scale);
    }

    private void SetupUnits(float scale)
    {
        DrawSummaryCard(scale);
        StepLabel(Loc.T(L.Health.PreferredUnits), scale);
        if (RadioRow(Loc.T(L.Health.UnitEorzean), Loc.T(L.Health.UnitEorzeanSub), U == HealthUnits.Eorzean, scale))
        {
            SetUnits(HealthUnits.Eorzean);
        }

        if (RadioRow(Loc.T(L.Health.UnitMetric), Loc.T(L.Health.UnitMetricSub), U == HealthUnits.Metric, scale))
        {
            SetUnits(HealthUnits.Metric);
        }

        if (RadioRow(Loc.T(L.Health.UnitImperial), Loc.T(L.Health.UnitImperialSub), U == HealthUnits.Imperial, scale))
        {
            SetUnits(HealthUnits.Imperial);
        }
    }

    private void SetUnits(HealthUnits units)
    {
        if (Profile.Units == units)
        {
            return;
        }

        Profile.Units = units;
        tracker.SaveNow();
    }

    private void SetupGoals(float scale)
    {
        StepLabel(Loc.T(L.Health.DailyGoals), scale);
        var steps = IntField(Loc.T(L.Health.Steps), "##hp.setup.steps", Profile.DailyStepGoal, 1000, 1000, 100000, scale);
        if (steps != Profile.DailyStepGoal)
        {
            Profile.DailyStepGoal = steps;
            tracker.MarkDirty();
        }

        var swim = FloatField(Loc.T(L.Health.SwimmingYalms), "##hp.setup.swim", Profile.DailySwimGoalYalms, 100, 100, 100000, 0,
            scale);
        if (Math.Abs(swim - Profile.DailySwimGoalYalms) > 0.01)
        {
            Profile.DailySwimGoalYalms = swim;
            tracker.MarkDirty();
        }

        var drinks = IntField(Loc.T(L.Health.HydrationDrinks), "##hp.setup.drinks", Profile.DailyHydrationGoal, 1, 1, 20, scale);
        if (drinks != Profile.DailyHydrationGoal)
        {
            Profile.DailyHydrationGoal = drinks;
            tracker.MarkDirty();
        }
    }

    private void SetupEnergy(float scale)
    {
        StepLabel(Loc.T(L.Health.FictionalEnergy), scale);
        ui.HelpText(Loc.T(L.Health.WeightHint));
        ui.LabelValue(Loc.T(L.Health.Current),
            Profile.WeightKg is { } kg ? HealthFormat.Weight(kg, U) : Loc.T(L.Health.NotSet));
        ui.Field(Loc.T(L.Health.WeightLabel, WeightUnitLabel()), "##health.setup.weight", ref weightBuffer, 8, false);
        if (WideButton(Loc.T(L.Health.SetWeight), false, scale, 30f) &&
            double.TryParse(weightBuffer, NumberStyles.Any, Loc.Culture, out var value) && value > 0)
        {
            Profile.WeightKg = HealthFormat.WeightToKg(value, U);
            tracker.SaveNow();
        }

        DrawWeightSuggestions(scale);

        var calories = Profile.CaloriesEnabled;
        ui.ToggleRow(Loc.T(L.Health.EstimateActivityEnergy), ref calories);
        if (calories != Profile.CaloriesEnabled)
        {
            Profile.CaloriesEnabled = calories;
            tracker.MarkDirty();
        }
    }

    private void SetupMovement(float scale)
    {
        StepLabel(Loc.T(L.Health.Movement), scale);
        ui.LabelValue(Loc.T(L.Health.Height),
            Loc.T(L.Health.HeightWithSource, HealthFormat.Height(tracker.HeightCm, U), HeightSourceLabel()));
        var strideDelta = StepperRow(Loc.T(L.Health.YalmsPerStep),
            Profile.StrideYalms.ToString("0.00", Loc.Culture), scale);
        if (strideDelta != 0)
        {
            Profile.StrideYalms = Math.Clamp(Profile.StrideYalms + strideDelta * 0.05, 0.30, 1.50);
            tracker.MarkDirty();
        }

        if (WideButton(Loc.T(L.Health.SuggestStrideFromHeight), false, scale, 30f))
        {
            Profile.StrideYalms = HealthFormat.SuggestStride(tracker.HeightCm);
            tracker.SaveNow();
        }

        ui.HelpText(Loc.T(L.Health.StrideHintSetup));
    }

    private void SetupReview(float scale)
    {
        StepLabel(Loc.T(L.Health.Review), scale);
        var card = BeginCard(6, 38f, scale);
        KeyRow(CardRow(card, 0, 38f, scale), Loc.T(L.Health.Units), UnitLabels()[(int)U], scale);
        KeyRow(CardRow(card, 1, 38f, scale), Loc.T(L.Health.StepsGoal), HealthFormat.Number(Profile.DailyStepGoal), scale);
        KeyRow(CardRow(card, 2, 38f, scale), Loc.T(L.Health.SwimGoal), Dist(Profile.DailySwimGoalYalms), scale);
        KeyRow(CardRow(card, 3, 38f, scale), Loc.T(L.Health.HydrationGoal),
            Loc.T(L.Health.DrinksSuffix, Profile.DailyHydrationGoal), scale);
        KeyRow(CardRow(card, 4, 38f, scale), Loc.T(L.Health.Weight),
            Profile.WeightKg is { } kg ? HealthFormat.Weight(kg, U) : Loc.T(L.Health.NotSet), scale);
        KeyRow(CardRow(card, 5, 38f, scale), Loc.T(L.Health.EnergyEstimates),
            Loc.T(Profile.CaloriesEnabled ? L.Health.On : L.Health.Off), scale);
        EndCard(card, scale);
        ui.HelpText(Loc.T(L.Health.DisclaimerShort));
    }

    private void SetupNav(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 44f * scale;
        var gap = 10f * scale;
        var last = setupStep >= SetupSteps - 1;

        if (setupStep > 0)
        {
            var half = (width - gap) * 0.5f;
            var backRect = new Rect(origin, origin + new Vector2(half, height));
            var nextRect = new Rect(new Vector2(origin.X + half + gap, origin.Y),
                new Vector2(origin.X + width, origin.Y + height));
            if (AppSkin.PillButton(backRect, Loc.T(L.Health.Back), false, true, ui.Theme))
            {
                setupStep--;
            }

            if (AppSkin.PillButton(nextRect, Loc.T(last ? L.Health.Begin : L.Health.Next), true, true, ui.Theme))
            {
                Advance(last);
            }
        }
        else
        {
            var nextRect = new Rect(origin, origin + new Vector2(width, height));
            if (AppSkin.PillButton(nextRect, Loc.T(L.Health.Next), true, true, ui.Theme))
            {
                Advance(false);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void Advance(bool finish)
    {
        if (finish)
        {
            Profile.SetupCompleted = true;
            if (Profile.Goals.Count == 0)
            {
                Profile.Goals = HealthTracker.DefaultGoals(Profile.DailySwimGoalYalms);
            }

            setupStep = 0;
            tracker.SaveNow();
            return;
        }

        setupStep = Math.Min(setupStep + 1, SetupSteps - 1);
    }

    private void DrawStepDots(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var radius = 12f * scale;
        var padX = radius + 6f * scale;
        var usable = width - 2f * padX;
        var cy = origin.Y + radius + 4f * scale;
        var accent = ImGui.GetColorU32(Pal.Accent);
        var idle = ImGui.GetColorU32(Pal.FieldSurface);

        for (var index = 0; index < SetupSteps - 1; index++)
        {
            var x0 = origin.X + padX + usable * (index / (float)(SetupSteps - 1));
            var x1 = origin.X + padX + usable * ((index + 1) / (float)(SetupSteps - 1));
            drawList.AddLine(new Vector2(x0, cy), new Vector2(x1, cy), index < setupStep ? accent : idle, 2f * scale);
        }

        for (var index = 0; index < SetupSteps; index++)
        {
            var cx = origin.X + padX + usable * (index / (float)(SetupSteps - 1));
            var center = new Vector2(cx, cy);
            var done = index <= setupStep;
            drawList.AddCircleFilled(center, radius, done ? accent : idle, 24);
            Typography.DrawCentered(center, (index + 1).ToString(Loc.Culture),
                done ? new Vector4(1f, 1f, 1f, 1f) : Pal.MutedInk, TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, radius * 2f + 14f * scale));
    }

    private void DrawSummaryCard(float scale)
    {
        var player = gameData.LocalPlayer;
        var name = player?.Name.TextValue ?? Loc.T(L.Health.Adventurer);
        var world = player is not null ? gameData.WorldName(player.HomeWorld.RowId) : string.Empty;
        EnsureIdentity();
        var race = cachedRace;
        var clan = cachedClan;
        var raceClan = race.Length > 0 && clan.Length > 0
            ? Loc.T(L.Health.RaceClanValue, race, clan)
            : race.Length > 0 ? race : "-";

        ui.SectionLabel(Loc.T(L.Health.ProfileSummary), TextStyles.FootnoteEmphasized, 4f);
        var card = BeginCard(4, 40f, scale);
        KeyRow(CardRow(card, 0, 40f, scale), Loc.T(L.Health.Name), name, scale);
        KeyRow(CardRow(card, 1, 40f, scale), Loc.T(L.Health.World), world.Length > 0 ? world : "-", scale);
        KeyRow(CardRow(card, 2, 40f, scale), Loc.T(L.Health.RaceClan), raceClan, scale);
        KeyRow(CardRow(card, 3, 40f, scale), Loc.T(L.Health.Height),
            Loc.T(L.Health.HeightWithSource, HealthFormat.Height(tracker.HeightCm, U), HeightSourceLabel()), scale);
        EndCard(card, scale);
    }

    private void KeyRow(Rect row, string label, string value, float scale)
    {
        var minLabelWidth = row.Width * 0.35f;
        var valueNaturalSize = Typography.Measure(value, TextStyles.Headline);
        var valueMaxWidth = MathF.Max(1f,
            MathF.Min(valueNaturalSize.X, row.Width - minLabelWidth - 10f * scale));
        Marquee.DrawRightAuto("health.keyrow.value." + label, value, row.Max.X,
            row.Center.Y - valueNaturalSize.Y * 0.5f, valueMaxWidth, TextStyles.Headline, Pal.TitleInk);
        var labelMaxWidth = MathF.Max(1f, row.Width - valueMaxWidth - 10f * scale);
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Marquee.DrawLeftAuto("health.keyrow.label." + label, label, row.Min.X, row.Center.Y - labelSize.Y * 0.5f,
            labelMaxWidth, TextStyles.Subheadline, Pal.MutedInk);
    }

    private void StepLabel(string text, float scale)
    {
        ui.SectionLabel(Loc.T(L.Health.StepOf, setupStep + 1, SetupSteps, text), TextStyles.FootnoteEmphasized, 8f);
    }

    private bool RadioRow(string title, string subtitle, bool selected, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 48f * scale;
        var min = origin;
        var max = origin + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        var fill = selected ? Pal.Accent with { W = 0.18f } : Pal.FieldSurface with { W = Pal.FieldSurface.W * (hovered ? 1.4f : 1f) };
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), 12f * scale);
        if (selected)
        {
            drawList.AddRect(min, max, ImGui.GetColorU32(Pal.Accent), 12f * scale, ImDrawFlags.RoundCornersAll, 1.5f * scale);
        }

        var cy = origin.Y + height * 0.5f;
        var cx = origin.X + 20f * scale;
        var ring = 8f * scale;
        drawList.AddCircle(new Vector2(cx, cy), ring, ImGui.GetColorU32(selected ? Pal.Accent : Pal.MutedInk), 24,
            2f * scale);
        if (selected)
        {
            drawList.AddCircleFilled(new Vector2(cx, cy), ring * 0.5f, ImGui.GetColorU32(Pal.Accent), 16);
        }

        var textLeft = cx + 18f * scale;
        if (subtitle.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, cy - 15f * scale), title, Pal.TitleInk, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, cy + 3f * scale), subtitle, Pal.MutedInk, TextStyles.Footnote);
        }
        else
        {
            var size = Typography.Measure(title, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, cy - size.Y * 0.5f), title, Pal.TitleInk, TextStyles.Headline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
        return UiInteract.HoverClick(min, max);
    }

    private void DrawHydration(float scale)
    {
        var day = Profile.LatestDay ?? new HealthDay();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 14f * scale),
            Loc.T(L.Health.DrinksToday, day.DrinkCount, Profile.DailyHydrationGoal), Pal.TitleInk, TextStyles.Title3);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 34f * scale));

        if (WideButton(Loc.T(L.Health.DrinkWater), true, scale, 46f))
        {
            tracker.LogDrink(DrinkKeys.Water, string.Empty, 250);
        }

        var chipOrigin = ImGui.GetCursorScreenPos();
        var centerY = chipOrigin.Y + 16f * scale;
        var cursorX = chipOrigin.X;
        var kinds = DrinkKinds();
        for (var index = 0; index < kinds.Length; index++)
        {
            if (ui.FlowChip(ref cursorX, centerY, 8f * scale, kinds[index], false))
            {
                tracker.LogDrink(DrinkKeys.All[index], string.Empty, 250);
            }
        }

        ImGui.SetCursorScreenPos(chipOrigin);
        ImGui.Dummy(new Vector2(width, 40f * scale));

        ui.SectionLabel(Loc.T(L.Health.CustomDrink), TextStyles.FootnoteEmphasized, 6f);
        ui.Field(Loc.T(L.Health.Name), "##health.customName", ref customDrinkName, 24, false);
        var serving = IntField(Loc.T(L.Health.ServingMl), "##hp.water.serving", customDrinkMl, 50, 50, 2000, scale);
        if (serving != customDrinkMl)
        {
            customDrinkMl = serving;
        }

        if (WideButton(Loc.T(L.Health.LogCustomDrink), false, scale))
        {
            tracker.LogDrink(string.Empty, customDrinkName, customDrinkMl);
        }

        if (WideButton(Loc.T(L.Health.UndoLastDrink), false, scale))
        {
            tracker.UndoLastDrink();
        }

        var goalDrinks = IntField(Loc.T(L.Health.DailyGoalDrinks), "##hp.water.goal", Profile.DailyHydrationGoal, 1, 1, 20, scale);
        if (goalDrinks != Profile.DailyHydrationGoal)
        {
            Profile.DailyHydrationGoal = goalDrinks;
            tracker.SaveNow();
        }

        ui.SectionLabel(Loc.T(L.Health.Today), TextStyles.FootnoteEmphasized, 6f);
        if (day.Drinks.Count == 0)
        {
            ui.HelpText(Loc.T(L.Health.NoDrinksToday));
        }
        else
        {
            for (var index = day.Drinks.Count - 1; index >= 0; index--)
            {
                var entry = day.Drinks[index];
                var time = TimeText.Clock(entry.Unix);
                ui.LabelValue(Loc.T(L.Health.DrinkEntry, time, HealthFormat.DrinkKindName(entry)),
                    HealthFormat.Volume(entry.Millilitres, U));
            }
        }

        DrawReminderSettings(scale);
    }

    private void DrawReminderSettings(float scale)
    {
        ui.SectionLabel(Loc.T(L.Health.Reminders), TextStyles.FootnoteEmphasized, 8f);
        var enabled = Profile.HydrationRemindersEnabled;
        ui.ToggleRow(Loc.T(L.Health.HydrationReminders), ref enabled);
        if (enabled != Profile.HydrationRemindersEnabled)
        {
            Profile.HydrationRemindersEnabled = enabled;
            tracker.SaveNow();
        }

        if (!Profile.HydrationRemindersEnabled)
        {
            return;
        }

        var every = IntField(Loc.T(L.Health.EveryMinutes), "##hp.remind.every", Profile.ReminderIntervalMinutes, 5, 1, 720, scale);
        if (every != Profile.ReminderIntervalMinutes)
        {
            Profile.ReminderIntervalMinutes = every;
            tracker.SaveNow();
        }

        var (fromHour, fromMinute) = TimeField(Loc.T(L.Health.QuietFrom), "##hp.remind.from", Profile.QuietStartHour,
            Profile.QuietStartMinute, scale);
        if (fromHour != Profile.QuietStartHour || fromMinute != Profile.QuietStartMinute)
        {
            Profile.QuietStartHour = fromHour;
            Profile.QuietStartMinute = fromMinute;
            tracker.SaveNow();
        }

        var (untilHour, untilMinute) = TimeField(Loc.T(L.Health.QuietUntil), "##hp.remind.until", Profile.QuietEndHour,
            Profile.QuietEndMinute, scale);
        if (untilHour != Profile.QuietEndHour || untilMinute != Profile.QuietEndMinute)
        {
            Profile.QuietEndHour = untilHour;
            Profile.QuietEndMinute = untilMinute;
            tracker.SaveNow();
        }

        var pause = Profile.ReminderPauseInDuties;
        ui.ToggleRow(Loc.T(L.Health.PauseDuringDuties), ref pause);
        if (pause != Profile.ReminderPauseInDuties)
        {
            Profile.ReminderPauseInDuties = pause;
            tracker.SaveNow();
        }
    }

    private void DrawGoals(float scale)
    {
        for (var index = 0; index < Profile.Goals.Count; index++)
        {
            var goal = Profile.Goals[index];
            GoalBar(goal, scale);
            if (editingGoalId == goal.Id)
            {
                DrawGoalEditor(goal, scale);
            }
            else if (WideButton(Loc.T(goal.Enabled ? L.Health.Edit : L.Health.EditDisabled), false, scale, 30f))
            {
                editingGoalId = goal.Id;
                goalNameBuffer = HealthFormat.GoalName(goal);
            }

            ImGui.Dummy(new Vector2(0f, 6f * scale));
        }

        if (WideButton(Loc.T(L.Health.AddGoal), true, scale))
        {
            var goal = new HealthGoal { NameKey = GoalKeys.New, Type = HealthGoalType.Steps, Target = 1000 };
            Profile.Goals.Add(goal);
            editingGoalId = goal.Id;
            goalNameBuffer = HealthFormat.GoalName(goal);
            tracker.SaveNow();
        }

        if (WideButton(Loc.T(L.Health.ResetDefaultGoals), false, scale))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Health.ResetGoalsTitle),
                Message = Loc.T(L.Health.ResetGoalsMessage),
                ConfirmLabel = Loc.T(L.Health.Reset),
                CancelLabel = Loc.T(L.Health.Cancel),
                Confirm = () =>
                {
                    Profile.Goals = HealthTracker.DefaultGoals(Profile.DailySwimGoalYalms);
                    editingGoalId = null;
                    tracker.SaveNow();
                },
            });
        }
    }

    private void DrawGoalEditor(HealthGoal goal, float scale)
    {
        ui.Field(Loc.T(L.Health.Name), "##health.goalName", ref goalNameBuffer, 40, false);

        var typeDelta = StepperRow(Loc.T(L.Health.Type), GoalTypeLabel(goal.Type), scale);
        if (typeDelta != 0)
        {
            goal.Type = Cycle(goal.Type, typeDelta);
            tracker.SaveNow();
        }

        var scopeDelta = StepperRow(Loc.T(L.Health.Scope), ScopeLabels()[(int)goal.Scope], scale);
        if (scopeDelta != 0)
        {
            goal.Scope = (HealthGoalScope)(((int)goal.Scope + scopeDelta + 4) % 4);
            tracker.SaveNow();
        }

        var target = FloatField(Loc.T(L.Health.Target), "##hp.goalTarget", goal.Target, GoalStep(goal.Type), 1, 10_000_000, 0,
            scale);
        if (Math.Abs(target - goal.Target) > 0.001)
        {
            goal.Target = target;
            goal.CompletedKey = string.Empty;
            tracker.SaveNow();
        }

        var enabled = goal.Enabled;
        ui.ToggleRow(Loc.T(L.Health.Enabled), ref enabled);
        if (enabled != goal.Enabled)
        {
            goal.Enabled = enabled;
            tracker.SaveNow();
        }

        if (WideButton(Loc.T(L.Health.DeleteGoal), false, scale, 30f))
        {
            Profile.Goals.Remove(goal);
            editingGoalId = null;
            tracker.SaveNow();
            return;
        }

        if (WideButton(Loc.T(L.Health.Done), true, scale, 30f))
        {
            var typed = goalNameBuffer.Trim();
            if (typed.Length > 0 && typed != HealthFormat.GoalName(goal))
            {
                goal.Name = typed;
                goal.NameKey = string.Empty;
            }
            editingGoalId = null;
            tracker.SaveNow();
        }
    }

    private static string GoalTypeLabel(HealthGoalType type) => Loc.T(type switch
    {
        HealthGoalType.Steps => L.Health.TypeSteps,
        HealthGoalType.OnFootDistance => L.Health.TypeOnFootDistance,
        HealthGoalType.WalkDistance => L.Health.TypeWalkingDistance,
        HealthGoalType.RunDistance => L.Health.TypeRunningDistance,
        HealthGoalType.SwimDistance => L.Health.TypeSwimmingDistance,
        HealthGoalType.ActiveTime => L.Health.TypeActiveTime,
        HealthGoalType.HydrationCount => L.Health.TypeDrinksLogged,
        HealthGoalType.HydrationVolume => L.Health.TypeDrinkVolume,
        HealthGoalType.Teleports => L.Health.TypeTeleports,
        HealthGoalType.TeleportDistance => L.Health.TypeTeleportDistance,
        _ => L.Health.TypeEnergy,
    });

    private static double GoalStep(HealthGoalType type) => type switch
    {
        HealthGoalType.Steps => 500,
        HealthGoalType.ActiveTime => 300,
        HealthGoalType.HydrationCount or HealthGoalType.Teleports => 1,
        HealthGoalType.HydrationVolume => 250,
        HealthGoalType.Calories => 50,
        _ => 100,
    };

    private static HealthGoalType Cycle(HealthGoalType type, int delta)
    {
        return (HealthGoalType)(((int)type + delta + GoalTypeCount) % GoalTypeCount);
    }

    private void DrawHistory(float scale)
    {
        var days = Profile.Days;
        if (days.Count == 0)
        {
            ui.HelpText(Loc.T(L.Health.NoActivity));
            return;
        }

        var shown = 0;
        for (var index = days.Count - 1; index >= 0 && shown < 7; index--, shown++)
        {
            var day = days[index];
            var steps = HealthFormat.Steps(day.OnFootYalms, Profile.StrideYalms);
            ui.SectionLabel(Loc.T(L.Health.HistoryDayHeader, FormatDate(day.Date), day.GoalsCompleted, day.Teleports),
                TextStyles.FootnoteEmphasized, 6f);
            var card = BeginCard(4, CompactRowHeight, scale);
            StatRow(CardRow(card, 0, CompactRowHeight, scale), Accent1, FontAwesomeIcon.Walking,
                Loc.T(L.Health.StepsValue, HealthFormat.Number(steps)), Dist(day.OnFootYalms), scale);
            StatRow(CardRow(card, 1, CompactRowHeight, scale), Accent2, FontAwesomeIcon.Clock,
                Loc.T(L.Health.Active), HealthFormat.Duration(day.ActiveSeconds), scale);
            StatRow(CardRow(card, 2, CompactRowHeight, scale), Accent4, FontAwesomeIcon.Tint,
                Loc.T(L.Health.Hydration), Loc.T(L.Health.DrinksValue, day.DrinkCount), scale);
            var kcal = Profile.CaloriesEnabled
                ? Loc.T(L.Health.Kcal, day.Calories.ToString("0", Loc.Culture))
                : "-";
            StatRow(CardRow(card, 3, CompactRowHeight, scale), Accent3, FontAwesomeIcon.Fire, Loc.T(L.Health.Energy),
                kcal, scale);
            EndCard(card, scale);
        }
    }

    private static string FormatDate(string key)
    {
        return DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var parsed)
            ? parsed.ToString("ddd, MMM d", Loc.Culture)
            : key;
    }

    private void DrawProfile(float scale)
    {
        DrawSummaryCard(scale);

        BeginPanel(Loc.T(L.Health.Height), scale);
        InfoRow(Loc.T(L.Health.Reading),
            Loc.T(L.Health.HeightWithSource, HealthFormat.Height(tracker.HeightCm, U), HeightSourceLabel()), scale);
        if (WideButton(Loc.T(L.Health.RefreshHeight), false, scale, 30f))
        {
            tracker.RefreshHeight();
        }

        var autoHeight = PanelToggle(Loc.T(L.Health.AutoRefreshHeight), Profile.AutoRefreshHeight, scale);
        if (autoHeight != Profile.AutoRefreshHeight)
        {
            Profile.AutoRefreshHeight = autoHeight;
            tracker.SaveNow();
        }

        var manualDelta = StepperRow(Loc.T(L.Health.ManualOverrideCm),
            Profile.ManualHeightCm is { } m ? m.ToString("0.0", Loc.Culture) : Loc.T(L.Health.OverrideOff), scale);
        if (manualDelta != 0)
        {
            var baseCm = Profile.ManualHeightCm ?? (tracker.HeightCm > 0 ? tracker.HeightCm : 170);
            Profile.ManualHeightCm = Math.Clamp(baseCm + manualDelta * 0.5, 50, 260);
            tracker.RefreshHeight();
            tracker.SaveNow();
        }

        if (Profile.ManualHeightCm is not null && WideButton(Loc.T(L.Health.ClearOverride), false, scale, 30f))
        {
            Profile.ManualHeightCm = null;
            tracker.RefreshHeight();
            tracker.SaveNow();
        }

        EndPanel(scale);

        BeginPanel(Loc.T(L.Health.FictionalWeight), scale);
        InfoRow(Loc.T(L.Health.Current),
            Profile.WeightKg is { } kg ? HealthFormat.Weight(kg, U) : Loc.T(L.Health.NotSet), scale);
        PanelField(Loc.T(L.Health.EnterWeight, WeightUnitLabel()), "##health.weight", ref weightBuffer, 8, scale);
        if (WideButton(Loc.T(L.Health.SetWeight), false, scale, 30f) &&
            double.TryParse(weightBuffer, NumberStyles.Any, Loc.Culture, out var value) && value > 0)
        {
            Profile.WeightKg = HealthFormat.WeightToKg(value, U);
            tracker.SaveNow();
        }

        if (Profile.WeightKg is not null && WideButton(Loc.T(L.Health.ClearWeight), false, scale, 30f))
        {
            Profile.WeightKg = null;
            weightBuffer = string.Empty;
            tracker.SaveNow();
        }

        DrawWeightSuggestions(scale);

        var calories = PanelToggle(Loc.T(L.Health.EstimateActivityEnergy), Profile.CaloriesEnabled, scale);
        if (calories != Profile.CaloriesEnabled)
        {
            Profile.CaloriesEnabled = calories;
            tracker.SaveNow();
        }

        PanelHint(Loc.T(L.Health.WeightHint), scale);
        EndPanel(scale);

        BeginPanel(Loc.T(L.Health.Units), scale);
        var units = Segmented("health.units", UnitLabels(), (int)U, scale);
        if (units != (int)U)
        {
            Profile.Units = (HealthUnits)units;
            weightBuffer = string.Empty;
            tracker.SaveNow();
        }

        EndPanel(scale);

        BeginPanel(Loc.T(L.Health.StrideLength), scale);
        var strideDelta = StepperRow(Loc.T(L.Health.YalmsPerStep),
            Profile.StrideYalms.ToString("0.00", Loc.Culture), scale);
        if (strideDelta != 0)
        {
            Profile.StrideYalms = Math.Clamp(Profile.StrideYalms + strideDelta * 0.05, 0.30, 1.50);
            tracker.SaveNow();
        }

        if (WideButton(Loc.T(L.Health.SuggestFromHeight), false, scale, 30f))
        {
            Profile.StrideYalms = HealthFormat.SuggestStride(tracker.HeightCm);
            tracker.SaveNow();
        }

        PanelHint(Loc.T(L.Health.StrideHint), scale);
        EndPanel(scale);

        BeginPanel(Loc.T(L.Health.TrackingStatus), scale);
        InfoRow(Loc.T(L.Health.Status), tracker.TrackingStatus, scale);
        EndPanel(scale);

        BeginPanel(Loc.T(L.Health.ResetSection), scale);
        if (WideButton(Loc.T(L.Health.ResetSession), false, scale, 30f))
        {
            tracker.ResetSession();
        }

        if (WideButton(Loc.T(L.Health.ResetToday), false, scale, 30f))
        {
            AskReset(Loc.T(L.Health.ResetTodayConfirm), tracker.ResetToday);
        }

        if (WideButton(Loc.T(L.Health.ResetTodayHydration), false, scale, 30f))
        {
            AskReset(Loc.T(L.Health.ResetTodayHydrationConfirm), tracker.ResetTodayHydration);
        }

        if (WideButton(Loc.T(L.Health.ResetHistory), false, scale, 30f))
        {
            AskReset(Loc.T(L.Health.ResetHistoryConfirm), tracker.ResetHistory);
        }

        if (WideButton(Loc.T(L.Health.ResetRecords), false, scale, 30f))
        {
            AskReset(Loc.T(L.Health.ResetRecordsConfirm), tracker.ResetRecords);
        }

        if (WideButton(Loc.T(L.Health.ResetAll), false, scale, 30f))
        {
            AskReset(Loc.T(L.Health.ResetAllConfirm), tracker.ResetAll);
        }

        EndPanel(scale);

        PanelHint(Loc.T(L.Health.Disclaimer), scale);
    }

    private void AskReset(string message, Action confirmed)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Health.Confirm),
            Message = message,
            ConfirmLabel = Loc.T(L.Health.Reset),
            CancelLabel = Loc.T(L.Health.Cancel),
            Confirm = confirmed,
        });
    }

    private string HeightSourceLabel() => Loc.T(tracker.HeightSource switch
    {
        HeightSource.Manual => L.Health.HeightSourceManual,
        HeightSource.Game => L.Health.HeightSourceGame,
        _ => L.Health.HeightSourceUnavailable,
    });

    private string WeightUnitLabel() => Loc.T(U switch
    {
        HealthUnits.Metric => L.Health.WeightUnitKg,
        HealthUnits.Imperial => L.Health.WeightUnitLb,
        _ => L.Health.WeightUnitPonz,
    });

    private void DrawWeightSuggestions(float scale)
    {
        var cm = tracker.HeightCm;
        if (cm <= 0)
        {
            return;
        }

        PanelLabel(Loc.T(L.Health.SuggestedTapToUse), scale);
        var suggestions = WeightSuggestions(cm);
        for (var index = 0; index < suggestions.Length; index++)
        {
            var (label, kg) = suggestions[index];
            if (WideButton(Loc.T(L.Health.SuggestionEntry, label, HealthFormat.Weight(kg, U)), false, scale, 30f))
            {
                Profile.WeightKg = kg;
                weightBuffer = string.Empty;
                tracker.SaveNow();
            }
        }

        PanelHint(Loc.T(L.Health.SuggestionHint), scale);
    }

    private void PanelLabel(string text, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(basePos.X + groupPad, basePos.Y), text, Pal.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, 18f * scale));
    }

    private readonly (string Label, double Kg)[] weightSuggestions = new (string, double)[3];
    private double suggestionHeightCm = -1;
    private double suggestionBuild = -1;

    private (string Label, double Kg)[] WeightSuggestions(double cm)
    {
        EnsureIdentity();
        var build = cachedBuild;
        if (Math.Abs(cm - suggestionHeightCm) > 0.01 || Math.Abs(build - suggestionBuild) > 0.0001)
        {
            suggestionHeightCm = cm;
            suggestionBuild = build;
            var metres = cm / 100d;
            weightSuggestions[0].Kg = Math.Round(19.5 * metres * metres * build);
            weightSuggestions[1].Kg = Math.Round(23.0 * metres * metres * build);
            weightSuggestions[2].Kg = Math.Round(26.5 * metres * metres * build);
        }

        weightSuggestions[0].Label = Loc.T(L.Health.SuggestLean);
        weightSuggestions[1].Label = Loc.T(L.Health.SuggestAverage);
        weightSuggestions[2].Label = Loc.T(L.Health.SuggestSturdy);
        return weightSuggestions;
    }

    private double ReadBuildFactor()
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return 1.0;
        }

        try
        {
            var customize = player.Customize;
            if (customize.Length >= 1)
            {
                return customize[0] switch
                {
                    3 => 1.12,
                    5 => 1.18,
                    7 => 1.20,
                    2 => 0.95,
                    8 => 0.93,
                    _ => 1.0,
                };
            }
        }
        catch
        {
        }

        return 1.0;
    }

    private ulong identityContentId;
    private string cachedRace = string.Empty;
    private string cachedClan = string.Empty;
    private double cachedBuild = 1.0;

    private void EnsureIdentity()
    {
        var id = tracker.CharacterId;
        if (id == identityContentId)
        {
            return;
        }

        identityContentId = id;
        ReadIdentity(out cachedRace, out cachedClan);
        cachedBuild = ReadBuildFactor();
    }

    private void ReadIdentity(out string race, out string clan)
    {
        race = string.Empty;
        clan = string.Empty;
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return;
        }

        try
        {
            var customize = player.Customize;
            if (customize.Length >= 5)
            {
                var female = customize[1] == 1;
                race = gameData.RaceName(customize[0], female);
                clan = gameData.ClanName(customize[4], female);
            }
        }
        catch
        {
        }
    }

    private int StepperRow(string label, string value, float scale, float rowHeight = 40f)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var row = new Rect(origin, origin + new Vector2(width, rowHeight * scale));
        var radius = 13f * scale;
        var plus = new Vector2(row.Max.X - radius, row.Center.Y);
        var minus = new Vector2(row.Max.X - radius - 96f * scale, row.Center.Y);
        var stepperLabel = Typography.FitText(label, minus.X - radius - row.Min.X - 8f * scale,
            TextStyles.Subheadline);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - 8f * scale), stepperLabel, Pal.BodyInk,
            TextStyles.Subheadline);
        var valueCenter = new Vector2((plus.X + minus.X) * 0.5f, row.Center.Y);
        Typography.DrawCentered(valueCenter, value, Pal.TitleInk, 0.95f, FontWeight.SemiBold);
        var delta = 0;
        if (ui.IconButton(minus, radius, FontAwesomeIcon.Minus.ToIconString(), Pal.TitleInk, Pal.FieldSurface, 0.5f))
        {
            delta--;
        }

        if (ui.IconButton(plus, radius, FontAwesomeIcon.Plus.ToIconString(), Pal.TitleInk, Pal.FieldSurface, 0.5f))
        {
            delta++;
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, (rowHeight + 6f) * scale));
        return delta;
    }

    private string? activeNumberId;

    private int IntField(string label, string id, int value, int step, int min, int max, float scale)
    {
        NumberField(label, scale, out var inputPos, out var inputWidth, out var boxCenter, out var dec, out var inc,
            out var basePos, out var full, out var rowHeight);
        var v = Math.Clamp(value + (dec ? -step : 0) + (inc ? step : 0), min, max);
        var active = activeNumberId == id;
        ImGui.SetCursorScreenPos(inputPos);
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, active ? Pal.TitleInk : AppSkin.Transparent))
        {
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt(id, ref v, 0, 0);
        }

        UpdateActiveNumber(id);
        if (activeNumberId != id)
        {
            Typography.DrawCentered(boxCenter, v.ToString(Loc.Culture), Pal.TitleInk,
                TextStyles.Headline);
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight));
        return Math.Clamp(v, min, max);
    }

    private double FloatField(string label, string id, double value, double step, double min, double max,
        int decimals, float scale)
    {
        NumberField(label, scale, out var inputPos, out var inputWidth, out var boxCenter, out var dec, out var inc,
            out var basePos, out var full, out var rowHeight);
        var v = (float)Math.Clamp(value + (dec ? -step : 0) + (inc ? step : 0), min, max);
        var active = activeNumberId == id;
        ImGui.SetCursorScreenPos(inputPos);
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, active ? Pal.TitleInk : AppSkin.Transparent))
        {
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputFloat(id, ref v, 0f, 0f, decimals == 0 ? "%.0f" : "%.2f");
        }

        UpdateActiveNumber(id);
        if (activeNumberId != id)
        {
            Typography.DrawCentered(boxCenter, v.ToString(decimals == 0 ? "0" : "0.00", Loc.Culture), Pal.TitleInk,
                TextStyles.Headline);
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight));
        return Math.Clamp(v, min, max);
    }

    private void UpdateActiveNumber(string id)
    {
        if (ImGui.IsItemActive())
        {
            activeNumberId = id;
        }
        else if (activeNumberId == id)
        {
            activeNumberId = null;
        }
    }

    private void NumberField(string label, float scale, out Vector2 inputPos, out float inputWidth,
        out Vector2 boxCenter, out bool dec, out bool inc, out Vector2 basePos, out float full, out float rowHeight)
    {
        full = ImGui.GetContentRegionAvail().X;
        basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var frameHeight = ImGui.GetFrameHeight();
        rowHeight = frameHeight + 10f * scale;
        var centerY = basePos.Y + rowHeight * 0.5f;

        var radius = 13f * scale;
        var gap = 8f * scale;
        inputWidth = 88f * scale;
        var rightEdge = origin.X + width;
        var plusCenter = new Vector2(rightEdge - radius, centerY);
        var inputRight = plusCenter.X - radius - gap;
        var inputLeft = inputRight - inputWidth;
        var minusCenter = new Vector2(inputLeft - gap - radius, centerY);

        var labelMaxWidth = MathF.Max(1f, minusCenter.X - radius - gap - origin.X);
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Marquee.DrawLeftAuto("health.numberfield.label." + label, label, origin.X, centerY - labelSize.Y * 0.5f,
            labelMaxWidth, TextStyles.Subheadline, Pal.BodyInk);

        dec = ui.IconButton(minusCenter, radius, FontAwesomeIcon.Minus.ToIconString(), Pal.TitleInk,
            Pal.FieldSurface, 0.5f);
        inc = ui.IconButton(plusCenter, radius, FontAwesomeIcon.Plus.ToIconString(), Pal.TitleInk,
            Pal.FieldSurface, 0.5f);

        var boxMin = new Vector2(inputLeft, centerY - frameHeight * 0.5f);
        var boxMax = new Vector2(inputRight, centerY + frameHeight * 0.5f);
        ImGui.GetWindowDrawList().AddRectFilled(boxMin, boxMax, ImGui.GetColorU32(Pal.FieldSurface), 8f * scale);
        boxCenter = new Vector2((inputLeft + inputRight) * 0.5f, centerY);
        inputPos = boxMin;
    }

    private int IntBox(string id, float inputLeft, float centerY, float inputWidth, float frameHeight, int value,
        int min, int max, string overlayFormat, float scale)
    {
        var boxMin = new Vector2(inputLeft, centerY - frameHeight * 0.5f);
        var boxMax = new Vector2(inputLeft + inputWidth, centerY + frameHeight * 0.5f);
        ImGui.GetWindowDrawList().AddRectFilled(boxMin, boxMax, ImGui.GetColorU32(Pal.FieldSurface), 8f * scale);
        var v = Math.Clamp(value, min, max);
        var active = activeNumberId == id;
        ImGui.SetCursorScreenPos(boxMin);
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, active ? Pal.TitleInk : AppSkin.Transparent))
        {
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt(id, ref v, 0, 0);
        }

        UpdateActiveNumber(id);
        if (activeNumberId != id)
        {
            Typography.DrawCentered(new Vector2((boxMin.X + boxMax.X) * 0.5f, centerY),
                v.ToString(overlayFormat, Loc.Culture), Pal.TitleInk, TextStyles.Headline);
        }

        return Math.Clamp(v, min, max);
    }

    private (int Hour, int Minute) TimeField(string label, string id, int hour, int minute, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var frameHeight = ImGui.GetFrameHeight();
        var rowHeight = frameHeight + 10f * scale;
        var centerY = basePos.Y + rowHeight * 0.5f;
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(new Vector2(origin.X, centerY - labelSize.Y * 0.5f), label, Pal.BodyInk,
            TextStyles.Subheadline);

        var boxWidth = 54f * scale;
        var colonGap = 16f * scale;
        var rightEdge = origin.X + width;
        var minuteLeft = rightEdge - boxWidth;
        var colonX = minuteLeft - colonGap * 0.5f;
        var hourLeft = minuteLeft - colonGap - boxWidth;
        var h = IntBox(id + ".h", hourLeft, centerY, boxWidth, frameHeight, hour, 0, 23, "00", scale);
        Typography.DrawCentered(new Vector2(colonX, centerY), ":", Pal.TitleInk, TextStyles.Headline);
        var m = IntBox(id + ".m", minuteLeft, centerY, boxWidth, frameHeight, minute, 0, 59, "00", scale);

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight));
        return (h, m);
    }

    private int Segmented(string id, string[] options, int selected, float scale, float rowHeight = 32f)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var rect = new Rect(origin, origin + new Vector2(width, rowHeight * scale));
        var result = SegmentStrip.Draw(id, rect, options, selected, Pal);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, (rowHeight + 6f) * scale));
        return result;
    }

    private Vector2 panelStart;
    private float panelWidth;

    private void BeginPanel(string title, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);
        panelStart = ImGui.GetCursorScreenPos();
        panelWidth = ImGui.GetContentRegionAvail().X;
        ImGui.Dummy(new Vector2(panelWidth, 10f * scale));
        groupPad = 14f * scale;
        ImGui.Indent(groupPad);
        ui.SectionLabel(title, TextStyles.Caption1, 0f);
        ImGui.Unindent(groupPad);
        ImGui.Dummy(new Vector2(panelWidth, 4f * scale));
    }

    private void EndPanel(float scale)
    {
        ImGui.Dummy(new Vector2(panelWidth, 10f * scale));
        var end = ImGui.GetCursorScreenPos();
        groupPad = 0f;
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSetCurrent(0);
        ui.Card(drawList, panelStart, new Vector2(panelStart.X + panelWidth, end.Y), 18f * scale, elevated: true);
        drawList.ChannelsMerge();
        ImGui.Dummy(new Vector2(panelWidth, 12f * scale));
    }

    private void InfoRow(string label, string value, float scale, float rowHeight = 34f)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        KeyRow(new Rect(origin, origin + new Vector2(full - groupPad * 2f, rowHeight * scale)), label, value, scale);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight * scale));
    }

    private bool PanelToggle(string label, bool value, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var height = 34f * scale;
        var row = new Rect(origin, origin + new Vector2(width, height));
        var trackWidth = 44f * scale;
        var toggleLabel = Typography.FitText(label, width - trackWidth - 10f * scale, TextStyles.Subheadline);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - 8f * scale), toggleLabel, Pal.BodyInk,
            TextStyles.Subheadline);
        var trackHeight = 24f * scale;
        var trackMin = new Vector2(row.Max.X - trackWidth, row.Center.Y - trackHeight * 0.5f);
        var trackMax = new Vector2(row.Max.X, row.Center.Y + trackHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(value ? Pal.Accent : Pal.FieldSurface),
            trackHeight * 0.5f);
        var knobRadius = trackHeight * 0.5f - 3f * scale;
        var knobX = value ? trackMax.X - knobRadius - 3f * scale : trackMin.X + knobRadius + 3f * scale;
        drawList.AddCircleFilled(new Vector2(knobX, row.Center.Y), knobRadius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 20);
        var clicked = UiInteract.HoverClick(row.Min, row.Max);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, height + 6f * scale));
        return clicked ? !value : value;
    }

    private void PanelField(string label, string id, ref string value, int maxLength, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        Typography.Draw(origin, label, Pal.MutedInk, TextStyles.Footnote);
        var boxTop = origin.Y + 18f * scale;
        var boxHeight = 32f * scale;
        var min = new Vector2(origin.X, boxTop);
        var max = new Vector2(origin.X + width, boxTop + boxHeight);
        ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(Pal.FieldSurface), 8f * scale);
        ImGui.SetCursorScreenPos(new Vector2(min.X + 10f * scale, boxTop + boxHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, Pal.TitleInk))
        {
            ImGui.InputText(id, ref value, maxLength, ImGuiInputTextFlags.None);
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, (18f + 32f + 8f) * scale));
    }

    private void PanelHint(string text, float scale)
    {
        if (groupPad > 0f)
        {
            ImGui.Indent(groupPad);
        }

        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, Pal.MutedInk))
        {
            Typography.Wrapped(text);
        }

        ImGui.PopTextWrapPos();
        if (groupPad > 0f)
        {
            ImGui.Unindent(groupPad);
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
    }
}
