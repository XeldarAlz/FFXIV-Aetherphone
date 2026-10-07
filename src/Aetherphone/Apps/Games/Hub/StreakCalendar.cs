using System.Globalization;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal static class StreakGrid
{
    public const int Weeks = 5;
    public const int Days = 7;
    public const int Cells = Weeks * Days;

    public static int Weekday(int dayIndex) => (dayIndex % Days + Days + 1) % Days;

    public static int Column(int dayIndex, DayOfWeek firstDay) => (Weekday(dayIndex) - (int)firstDay + Days) % Days;

    public static int Start(int today, DayOfWeek firstDay) => today - Column(today, firstDay) - (Weeks - 1) * Days;

    public static int TodayCell(int today, DayOfWeek firstDay) => (Weeks - 1) * Days + Column(today, firstDay);

    public static ulong Mask(GameStatsStore stats, int start, int today)
    {
        var mask = 0UL;
        for (var cell = 0; cell < Cells; cell++)
        {
            var day = start + cell;
            if (day <= today && stats.DailyDoneOn(day))
            {
                mask |= 1UL << cell;
            }
        }

        return mask;
    }

    public static bool Done(ulong mask, int cell) => cell is >= 0 and < Cells && ((mask >> cell) & 1UL) != 0UL;

    public static bool StartsRun(ulong mask, int cell) =>
        Done(mask, cell) && (cell % Days == 0 || !Done(mask, cell - 1));

    public static int RunEnd(ulong mask, int cell)
    {
        var rowEnd = cell - cell % Days + Days - 1;
        var end = cell;
        while (end < rowEnd && Done(mask, end + 1))
        {
            end++;
        }

        return end;
    }
}

internal sealed class StreakCalendar
{
    private const float Padding = Metrics.Space.Lg;
    private const float RowPitch = 38f;
    private const float Dot = 30f;
    private const float FutureDot = 6f;
    private const float RunAlpha = 0.32f;
    private const float FooterIcon = 36f;
    private const float CheckSize = 22f;
    private const float HeaderGap = Metrics.Space.Sm;
    private const float WeekdayGap = Metrics.Space.Xs;
    private const float FooterGap = Metrics.Space.Md;
    private const int Segments = 32;
    private const string PlayId = "games.profile.calendar.play";

    private readonly string[] weekdayInitials = new string[StreakGrid.Days];
    private CultureInfo? weekdayCulture;
    private DayOfWeek firstDay;
    private ulong doneMask;
    private int maskToday = -1;
    private int maskStreak = -1;
    private int maskBest = -1;
    private bool maskDone;
    private int gridStart;
    private CachedText bestText;
    private DailyCountdown countdown;
    private string todaySource = string.Empty;
    private string todayLabel = string.Empty;
    private LanguageInfo? todayLanguage;

    public static float Height(float scale) =>
        (Padding * 2f + HeaderGap + WeekdayGap + StreakGrid.Weeks * RowPitch + FooterGap * 2f) * scale
        + Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Footnote)
        + FooterHeight(scale);

    private static float FooterHeight(float scale) =>
        MathF.Max(FooterIcon * scale,
            Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Footnote));

    public bool Draw(ImDrawListPtr drawList, AppSkin ui, GameStatsStore stats, IMiniGame daily, Vector2 origin,
        float width, float scale)
    {
        Sync(stats);
        var max = new Vector2(origin.X + width, origin.Y + Height(scale));
        ui.Card(drawList, origin, max, HubMetrics.CardRadius * scale);
        var pad = Padding * scale;
        var left = origin.X + pad;
        var right = max.X - pad;
        var y = origin.Y + pad;
        DrawHeader(drawList, ui, stats, left, right, y);
        y += Typography.LineHeight(TextStyles.Headline) + HeaderGap * scale;
        var pitch = (right - left) / StreakGrid.Days;
        DrawWeekdays(drawList, ui, left, y, pitch);
        y += Typography.LineHeight(TextStyles.Footnote) + WeekdayGap * scale;
        DrawGrid(drawList, ui, left, y, pitch, scale);
        y += StreakGrid.Weeks * RowPitch * scale + FooterGap * scale;
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);
        y += FooterGap * scale;
        return DrawFooter(drawList, ui, stats, daily, left, right, y, scale);
    }

    private void Sync(GameStatsStore stats)
    {
        var culture = Loc.Culture;
        if (!ReferenceEquals(weekdayCulture, culture))
        {
            weekdayCulture = culture;
            firstDay = culture.DateTimeFormat.FirstDayOfWeek;
            var names = culture.DateTimeFormat.ShortestDayNames;
            for (var column = 0; column < StreakGrid.Days; column++)
            {
                weekdayInitials[column] = names[((int)firstDay + column) % StreakGrid.Days];
            }

            maskToday = -1;
        }

        var today = GameStatsStore.TodayIndex;
        var done = stats.DailyDone;
        var streak = stats.DailyStreak;
        var best = stats.DailyBestStreak;
        if (today == maskToday && done == maskDone && streak == maskStreak && best == maskBest)
        {
            return;
        }

        maskToday = today;
        maskDone = done;
        maskStreak = streak;
        maskBest = best;
        gridStart = StreakGrid.Start(today, firstDay);
        doneMask = StreakGrid.Mask(stats, gridStart, today);
    }

    private void DrawHeader(ImDrawListPtr drawList, AppSkin ui, GameStatsStore stats, float left, float right,
        float top)
    {
        var best = stats.DailyBestStreak;
        var bestWidth = 0f;
        if (best > 0)
        {
            var label = bestText.IsCurrent(best)
                ? bestText.Value
                : bestText.Store(best, Loc.T(L.GamesHub.BestValue, GameNumber.Label(best)));
            var size = Typography.Measure(label, TextStyles.Footnote);
            bestWidth = size.X + Metrics.Space.Md * UiScale.Current;
            var headlineHeight = Typography.LineHeight(TextStyles.Headline);
            var footnoteHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(right - size.X, top + (headlineHeight - footnoteHeight) * 0.5f),
                label, ui.MutedInk, TextStyles.Footnote);
        }

        var title = Typography.FitText(Loc.T(L.Games.Daily), MathF.Max(1f, right - left - bestWidth),
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top), title, ui.TitleInk, TextStyles.Headline);
    }

    private void DrawWeekdays(ImDrawListPtr drawList, AppSkin ui, float left, float top, float pitch)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        for (var column = 0; column < StreakGrid.Days; column++)
        {
            var center = new Vector2(left + pitch * (column + 0.5f), top + lineHeight * 0.5f);
            Typography.DrawCentered(drawList, center, weekdayInitials[column], ui.MutedInk, TextStyles.Footnote);
        }
    }

    private void DrawGrid(ImDrawListPtr drawList, AppSkin ui, float left, float top, float pitch, float scale)
    {
        var radius = MathF.Min(Dot * 0.5f * scale, pitch * 0.5f);
        var runFill = ImGui.GetColorU32(HubMetrics.Ember with { W = RunAlpha });
        var doneFill = ImGui.GetColorU32(HubMetrics.Ember);
        var missed = ImGui.GetColorU32(ui.Hairline);
        var future = ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Quaternary));
        var todayRing = ImGui.GetColorU32(ui.TitleInk);
        var todayCell = StreakGrid.TodayCell(maskToday, firstDay);
        for (var cell = 0; cell < StreakGrid.Cells; cell++)
        {
            if (!StreakGrid.StartsRun(doneMask, cell))
            {
                continue;
            }

            var end = StreakGrid.RunEnd(doneMask, cell);
            if (end == cell)
            {
                continue;
            }

            var first = CellCenter(left, top, pitch, cell, scale);
            var last = CellCenter(left, top, pitch, end, scale);
            Squircle.Fill(drawList, new Vector2(first.X - radius, first.Y - radius),
                new Vector2(last.X + radius, last.Y + radius), radius, runFill);
        }

        for (var cell = 0; cell < StreakGrid.Cells; cell++)
        {
            var center = CellCenter(left, top, pitch, cell, scale);
            if (cell > todayCell)
            {
                drawList.AddCircleFilled(center, FutureDot * 0.5f * scale, future, Segments);
                continue;
            }

            if (StreakGrid.Done(doneMask, cell))
            {
                drawList.AddCircleFilled(center, radius, doneFill, Segments);
            }
            else if (cell != todayCell)
            {
                drawList.AddCircle(center, radius - Metrics.Stroke.Hairline * 0.5f, missed, Segments,
                    Metrics.Stroke.Hairline);
            }

            if (cell == todayCell)
            {
                var ring = Metrics.Stroke.Ring * scale;
                drawList.AddCircle(center, radius - ring * 0.5f, todayRing, Segments, ring);
            }
        }
    }

    private static Vector2 CellCenter(float left, float top, float pitch, int cell, float scale) =>
        new(left + pitch * (cell % StreakGrid.Days + 0.5f), top + RowPitch * scale * (cell / StreakGrid.Days + 0.5f));

    private bool DrawFooter(ImDrawListPtr drawList, AppSkin ui, GameStatsStore stats, IMiniGame daily, float left,
        float right, float top, float scale)
    {
        var height = FooterHeight(scale);
        var icon = FooterIcon * scale;
        var iconMin = new Vector2(left, top + (height - icon) * 0.5f);
        GameIconArt.Draw(drawList, daily.Id, daily.Accent, iconMin, iconMin + new Vector2(icon, icon), null, true);
        var done = stats.DailyDone;
        var playLabel = Loc.T(L.Games.Play);
        var trailing = done ? CheckSize * scale : Button.WidthFor(playLabel, ButtonSize.Small);
        var textLeft = iconMin.X + icon + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, right - trailing - Metrics.Space.Md * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var textTop = top + (height - titleHeight - Typography.LineHeight(TextStyles.Footnote)) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(TodayLabel(daily.Title), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(countdown.Label(), textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var centerY = top + height * 0.5f;
        if (done)
        {
            PhoneIcon.Draw(drawList, new Vector2(right - trailing * 0.5f, centerY), PhoneIcons.CircleCheckFilled,
                HubMetrics.Ember, CheckSize * scale);
            return false;
        }

        var buttonHeight = Button.SmallHeight * scale;
        var button = new Rect(new Vector2(right - trailing, centerY - buttonHeight * 0.5f),
            new Vector2(right, centerY + buttonHeight * 0.5f));
        return Button.Draw(drawList, button, playLabel, ui.Ink.WithAccent(daily.Accent), id: PlayId);
    }

    private string TodayLabel(string title)
    {
        if (ReferenceEquals(todaySource, title) && ReferenceEquals(todayLanguage, Loc.Current))
        {
            return todayLabel;
        }

        todaySource = title;
        todayLanguage = Loc.Current;
        todayLabel = Loc.T(L.GamesHub.TodayGame, title);
        return todayLabel;
    }
}
