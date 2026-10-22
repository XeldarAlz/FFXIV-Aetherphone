using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
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
    private const float GoalCardHeight = 84f;
    private const float GoalRingRadius = 26f;
    private const float GoalRingThickness = 7f;
    private const float GoalRingTrackAlpha = 0.22f;
    private const float GoalDisabledAlpha = 0.45f;
    private const float GhostButtonHeight = 44f;
    private const float TypeTileHeight = 58f;
    private const float TypeGlyph = 18f;
    private const float TypeHoverAlpha = 0.10f;
    private const float TypeRestAlpha = 0.05f;
    private const float TypeInkAlpha = 0.7f;
    private const int TypeColumns = 4;
    private const float ScopeStripHeight = 32f;
    private const int MaxNameLength = 40;
    private const int GoalStepGrowth = 20;
    private const int GoalStepBoost = 5;
    private const double MaxGoalTarget = 10_000_000d;

    private static readonly StepperIds GoalTargetIds = StepperIds.For("health.goal.target");

    private static readonly FontAwesomeIcon[] GoalIcons =
    {
        FontAwesomeIcon.ShoePrints,
        FontAwesomeIcon.Route,
        FontAwesomeIcon.Walking,
        FontAwesomeIcon.Running,
        FontAwesomeIcon.Swimmer,
        FontAwesomeIcon.Stopwatch,
        FontAwesomeIcon.Tint,
        FontAwesomeIcon.Water,
        FontAwesomeIcon.LocationArrow,
        FontAwesomeIcon.Route,
        FontAwesomeIcon.Fire,
    };

    private static readonly string[] TypeTileIds =
    {
        "health.goaltype.0", "health.goaltype.1", "health.goaltype.2", "health.goaltype.3", "health.goaltype.4",
        "health.goaltype.5", "health.goaltype.6", "health.goaltype.7", "health.goaltype.8", "health.goaltype.9",
        "health.goaltype.10",
    };

    private readonly NavBarButton[] goalButtons = new NavBarButton[1];
    private readonly Sheet goalSheet = new();
    private readonly string[] scopeLabels = new string[4];
    private readonly List<string> goalCardIds = new();
    private readonly List<CachedText> goalLines = new();
    private readonly List<Spring> goalFills = new();
    private HealthGoal? goalEditing;
    private HealthGoalType draftType;
    private HealthGoalScope draftScope;
    private double draftTarget;
    private bool draftEnabled;
    private string draftName = string.Empty;
    private string draftNameOriginal = string.Empty;
    private CachedText draftTargetText;

    private void DrawGoals(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("health.goals"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var goals = Profile.Goals;
            var cursorY = origin.Y;
            if (goals.Count == 0)
            {
                cursorY += HealthArt.State(drawList, ui, origin, width, FontAwesomeIcon.Bullseye, ui.Accent,
                    Loc.T(L.Health.GoalsEmptyTitle), Loc.T(L.Health.GoalsEmptyBody), scale);
            }

            EnsureGoalSlots(goals.Count);
            for (var index = 0; index < goals.Count; index++)
            {
                if (index > 0)
                {
                    cursorY += HealthArt.TileGap * scale;
                }

                var rect = new Rect(new Vector2(origin.X, cursorY),
                    new Vector2(origin.X + width, cursorY + GoalCardHeight * scale));
                if (index == 0)
                {
                    UiAnchors.Report("health.goal", rect);
                }

                DrawGoalCard(drawList, rect, goals[index], index, scale);
                cursorY = rect.Max.Y;
            }

            cursorY += HealthArt.TileGap * scale;
            var restore = new Rect(new Vector2(origin.X, cursorY),
                new Vector2(origin.X + width, cursorY + GhostButtonHeight * scale));
            if (ui.GhostButton(restore, Loc.T(L.Health.ResetDefaultGoals)))
            {
                AskRestoreGoals();
            }

            cursorY = restore.Max.Y + Footnote(new Vector2(origin.X, restore.Max.Y), width, Loc.T(L.Health.GoalsHint),
                scale);
            ReserveTo(origin, width, cursorY + HealthArt.BottomBreathing * scale);
        }

        goalButtons[0] = new NavBarButton(PhoneIcons.Plus, Loc.T(L.Health.AddGoal));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "health.nav.goals", Loc.T(L.Health.Goals),
            NavBarStyle.From(ui), goalButtons, DisplayName, back);
        if (pressed == 0)
        {
            OpenGoalSheet(null);
        }
    }

    private void EnsureGoalSlots(int count)
    {
        while (goalCardIds.Count < count)
        {
            goalCardIds.Add("health.goalcard." + goalCardIds.Count);
            goalLines.Add(default);
            goalFills.Add(default);
        }
    }

    private void DrawGoalCard(ImDrawListPtr drawList, Rect rect, HealthGoal goal, int index, float scale)
    {
        if (!ImGui.IsRectVisible(rect.Min, rect.Max))
        {
            return;
        }

        if (PressableCard(drawList, rect, goalCardIds[index], goalSheet.CapturesPointer, out var card))
        {
            OpenGoalSheet(goal);
        }

        var (current, target) = tracker.GoalProgress(goal);
        var fraction = target > 0d ? (float)Math.Clamp(current / target, 0d, 1d) : 0f;
        var fill = goalFills[index];
        var shown = fill.Step(goal.Enabled ? fraction : 0f, Motion.Sheet, HealthArt.FrameDelta());
        goalFills[index] = fill;
        var alpha = goal.Enabled ? 1f : GoalDisabledAlpha;
        var tint = Palette.WithAlpha(GoalTint(goal.Type), alpha);
        var pad = HealthArt.CardPad * scale;
        var radius = GoalRingRadius * scale;
        var thickness = GoalRingThickness * scale;
        var center = new Vector2(card.Min.X + pad + radius, card.Center.Y);
        var ringRadius = radius - thickness * 0.5f;
        drawList.AddCircle(center, ringRadius, ImGui.GetColorU32(Palette.WithAlpha(tint, GoalRingTrackAlpha * alpha)),
            48, thickness);
        if (shown > 0.001f)
        {
            ProgressRing.Fill(drawList, center, ringRadius, thickness, shown, tint);
        }

        var complete = goal.Enabled && current + 0.0001d >= target;
        ProgressRing.CenterIcon(drawList, center, complete ? FontAwesomeIcon.Check : GoalIcons[(int)goal.Type], tint,
            radius * 0.62f);
        var chevron = ChevronSize * scale;
        HealthArt.Chevron(drawList, new Vector2(card.Max.X - pad - chevron * 0.5f, card.Center.Y), chevron, ui.MutedInk,
            scale);
        var textLeft = center.X + radius + HealthArt.TileGap * scale;
        var textWidth = MathF.Max(1f, card.Max.X - pad - chevron - ChevronGap * scale - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = card.Center.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(HealthFormat.GoalName(goal), textWidth, TextStyles.Headline),
            Palette.WithAlpha(ui.TitleInk, alpha), TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(GoalLine(goal, index, current), textWidth, TextStyles.Subheadline),
            complete ? tint : ui.MutedInk, TextStyles.Subheadline);
    }

    private string GoalLine(HealthGoal goal, int index, double current)
    {
        var quantized = goal.Type == HealthGoalType.ActiveTime ? Math.Floor(current / 60d) : Math.Floor(current);
        var key = HashCode.Combine(quantized, goal.Target, goal.Type, goal.Scope, goal.Enabled, Units);
        var cache = goalLines[index];
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var text = goal.Enabled ? HealthText.GoalProgress(goal, current, Units) : Loc.T(L.Health.GoalOff);
        cache.Store(key, text);
        goalLines[index] = cache;
        return text;
    }

    private static Vector4 GoalTint(HealthGoalType type) => type switch
    {
        HealthGoalType.Steps => HealthArt.Tint(HealthMetric.Steps),
        HealthGoalType.SwimDistance => HealthArt.Tint(HealthMetric.Swimming),
        HealthGoalType.ActiveTime => HealthArt.Tint(HealthMetric.ActiveTime),
        HealthGoalType.HydrationCount or HealthGoalType.HydrationVolume => HealthArt.Tint(HealthMetric.Water),
        HealthGoalType.Teleports or HealthGoalType.TeleportDistance => HealthArt.TeleportTint,
        HealthGoalType.Calories => HealthArt.EnergyTint,
        _ => HealthArt.Tint(HealthMetric.Distance),
    };

    private void AskRestoreGoals()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Health.ResetGoalsTitle),
            Message = Loc.T(L.Health.ResetGoalsMessage),
            ConfirmLabel = Loc.T(L.Health.Reset),
            CancelLabel = Loc.T(L.Health.Cancel),
            Confirm = RestoreGoals,
        });
    }

    private void RestoreGoals()
    {
        Profile.Goals = HealthTracker.DefaultGoals(Profile.DailySwimGoalYalms);
        goalSheet.CloseImmediately();
        tracker.SaveNow();
    }

    private void OpenGoalSheet(HealthGoal? goal)
    {
        goalEditing = goal;
        draftType = goal?.Type ?? HealthGoalType.Steps;
        draftScope = goal?.Scope ?? HealthGoalScope.Daily;
        draftTarget = goal?.Target ?? DefaultTarget(HealthGoalType.Steps);
        draftEnabled = goal?.Enabled ?? true;
        draftName = goal is null ? string.Empty : HealthFormat.GoalName(goal);
        draftNameOriginal = draftName;
        UiFeedback.Play(UiSound.Tap);
        goalSheet.Open();
    }

    private void DrawGoalSheet(Rect screen)
    {
        if (!goalSheet.CapturesPointer)
        {
            return;
        }

        var scale = UiScale.Current;
        var rows = (GoalTypeCount + TypeColumns - 1) / TypeColumns;
        var gap = Metrics.Space.Sm * scale;
        var editing = goalEditing is not null;
        var height = (SheetMetrics.GrabberZone + SheetTitleHeight + SheetGap + GlassField.HeightUnits + SheetGap +
                      ScopeStripHeight + SheetGap + SheetValueHeight + SheetGap + SheetButtonHeight +
                      Metrics.Size.HomeIndicatorInset) * scale + rows * TypeTileHeight * scale + (rows - 1) * gap +
                     SheetGap * scale + (editing ? (RowHeight + SheetButtonHeight + SheetGap) * scale : 0f);
        using var layer = ScreenLayer.Begin("health.goalSheet", screen, false);
        var frame = goalSheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Fitted(height),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var left = content.Min.X + SheetPadX * scale;
        var right = content.Max.X - SheetPadX * scale;
        var y = content.Min.Y;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, y + SheetTitleHeight * scale * 0.5f),
            Loc.T(editing ? L.Health.EditGoal : L.Health.NewGoal), ink, TextStyles.Headline);
        y += (SheetTitleHeight + SheetGap) * scale;
        var field = new Rect(new Vector2(left, y), new Vector2(right, y + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        GlassField.Text(field, "##health.goalName", GoalTypeLabel(draftType), ref draftName, theme, scale,
            MaxNameLength, false, ImGuiInputTextFlags.None);
        y = field.Max.Y + SheetGap * scale;
        y = DrawTypeGrid(drawList, left, right, y, ink, scale) + SheetGap * scale;
        scopeLabels[0] = Loc.T(L.Health.ScopeDaily);
        scopeLabels[1] = Loc.T(L.Health.ScopeWeekly);
        scopeLabels[2] = Loc.T(L.Health.ScopeSession);
        scopeLabels[3] = Loc.T(L.Health.ScopeAllTime);
        var strip = new Rect(new Vector2(left, y), new Vector2(right, y + ScopeStripHeight * scale));
        var scope = SegmentStrip.Draw("health.goal.scope", strip, scopeLabels, (int)draftScope, ui.Palette,
            ScopeStripHeight);
        if (scope != (int)draftScope)
        {
            UiFeedback.Play(UiSound.Tap);
            draftScope = (HealthGoalScope)scope;
        }

        y = strip.Max.Y + SheetGap * scale;
        var valueRow = new Rect(new Vector2(left, y), new Vector2(right, y + SheetValueHeight * scale));
        var step = TargetStep(draftType, draftTarget);
        var delta = Stepper(drawList, valueRow, GoalTargetIds, DraftTargetText(), draftTarget - step >= step * 0.999d,
            draftTarget + step <= MaxGoalTarget, GoalTint(draftType), ink, TextStyles.Title2, scale);
        if (delta != 0)
        {
            draftTarget = Math.Clamp(Math.Round((draftTarget + delta * step) / step) * step, step, MaxGoalTarget);
        }

        y = valueRow.Max.Y + SheetGap * scale;
        if (editing)
        {
            var row = new Rect(new Vector2(left, y), new Vector2(right, y + RowHeight * scale));
            draftEnabled = ToggleRow(drawList, row, "health.goal.enabled", Loc.T(L.Health.Enabled), draftEnabled, ink,
                scale);
            y = row.Max.Y + SheetGap * scale;
        }

        var save = new Rect(new Vector2(left, y), new Vector2(right, y + SheetButtonHeight * scale));
        if (frame.Interactive && Button.Draw(save, Loc.T(editing ? L.Health.Done : L.Health.AddGoal), ui.Ink))
        {
            SaveGoal();
        }

        if (editing)
        {
            y = save.Max.Y + SheetGap * scale;
            var delete = new Rect(new Vector2(left, y), new Vector2(right, y + SheetButtonHeight * scale));
            if (frame.Interactive && ui.DangerGhostButton(delete, Loc.T(L.Health.DeleteGoal)))
            {
                DeleteGoal();
            }
        }

        goalSheet.End(in frame);
    }

    private float DrawTypeGrid(ImDrawListPtr drawList, float left, float right, float top, Vector4 ink, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var tileWidth = (right - left - gap * (TypeColumns - 1)) / TypeColumns;
        var tileHeight = TypeTileHeight * scale;
        var bottom = top;
        for (var type = 0; type < GoalTypeCount; type++)
        {
            var column = type % TypeColumns;
            var row = type / TypeColumns;
            var min = new Vector2(left + (tileWidth + gap) * column, top + (tileHeight + gap) * row);
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            bottom = tile.Max.Y;
            var goalType = (HealthGoalType)type;
            var selected = goalType == draftType;
            var tint = GoalTint(goalType);
            var hovered = UiInteract.Hover(tile.Min, tile.Max);
            var radius = Metrics.Radius.Md * scale;
            var washAlpha = selected ? KindSelectedAlpha : hovered ? TypeHoverAlpha : TypeRestAlpha;
            Squircle.Fill(drawList, tile.Min, tile.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(selected ? tint : ink, washAlpha)));
            if (selected)
            {
                Squircle.Stroke(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(tint), Metrics.Stroke.Thin * scale);
            }

            var labelHeight = Typography.LineHeight(TextStyles.Caption1);
            var glyphCenter = new Vector2(tile.Center.X, tile.Center.Y - labelHeight * 0.5f);
            ProgressRing.CenterIcon(drawList, glyphCenter, GoalIcons[type], selected ? tint : Palette.WithAlpha(ink, TypeInkAlpha),
                TypeGlyph * scale);
            var label = Typography.FitText(GoalTypeLabel(goalType), tile.Width - Metrics.Space.Xs * scale,
                TextStyles.Caption1);
            var labelSize = Typography.Measure(label, TextStyles.Caption1);
            Typography.Draw(drawList,
                new Vector2(tile.Center.X - labelSize.X * 0.5f, glyphCenter.Y + TypeGlyph * scale * 0.6f), label,
                selected ? ink : Palette.WithAlpha(ink, TypeInkAlpha), TextStyles.Caption1);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                HoverTooltip.Show(TypeTileIds[type], tile, GoalTypeLabel(goalType), HoverLabelSide.Above);
            }

            if (!selected && UiInteract.Click(tile.Min, tile.Max, hovered))
            {
                UiFeedback.Play(UiSound.Tap);
                if (draftType != goalType)
                {
                    draftTarget = DefaultTarget(goalType);
                }

                draftType = goalType;
            }
        }

        return bottom;
    }

    private string DraftTargetText()
    {
        var key = HashCode.Combine(draftTarget, draftType, Units);
        return draftTargetText.IsCurrent(key)
            ? draftTargetText.Value
            : draftTargetText.Store(key, HealthText.GoalAmount(draftType, draftTarget, Units));
    }

    private void SaveGoal()
    {
        var goal = goalEditing;
        if (goal is null)
        {
            goal = new HealthGoal();
            Profile.Goals.Add(goal);
        }

        var changed = goal.Type != draftType || goal.Scope != draftScope || Math.Abs(goal.Target - draftTarget) > 0.0001d;
        goal.Type = draftType;
        goal.Scope = draftScope;
        goal.Target = draftTarget;
        goal.Enabled = draftEnabled;
        if (changed)
        {
            goal.CompletedKey = string.Empty;
        }

        var typed = draftName.Trim();
        var renamed = !string.Equals(typed, draftNameOriginal, StringComparison.Ordinal);
        if (renamed || typed.Length == 0 || (changed && goal.NameKey.Length > 0))
        {
            goal.NameKey = string.Empty;
            goal.Name = renamed ? typed : string.Empty;
        }

        tracker.SaveNow();
        UiFeedback.Play(UiSound.Success);
        goalSheet.Close();
    }

    private void DeleteGoal()
    {
        if (goalEditing is not null)
        {
            Profile.Goals.Remove(goalEditing);
            tracker.SaveNow();
        }

        UiFeedback.Play(UiSound.ToggleOff);
        goalSheet.Close();
    }

    private static double DefaultTarget(HealthGoalType type) => type switch
    {
        HealthGoalType.Steps => 10_000d,
        HealthGoalType.ActiveTime => 1_800d,
        HealthGoalType.HydrationCount => 8d,
        HealthGoalType.HydrationVolume => 2_000d,
        HealthGoalType.Teleports => 5d,
        HealthGoalType.Calories => 300d,
        HealthGoalType.SwimDistance => 500d,
        _ => HealthFormat.YalmsPerMalm,
    };

    private static double TargetStep(HealthGoalType type, double target)
    {
        var step = type switch
        {
            HealthGoalType.Steps => 500d,
            HealthGoalType.ActiveTime => 300d,
            HealthGoalType.HydrationCount or HealthGoalType.Teleports => 1d,
            HealthGoalType.HydrationVolume => 250d,
            HealthGoalType.Calories => 50d,
            _ => 100d,
        };
        return target >= step * GoalStepGrowth ? step * GoalStepBoost : step;
    }

    private static string GoalTypeLabel(HealthGoalType type) => HealthFormat.GoalTypeName(type);

    private const int GoalTypeCount = (int)HealthGoalType.Calories + 1;
}
