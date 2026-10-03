using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Timers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Timers;

internal sealed partial class TimersApp
{
    private const int ResetRows = 3;
    private const int ActivityRows = 4;
    private const long OceanWindowSeconds = 7200;
    private const long MapCooldownSeconds = 64800;
    private const long FashionOpenSeconds = 345600;
    private const long FashionClosedSeconds = 259200;
    private const float RowWashRadius = 12f;
    private const string FishingAppId = "fishing";

    private void DrawResets(DateTime utcNow, long nowUnix, float width, float scale)
    {
        SectionHeader(Loc.T(L.Timers.ServerResets), width, scale, null, false, out _, out _);
        var origin = ImGui.GetCursorScreenPos();
        var height = ResetRows * TimersArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        if (!ImGui.IsRectVisible(origin, max))
        {
            textCursor += ResetRows;
            Advance(origin, width, height, TimersArt.SectionGap, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        TimersArt.Card(drawList, ui, origin, max, scale);
        var daily = Unix(GameSchedule.NextDailyReset(utcNow));
        var grandCompany = Unix(GameSchedule.NextGrandCompanyReset(utcNow));
        var weekly = Unix(GameSchedule.NextWeeklyReset(utcNow));
        if (DrawScheduleRow(drawList, CardRow(origin, max, 0, scale), FontAwesomeIcon.Sun, Accent.Amber,
                Loc.T(L.Timers.DailyReset), daily - DaySeconds, daily, nowUnix, "timers.bell.daily",
                configuration.NotifyDailyReset, scale))
        {
            configuration.NotifyDailyReset = !configuration.NotifyDailyReset;
            SaveToggle();
        }

        if (DrawScheduleRow(drawList, CardRow(origin, max, 1, scale), FontAwesomeIcon.Flag, AccentRing.Orange,
                Loc.T(L.Timers.GrandCompanyReset), grandCompany - DaySeconds, grandCompany, nowUnix,
                "timers.bell.grandCompany", configuration.NotifyGrandCompanyReset, scale))
        {
            configuration.NotifyGrandCompanyReset = !configuration.NotifyGrandCompanyReset;
            SaveToggle();
        }

        if (DrawScheduleRow(drawList, CardRow(origin, max, 2, scale), FontAwesomeIcon.CalendarAlt, Accent.Blue,
                Loc.T(L.Timers.WeeklyReset), weekly - WeekSeconds, weekly, nowUnix, "timers.bell.weekly",
                configuration.NotifyWeeklyReset, scale))
        {
            configuration.NotifyWeeklyReset = !configuration.NotifyWeeklyReset;
            SaveToggle();
        }

        Advance(origin, width, height, TimersArt.SectionGap, scale);
    }

    private void DrawActivities(DateTime utcNow, long nowUnix, float width, float scale)
    {
        SectionHeader(Loc.T(L.Timers.Activities), width, scale, null, false, out _, out _);
        var origin = ImGui.GetCursorScreenPos();
        var height = ActivityRows * TimersArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        if (!ImGui.IsRectVisible(origin, max))
        {
            textCursor += ActivityRows;
            Advance(origin, width, height, TimersArt.SectionGap, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        TimersArt.Card(drawList, ui, origin, max, scale);
        DrawFashionRow(drawList, CardRow(origin, max, 0, scale), utcNow, nowUnix, scale);
        DrawCactpotRow(drawList, CardRow(origin, max, 1, scale), utcNow, nowUnix, scale);
        DrawMapRow(drawList, CardRow(origin, max, 2, scale), nowUnix, scale);
        DrawOceanRow(drawList, CardRow(origin, max, 3, scale), utcNow, nowUnix, scale);
        Advance(origin, width, height, TimersArt.SectionGap, scale);
    }

    private Rect CardRow(Vector2 cardMin, Vector2 cardMax, int index, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var top = cardMin.Y + index * TimersArt.RowHeight * scale;
        if (index > 0)
        {
            TimersArt.Hairline(ImGui.GetWindowDrawList(), ui, cardMin.X + pad, cardMax.X, top);
        }

        return new Rect(new Vector2(cardMin.X + pad, top),
            new Vector2(cardMax.X - pad, top + TimersArt.RowHeight * scale));
    }

    private void DrawFashionRow(ImDrawListPtr drawList, Rect row, DateTime utcNow, long nowUnix, float scale)
    {
        var window = GameSchedule.FashionReport(utcNow);
        var change = Unix(window.NextChangeUtc);
        var span = window.Active ? FashionOpenSeconds : FashionClosedSeconds;
        var text = NextText();
        var detail = FashionDetail(text, window.Active, change);
        if (DrawScheduleRow(drawList, row, FontAwesomeIcon.Tshirt, Accent.Pink, Loc.T(L.Timers.FashionReport),
                change - span, change, nowUnix, "timers.bell.fashion", configuration.NotifyFashionReport, scale,
                text, detail))
        {
            configuration.NotifyFashionReport = !configuration.NotifyFashionReport;
            SaveToggle();
        }
    }

    private void DrawCactpotRow(ImDrawListPtr drawList, Rect row, DateTime utcNow, long nowUnix, float scale)
    {
        var drawing = Unix(GameSchedule.NextJumboCactpot(utcNow, timers.RegionCode));
        if (DrawScheduleRow(drawList, row, FontAwesomeIcon.Coins, AccentRing.Gold, Loc.T(L.Timers.JumboCactpot),
                drawing - WeekSeconds, drawing, nowUnix, "timers.bell.cactpot", configuration.NotifyJumboCactpot,
                scale))
        {
            configuration.NotifyJumboCactpot = !configuration.NotifyJumboCactpot;
            SaveToggle();
        }
    }

    private void DrawMapRow(ImDrawListPtr drawList, Rect row, long nowUnix, float scale)
    {
        var text = NextText();
        var character = TimerLedger.Find(timers.Characters, timers.CurrentContentId);
        var allowance = character?.MapAllowanceUnix ?? 0;
        var valueRight = BellAwareRight(drawList, row, "timers.bell.map", configuration.NotifyMapAllowance, scale,
            out var bellClicked);
        if (bellClicked)
        {
            configuration.NotifyMapAllowance = !configuration.NotifyMapAllowance;
            SaveToggle();
        }

        var content = new Rect(row.Min, new Vector2(valueRight, row.Max.Y));
        if (allowance <= 0)
        {
            DrawRow(drawList, content, FontAwesomeIcon.Map, ui.MutedInk, 0f, false, Loc.T(L.Timers.TreasureMap),
                Loc.T(L.Timers.MapNotSeen), string.Empty, ui.MutedInk, scale);
            return;
        }

        if (allowance <= nowUnix)
        {
            var seen = TimerLabels.Seen(ref text.Detail, character!.MapSeenUnix, string.Empty);
            DrawRow(drawList, content, FontAwesomeIcon.Map, TimersArt.ReadyInk, 1f, true,
                Loc.T(L.Timers.TreasureMap), seen, Loc.T(L.Timers.Ready), TimersArt.ReadyInk, scale);
            return;
        }

        DrawRow(drawList, content, FontAwesomeIcon.Map, Accent.Mint,
            TimerLedger.Progress(allowance - MapCooldownSeconds, allowance, nowUnix), true,
            Loc.T(L.Timers.TreasureMap), text.EndFor(allowance), text.CountdownFor(allowance - nowUnix), ui.TitleInk,
            scale);
    }

    private void DrawOceanRow(ImDrawListPtr drawList, Rect row, DateTime utcNow, long nowUnix, float scale)
    {
        var text = NextText();
        var voyage = GameSchedule.OceanFishing(utcNow, OceanRoute.Indigo);
        var boarding = Unix(voyage.NextBoardingUtc);
        var wash = new Rect(new Vector2(row.Min.X - Metrics.Space.Sm * scale, row.Min.Y + Metrics.Space.Xxs * scale),
            new Vector2(row.Max.X + Metrics.Space.Sm * scale, row.Max.Y - Metrics.Space.Xxs * scale));
        var hovered = UiInteract.Hover(wash.Min, wash.Max);
        if (hovered)
        {
            drawList.AddRectFilled(wash.Min, wash.Max, ImGui.GetColorU32(ui.HoverWash), RowWashRadius * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var detail = OceanDetail(text, voyage, boarding);
        var value = voyage.BoardingNow ? Loc.T(L.Timers.BoardingNow) : text.CountdownFor(boarding - nowUnix);
        var valueInk = voyage.BoardingNow ? ui.Accent : ui.TitleInk;
        var chevronRight = row.Max.X;
        var chevronSize = Typography.LineHeight(TextStyles.Footnote) * 0.6f;
        ProgressRing.CenterIcon(drawList, new Vector2(chevronRight - chevronSize * 0.5f, row.Center.Y),
            FontAwesomeIcon.ChevronRight, ui.MutedInk, chevronSize);
        var content = new Rect(row.Min, new Vector2(chevronRight - chevronSize - Metrics.Space.Sm * scale, row.Max.Y));
        DrawRow(drawList, content, FontAwesomeIcon.Fish, Accent.Teal,
            voyage.BoardingNow ? 1f : TimerLedger.Progress(boarding - OceanWindowSeconds, boarding, nowUnix), true,
            Loc.T(L.Timers.OceanFishing), detail, value, valueInk, scale);
        if (UiInteract.Click(wash.Min, wash.Max, hovered))
        {
            navigation.Open(FishingAppId);
        }
    }

    private bool DrawScheduleRow(ImDrawListPtr drawList, Rect row, FontAwesomeIcon icon, Vector4 tint, string title,
        long startUnix, long endUnix, long nowUnix, string bellId, bool bellOn, float scale,
        TimerText? text = null, string? detail = null)
    {
        text ??= NextText();
        var valueRight = BellAwareRight(drawList, row, bellId, bellOn, scale, out var bellClicked);
        var content = new Rect(row.Min, new Vector2(valueRight, row.Max.Y));
        DrawRow(drawList, content, icon, tint, TimerLedger.Progress(startUnix, endUnix, nowUnix), true, title,
            detail ?? text.EndFor(endUnix), text.CountdownFor(endUnix - nowUnix), ui.TitleInk, scale);
        return bellClicked;
    }

    private float BellAwareRight(ImDrawListPtr drawList, Rect row, string bellId, bool bellOn, float scale,
        out bool clicked)
    {
        var radius = TimersArt.BellHitRadius * scale;
        var center = new Vector2(row.Max.X - radius + Metrics.Space.Xs * scale, row.Center.Y);
        clicked = TimersArt.Bell(drawList, bellId, center, bellOn, ui, BellTooltip(bellOn), scale);
        return center.X - radius;
    }

    private static string FashionDetail(TimerText text, bool active, long changeUnix)
    {
        var key = (changeUnix / 60) ^ (active ? 1L << 50 : 0L) ^ ((long)DateTime.Now.DayOfYear << 52);
        if (text.Detail.IsCurrent(key))
        {
            return text.Detail.Value;
        }

        var moment = TimeText.FutureMoment(changeUnix);
        return text.Detail.Store(key, Loc.T(active ? L.Timers.JudgingUntil : L.Timers.OpensAt, moment));
    }

    private static string OceanDetail(TimerText text, in OceanVoyage voyage, long boardingUnix)
    {
        if (text.Detail.IsCurrent(boardingUnix))
        {
            return text.Detail.Value;
        }

        var timeOfDay = voyage.TimeOfDay switch
        {
            OceanTimeOfDay.Sunset => Loc.T(L.Timers.OceanSunset),
            OceanTimeOfDay.Night => Loc.T(L.Timers.OceanNight),
            _ => Loc.T(L.Timers.OceanDay),
        };
        return text.Detail.Store(boardingUnix, string.Concat(voyage.Route, " · ", timeOfDay));
    }
}
