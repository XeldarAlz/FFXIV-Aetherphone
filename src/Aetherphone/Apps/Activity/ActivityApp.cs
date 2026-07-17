using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Activity;

internal sealed partial class ActivityApp : IPhoneApp
{
    private const float RowHeight = 58f;
    private const float CompactRowHeight = 46f;
    private const float CardPadding = 8f;
    private const float CardRounding = 18f;
    private const float CardGap = 14f;
    private const float TileSize = 30f;
    private const float StepperRadius = 13f;
    private const float MinLevelsGoal = 0.5f;
    private const float MaxLevelsGoal = 5f;
    private const float LevelsGoalStep = 0.5f;
    private const int MinDutiesGoal = 1;
    private const int MaxDutiesGoal = 10;

    private static readonly long[] GilGoalSteps = { 10000, 25000, 50000, 100000, 250000, 500000, 1000000 };

    public string Id => "character";
    public string DisplayName => Loc.T(L.Character.Activity);
    public string Glyph => "Ac";
    public int BadgeCount => tracker.VenturesReady;

    private readonly GameData gameData;
    private readonly ActivityTracker tracker;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Activity);
    private int screenIndex;

    public ActivityApp(GameData gameData, ActivityTracker tracker, Configuration configuration)
    {
        this.gameData = gameData;
        this.tracker = tracker;
        this.configuration = configuration;
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        ui.Theme = theme;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);
        DrawHeader(content, scale);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        if (!tracker.IsTracking)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Character.LogInToView), AppPalettes.Activity.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawIdentity(scale);
            DrawScreenTabs(scale);
            if (screenIndex == 0)
            {
                DrawRings(scale);
                DrawToday(scale);
                DrawSession(scale);
                DrawRetainers(scale);
                DrawGoals(scale);
            }
            else
            {
                DrawHistory(scale);
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawScreenTabs(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, origin + new Vector2(width, 34f * scale));
        var labels = new[] { Loc.T(L.Character.Today), Loc.T(L.Character.History) };
        screenIndex = SegmentStrip.Draw("character.screens", rect, labels, screenIndex, AppPalettes.Activity);
        ImGui.SetCursorScreenPos(rect.Min);
        ImGui.Dummy(rect.Size);
        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private static void DrawHeader(Rect content, float scale)
    {
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), Loc.T(L.Character.Activity),
            AppPalettes.Activity.TitleInk, 1.15f, FontWeight.SemiBold);
    }

    private void DrawIdentity(float scale)
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return;
        }

        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 12f * scale), player.Name.TextValue,
            AppPalettes.Activity.TitleInk, TextStyles.Title3);
        var jobName = gameData.JobName(player.ClassJob.RowId);
        var world = gameData.WorldName(player.HomeWorld.RowId);
        var detail = jobName.Length > 0 ? $"{jobName} · Lv {player.Level}" : $"Lv {player.Level}";
        if (world.Length > 0)
        {
            detail = $"{detail} · {world}";
        }

        Typography.DrawCentered(new Vector2(centerX, origin.Y + 34f * scale), detail, AppPalettes.Activity.MutedInk,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 52f * scale));
    }

    private float ProgressFraction => ProgressFractionFor(tracker.Today);

    private float AdventureFraction => AdventureFractionFor(tracker.Today);

    private float FortuneFraction => FortuneFractionFor(tracker.Today);

    private float ProgressFractionFor(ActivityDay day) => ActivityGoals.ProgressFraction(configuration, day);

    private float AdventureFractionFor(ActivityDay day) => ActivityGoals.AdventureFraction(configuration, day);

    private float FortuneFractionFor(ActivityDay day) => ActivityGoals.FortuneFraction(configuration, day);

    private void DrawRings(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        UiAnchors.Report("character.rings",
            new Rect(origin, origin + new Vector2(width, ActivityRings.Height * scale)));
        ActivityRings.Draw(AppPalettes.Activity.TitleInk, ProgressFraction, AdventureFraction, FortuneFraction);
        DrawLegend(scale);
    }

    private void DrawLegend(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var third = width / 3f;
        var height = 48f * scale;
        var today = tracker.Today;
        DrawLegendItem(new Vector2(origin.X + third * 0.5f, origin.Y), ActivityRings.RingOneTint,
            Loc.T(L.Character.RingProgress), Percent(ProgressFraction));
        DrawLegendItem(new Vector2(origin.X + third * 1.5f, origin.Y), ActivityRings.RingTwoTint,
            Loc.T(L.Character.RingAdventure), $"{today.DutiesCompleted} / {configuration.ActivityGoalDuties}");
        DrawLegendItem(new Vector2(origin.X + third * 2.5f, origin.Y), ActivityRings.RingThreeTint,
            Loc.T(L.Character.RingFortune), $"{Compact(today.GilEarned)} / {Compact(configuration.ActivityGoalGil)}");
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawLegendItem(Vector2 top, Vector4 tint, string label, string value)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dot = 6f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Callout);
        var dotCenter = new Vector2(top.X - labelSize.X * 0.5f - dot - 5f * scale, top.Y + labelSize.Y * 0.5f);
        ImGui.GetWindowDrawList().AddCircleFilled(dotCenter, dot, ImGui.GetColorU32(tint));
        Typography.Draw(new Vector2(top.X - labelSize.X * 0.5f, top.Y), label, AppPalettes.Activity.MutedInk,
            TextStyles.Callout);
        var valueSize = Typography.Measure(value, TextStyles.Title3);
        Typography.Draw(new Vector2(top.X - valueSize.X * 0.5f, top.Y + labelSize.Y + 5f * scale), value,
            AppPalettes.Activity.TitleInk, TextStyles.Title3);
    }

    private void DrawToday(float scale)
    {
        var today = tracker.Today;
        var hasCollectibles = today.MountsGained + today.MinionsGained > 0;
        var rowCount = hasCollectibles ? 5 : 4;
        ui.SectionLabel(Loc.T(L.Character.Today), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(rowCount, RowHeight, scale);
        UiAnchors.Report("character.summary", card);
        var rowIndex = 0;
        var expDetail = today.LevelsGained > 0
            ? Loc.T(L.Character.LevelsGained, today.LevelsGained)
            : Loc.T(L.Character.PercentOfGoal, PercentValue(ProgressFraction));
        ProgressRow(CardRow(card, rowIndex++, RowHeight, scale), ActivityRings.RingOneTint, FontAwesomeIcon.Bolt,
            Loc.T(L.Character.Experience), "+" + Compact(today.ExpGained), ProgressFraction, expDetail, scale);
        ProgressRow(CardRow(card, rowIndex++, RowHeight, scale), ActivityRings.RingTwoTint, FontAwesomeIcon.Dungeon,
            Loc.T(L.Character.Duties), $"{today.DutiesCompleted} / {configuration.ActivityGoalDuties}",
            AdventureFraction, null, scale);
        ProgressRow(CardRow(card, rowIndex++, RowHeight, scale), ActivityRings.RingThreeTint, FontAwesomeIcon.Coins,
            Loc.T(L.Character.GilEarned), "+" + Number(today.GilEarned), FortuneFraction, null, scale);
        StatRow(CardRow(card, rowIndex++, RowHeight, scale), Accent.Blue, FontAwesomeIcon.Clock,
            Loc.T(L.Character.TimePlayed), Duration(today.PlaySeconds), AppPalettes.Activity.TitleInk, null, scale);
        if (hasCollectibles)
        {
            var detail =
                $"{Loc.T(L.Character.Mounts)} {Number(today.MountsGained)} · {Loc.T(L.Character.Minions)} {Number(today.MinionsGained)}";
            StatRow(CardRow(card, rowIndex, RowHeight, scale), Accent.Violet, FontAwesomeIcon.Dragon,
                Loc.T(L.Character.NewCollectibles), "+" + Number(today.MountsGained + today.MinionsGained),
                AppPalettes.Activity.TitleInk, detail, scale);
        }

        EndCard(card, scale);
    }

    private void DrawSession(float scale)
    {
        var session = tracker.Session;
        ui.SectionLabel(Loc.T(L.Character.ThisSession), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(4, CompactRowHeight, scale);
        var titleInk = AppPalettes.Activity.TitleInk;
        StatRow(CardRow(card, 0, CompactRowHeight, scale), ActivityRings.RingOneTint, FontAwesomeIcon.Bolt,
            Loc.T(L.Character.Experience), "+" + Compact(session.ExpGained), titleInk, null, scale);
        StatRow(CardRow(card, 1, CompactRowHeight, scale), ActivityRings.RingTwoTint, FontAwesomeIcon.Dungeon,
            Loc.T(L.Character.Duties), Number(session.DutiesCompleted), titleInk, null, scale);
        StatRow(CardRow(card, 2, CompactRowHeight, scale), ActivityRings.RingThreeTint, FontAwesomeIcon.Coins,
            Loc.T(L.Character.GilEarned), "+" + Number(session.GilEarned), titleInk, null, scale);
        StatRow(CardRow(card, 3, CompactRowHeight, scale), Accent.Blue, FontAwesomeIcon.Clock,
            Loc.T(L.Character.TimePlayed), Duration(session.PlaySeconds), titleInk, null, scale);
        EndCard(card, scale);
    }

    private void DrawRetainers(float scale)
    {
        if (tracker.RetainerCount <= 0)
        {
            return;
        }

        var card = BeginCard(1, RowHeight, scale);
        string value;
        Vector4 valueInk;
        if (tracker.VenturesReady > 0)
        {
            value = Loc.T(L.Character.VenturesReady, tracker.VenturesReady);
            valueInk = AppPalettes.Activity.Accent;
        }
        else if (tracker.VenturesActive > 0)
        {
            value = Loc.T(L.Character.VenturesActive, tracker.VenturesActive);
            valueInk = AppPalettes.Activity.MutedInk;
        }
        else
        {
            value = Number(tracker.RetainerCount);
            valueInk = AppPalettes.Activity.TitleInk;
        }

        StatRow(CardRow(card, 0, RowHeight, scale), Accent.Amber, FontAwesomeIcon.Briefcase,
            Loc.T(L.Character.Retainers), value, valueInk, null, scale);
        EndCard(card, scale);
    }

    private void DrawGoals(float scale)
    {
        ui.SectionLabel(Loc.T(L.Character.GoalsSection), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(3, CompactRowHeight, scale);
        var levelsValue = Loc.T(L.Character.LevelsShort,
            configuration.ActivityGoalLevels.ToString("0.#", Loc.Culture));
        var levelsDelta = GoalRow(CardRow(card, 0, CompactRowHeight, scale), Loc.T(L.Character.GoalLevels),
            levelsValue, scale);
        if (levelsDelta != 0)
        {
            configuration.ActivityGoalLevels = Math.Clamp(
                configuration.ActivityGoalLevels + levelsDelta * LevelsGoalStep, MinLevelsGoal, MaxLevelsGoal);
            configuration.Save();
        }

        var dutiesDelta = GoalRow(CardRow(card, 1, CompactRowHeight, scale), Loc.T(L.Character.Duties),
            Number(configuration.ActivityGoalDuties), scale);
        if (dutiesDelta != 0)
        {
            configuration.ActivityGoalDuties =
                Math.Clamp(configuration.ActivityGoalDuties + dutiesDelta, MinDutiesGoal, MaxDutiesGoal);
            configuration.Save();
        }

        var gilDelta = GoalRow(CardRow(card, 2, CompactRowHeight, scale), Loc.T(L.Character.GilEarned),
            Compact(configuration.ActivityGoalGil), scale);
        if (gilDelta != 0)
        {
            configuration.ActivityGoalGil = SteppedGilGoal(configuration.ActivityGoalGil, gilDelta);
            configuration.Save();
        }

        EndCard(card, scale);
        ui.HelpText(Loc.T(L.Character.GoalsHint));
    }

    private static long SteppedGilGoal(long current, int delta)
    {
        var index = 0;
        for (var stepIndex = 0; stepIndex < GilGoalSteps.Length; stepIndex++)
        {
            if (Math.Abs(GilGoalSteps[stepIndex] - current) < Math.Abs(GilGoalSteps[index] - current))
            {
                index = stepIndex;
            }
        }

        index = Math.Clamp(index + delta, 0, GilGoalSteps.Length - 1);
        return GilGoalSteps[index];
    }

    private Rect BeginCard(int rowCount, float rowHeight, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = (rowCount * rowHeight + 2f * CardPadding) * scale;
        var rect = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(ImGui.GetWindowDrawList(), rect.Min, rect.Max, CardRounding * scale, elevated: true);
        return rect;
    }

    private static Rect CardRow(Rect card, int rowIndex, float rowHeight, float scale)
    {
        var padding = 14f * scale;
        var top = card.Min.Y + CardPadding * scale + rowIndex * rowHeight * scale;
        return new Rect(new Vector2(card.Min.X + padding, top),
            new Vector2(card.Max.X - padding, top + rowHeight * scale));
    }

    private static void EndCard(Rect card, float scale)
    {
        ImGui.SetCursorScreenPos(card.Min);
        ImGui.Dummy(card.Size);
        ImGui.Dummy(new Vector2(0f, CardGap * scale));
    }

    private static void StatRow(Rect row, Vector4 tint, FontAwesomeIcon icon, string label, string value,
        Vector4 valueInk, string? detail, float scale)
    {
        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile.Draw(tileCenter, tile, tint, icon);
        var textLeft = row.Min.X + tile + 12f * scale;
        if (detail is { Length: > 0 })
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), label, AppPalettes.Activity.TitleInk,
                TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), detail, AppPalettes.Activity.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            var labelSize = Typography.Measure(label, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - labelSize.Y * 0.5f), label,
                AppPalettes.Activity.TitleInk, TextStyles.Headline);
        }

        var valueSize = Typography.Measure(value, 1.02f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, valueInk,
            1.02f, FontWeight.SemiBold);
    }

    private static void ProgressRow(Rect row, Vector4 tint, FontAwesomeIcon icon, string label, string value,
        float fraction, string? detail, float scale)
    {
        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile.Draw(tileCenter, tile, tint, icon);
        var textLeft = row.Min.X + tile + 12f * scale;
        var topY = row.Min.Y + 8f * scale;
        var valueSize = Typography.Measure(value, 1.0f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, topY), value, tint, 1.0f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, topY), label, AppPalettes.Activity.TitleInk, TextStyles.Headline);
        if (detail is { Length: > 0 })
        {
            Typography.Draw(new Vector2(textLeft, topY + 19f * scale), detail, AppPalettes.Activity.MutedInk,
                TextStyles.Caption1);
        }

        var barTop = row.Max.Y - 13f * scale;
        var barMin = new Vector2(textLeft, barTop);
        var barMax = new Vector2(row.Max.X, barTop + 5f * scale);
        var rounding = (barMax.Y - barMin.Y) * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(AppPalettes.Activity.FieldSurface), rounding);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped > 0.001f)
        {
            var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * clamped, barMax.Y);
            drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(tint), rounding);
        }
    }

    private int GoalRow(Rect row, string label, string value, float scale)
    {
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - 8f * scale), label, AppPalettes.Activity.BodyInk,
            TextStyles.Subheadline);
        var radius = StepperRadius * scale;
        var plusCenter = new Vector2(row.Max.X - radius, row.Center.Y);
        var minusCenter = new Vector2(row.Max.X - radius - 106f * scale, row.Center.Y);
        var valueCenter = new Vector2((plusCenter.X + minusCenter.X) * 0.5f, row.Center.Y);
        Typography.DrawCentered(valueCenter, value, AppPalettes.Activity.TitleInk, 0.95f, FontWeight.SemiBold);
        var delta = 0;
        if (ui.IconButton(minusCenter, radius, FontAwesomeIcon.Minus.ToIconString(), AppPalettes.Activity.TitleInk,
                AppPalettes.Activity.FieldSurface, 0.5f))
        {
            delta--;
        }

        if (ui.IconButton(plusCenter, radius, FontAwesomeIcon.Plus.ToIconString(), AppPalettes.Activity.TitleInk,
                AppPalettes.Activity.FieldSurface, 0.5f))
        {
            delta++;
        }

        return delta;
    }

    private static string Percent(float fraction) => $"{PercentValue(fraction)}%";

    private static int PercentValue(float fraction) =>
        (int)MathF.Round(Math.Clamp(fraction, 0f, 9.99f) * 100f);

    private static string Duration(long seconds)
    {
        var minutes = (int)(seconds / 60);
        var hours = minutes / 60;
        if (hours > 0)
        {
            return Loc.T(L.Character.DurationHoursMinutes, hours, minutes % 60);
        }

        return Loc.T(L.Character.DurationMinutes, minutes);
    }

    private static string Compact(long value)
    {
        if (value >= 1_000_000)
        {
            var millions = value / 1_000_000f;
            return millions.ToString(millions >= 10f ? "0" : "0.#", Loc.Culture) + "M";
        }

        if (value >= 1_000)
        {
            var thousands = value / 1_000f;
            return thousands.ToString(thousands >= 10f ? "0" : "0.#", Loc.Culture) + "K";
        }

        return value.ToString(Loc.Culture);
    }

    private static string Number(long value) => value.ToString("N0", Loc.Culture);

    public void Dispose()
    {
    }
}
