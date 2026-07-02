using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Timers;

internal sealed class TimersApp : IPhoneApp
{
    private const float RowHeight = 60f;
    private const float BadgeRadius = 15f;
    private const float CardRounding = 16f;
    private const float RefreshIntervalSeconds = 2f;

    private const double DailyPeriodSeconds = 86400;
    private const double WeeklyPeriodSeconds = 604800;
    private const double OceanPeriodSeconds = 7200;

    private static readonly Vector4 TimerOrange = new(1.00f, 0.62f, 0.04f, 1f);
    private static readonly Vector4 StageBackground = new(0.020f, 0.020f, 0.035f, 1f);
    private static readonly Vector4 StageCard = new(0.100f, 0.100f, 0.115f, 1f);
    private static readonly Vector4 StageInk = new(0.97f, 0.97f, 0.98f, 1f);
    private static readonly Vector4 StageMuted = new(0.56f, 0.56f, 0.60f, 1f);
    private static readonly Vector4 StageSeparator = new(1f, 1f, 1f, 0.07f);

    public string Id => "timers";

    public string DisplayName => Loc.T(L.Apps.Timers);

    public string Glyph => "T";

    public Vector4 Accent => TimerOrange;

    public int BadgeCount => 0;

    private readonly Configuration configuration;
    private readonly List<RetainerVenture> retainers = new();

    private bool retainersAvailable;
    private float sinceRefresh;

    public TimersApp(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void OnOpened() => Refresh();

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        retainersAvailable = RetainerReader.TryRead(retainers);
        sinceRefresh = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            Refresh();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        DeviceChrome.FillScreen(screen, context.Theme, StageBackground);

        SceneChrome.BackChevron(content, context.Navigation, StageInk, scale);
        Typography.DrawCentered(new Vector2(content.Center.X, content.Min.Y + 20f * scale), DisplayName, StageInk, TextStyles.Headline);

        var utcNow = DateTime.UtcNow;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + 40f * scale), content.Max);
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 4f * scale)))
        using (var child = ImRaii.Child("##timers", body.Size, false, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar))
        {
            if (!child)
            {
                return;
            }

            DrawHero(utcNow, scale);
            DrawResets(utcNow, scale);
            DrawActivities(utcNow, scale);
            DrawRetainers(utcNow, scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawHero(DateTime utcNow, float scale)
    {
        var daily = GameSchedule.NextDailyReset(utcNow);
        var remaining = daily - utcNow;

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var centerX = origin.X + width * 0.5f;
        var ringCenter = new Vector2(centerX, origin.Y + 86f * scale);
        var radius = 58f * scale;
        var thickness = 5f * scale;

        var fraction = Math.Clamp(1f - (float)(remaining.TotalSeconds / DailyPeriodSeconds), 0f, 1f);

        ProgressRing.Glow(ringCenter, radius, TimerOrange, 0.35f + 0.25f * Styling.Pulse(Styling.PulseBreath));
        ProgressRing.Track(ringCenter, radius, thickness, Styling.WithAlpha(StageInk, 0.08f));
        ProgressRing.Fill(ringCenter, radius, thickness, fraction, TimerOrange);

        var big = remaining <= TimeSpan.Zero ? Loc.T(L.Time.Now) : HeroClock(remaining);
        ProgressRing.CenterValue(ringCenter, big, null, StageInk, StageMuted, TextStyles.LargeTitle);

        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 26f * scale), Loc.T(L.Timers.DailyReset), StageInk, TextStyles.Title3);

        var relative = remaining <= TimeSpan.Zero ? Loc.T(L.Time.Now) : TimeFormat.Relative(remaining);
        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 50f * scale), $"{relative} · {LocalTime(daily)}", StageMuted, TextStyles.Footnote);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 196f * scale));
    }

    private void DrawResets(DateTime utcNow, float scale)
    {
        SectionLabel(Loc.T(L.Timers.ServerResets), scale);
        var card = BeginCard(3, scale);

        var daily = GameSchedule.NextDailyReset(utcNow);
        var dailyFraction = PeriodFraction(daily - utcNow, DailyPeriodSeconds);
        ApplyDaily(DrawTimerRow(CardRow(card, 0, scale), Styling.AccentAmber, FontAwesomeIcon.Sun, dailyFraction, Loc.T(L.Timers.DailyReset), LocalTime(daily), TimeFormat.Relative(daily - utcNow), StageInk, true, configuration.NotifyDailyReset));

        var grandCompany = GameSchedule.NextGrandCompanyReset(utcNow);
        var grandCompanyFraction = PeriodFraction(grandCompany - utcNow, DailyPeriodSeconds);
        ApplyGrandCompany(DrawTimerRow(CardRow(card, 1, scale), Styling.AccentRose, FontAwesomeIcon.ShieldAlt, grandCompanyFraction, Loc.T(L.Timers.GrandCompanyReset), LocalTime(grandCompany), TimeFormat.Relative(grandCompany - utcNow), StageInk, true, configuration.NotifyGrandCompanyReset));

        var weekly = GameSchedule.NextWeeklyReset(utcNow);
        var weeklyFraction = PeriodFraction(weekly - utcNow, WeeklyPeriodSeconds);
        ApplyWeekly(DrawTimerRow(CardRow(card, 2, scale), Styling.AccentBlue, FontAwesomeIcon.CalendarAlt, weeklyFraction, Loc.T(L.Timers.WeeklyReset), LocalTime(weekly), TimeFormat.Relative(weekly - utcNow), StageInk, true, configuration.NotifyWeeklyReset));

        EndCard(card, scale);
    }

    private void DrawActivities(DateTime utcNow, float scale)
    {
        SectionLabel(Loc.T(L.Timers.Activities), scale);
        var card = BeginCard(3, scale);

        var fashion = GameSchedule.FashionReport(utcNow);
        var fashionState = fashion.Active ? Loc.T(L.Timers.Open) : Loc.T(L.Timers.Closed);
        DrawTimerRow(CardRow(card, 0, scale), Styling.AccentPink, FontAwesomeIcon.Tshirt, -1f, Loc.T(L.Timers.FashionReport), fashionState, TimeFormat.Relative(fashion.NextChangeUtc - utcNow), StageInk, false, false);

        var cactpot = GameSchedule.NextJumboCactpot(utcNow);
        var cactpotFraction = PeriodFraction(cactpot - utcNow, WeeklyPeriodSeconds);
        DrawTimerRow(CardRow(card, 1, scale), Styling.AccentAmberSoft, FontAwesomeIcon.Dice, cactpotFraction, Loc.T(L.Timers.JumboCactpot), LocalDay(cactpot), TimeFormat.Relative(cactpot - utcNow), StageInk, false, false);

        var ocean = GameSchedule.OceanFishing(utcNow);
        var route = ocean.Route.Length == 0 ? string.Empty : $"{ocean.Route} · {TimeOfDayLabel(ocean.TimeOfDay)}";
        var oceanValue = ocean.BoardingNow ? Loc.T(L.Timers.BoardingNow) : TimeFormat.Relative(ocean.NextBoardingUtc - utcNow);
        var oceanColor = ocean.BoardingNow ? TimerOrange : StageInk;
        var oceanFraction = ocean.BoardingNow ? 1f : PeriodFraction(ocean.NextBoardingUtc - utcNow, OceanPeriodSeconds);
        DrawTimerRow(CardRow(card, 2, scale), Styling.AccentMint, FontAwesomeIcon.Fish, oceanFraction, Loc.T(L.Timers.OceanFishing), route, oceanValue, oceanColor, false, false);

        EndCard(card, scale);
    }

    private void DrawRetainers(DateTime utcNow, float scale)
    {
        SectionLabel(Loc.T(L.Timers.Retainers), scale);

        if (!retainersAvailable || retainers.Count == 0)
        {
            DrawHint(Loc.T(L.Timers.OpenBellOnce), scale);
            return;
        }

        var card = BeginCard(retainers.Count, scale);
        for (var index = 0; index < retainers.Count; index++)
        {
            DrawRetainerRow(CardRow(card, index, scale), retainers[index], utcNow, scale);
        }

        EndCard(card, scale);

        var notifyCard = BeginCard(1, scale);
        var notify = DrawNotifyRow(CardRow(notifyCard, 0, scale), Loc.T(L.Timers.NotifyVentures), configuration.NotifyRetainerVentures, scale);
        EndCard(notifyCard, scale);

        if (notify != configuration.NotifyRetainerVentures)
        {
            configuration.NotifyRetainerVentures = notify;
            configuration.Save();
        }
    }

    private void DrawRetainerRow(Rect row, RetainerVenture venture, DateTime utcNow, float scale)
    {
        if (!venture.HasVenture)
        {
            DrawTimerRow(row, StageMuted, FontAwesomeIcon.Briefcase, -1f, venture.Name, string.Empty, Loc.T(L.Timers.NoVenture), StageMuted, false, false);
            return;
        }

        var remaining = venture.CompleteUtc - utcNow;
        if (remaining <= TimeSpan.Zero)
        {
            DrawTimerRow(row, PhoneTheme.Default.ToggleOn, FontAwesomeIcon.Briefcase, 1f, venture.Name, string.Empty, Loc.T(L.Timers.Ready), PhoneTheme.Default.ToggleOn, false, false);
            return;
        }

        DrawTimerRow(row, Styling.AccentMint, FontAwesomeIcon.Briefcase, -1f, venture.Name, LocalTime(venture.CompleteUtc), TimeFormat.Relative(remaining), StageInk, false, false);
    }

    private static bool DrawTimerRow(Rect row, Vector4 tint, FontAwesomeIcon icon, float fraction, string name, string sublabel, string value, Vector4 valueColor, bool hasToggle, bool toggleValue)
    {
        var scale = ImGuiHelpers.GlobalScale;

        var badgeCenter = new Vector2(row.Min.X + BadgeRadius * scale, row.Center.Y);
        RingBadge(badgeCenter, BadgeRadius * scale, tint, icon, fraction, scale);

        var textLeft = row.Min.X + BadgeRadius * 2f * scale + 12f * scale;
        if (sublabel.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), name, StageInk, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), sublabel, StageMuted, TextStyles.Footnote);
        }
        else
        {
            var nameSize = Typography.Measure(name, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, StageInk, TextStyles.Headline);
        }

        var rightEdge = row.Max.X;
        var result = toggleValue;
        if (hasToggle)
        {
            var width = 46f * scale;
            var height = 28f * scale;
            var min = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
            result = Toggle.Draw(new Rect(min, min + new Vector2(width, height)), toggleValue, PhoneTheme.Default);
            rightEdge = min.X - 14f * scale;
        }

        if (value.Length > 0)
        {
            var valueSize = Typography.Measure(value, 1.06f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(rightEdge - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, valueColor, 1.06f, FontWeight.SemiBold);
        }

        return result;
    }

    private static bool DrawNotifyRow(Rect row, string label, bool value, float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, StageInk, TextStyles.Body);

        var width = 46f * scale;
        var height = 28f * scale;
        var min = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        return Toggle.Draw(new Rect(min, min + new Vector2(width, height)), value, PhoneTheme.Default);
    }

    private static void RingBadge(Vector2 center, float radius, Vector4 tint, FontAwesomeIcon icon, float fraction, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Styling.WithAlpha(tint, 0.28f)), 32, 2.4f * scale);
        if (fraction >= 0f)
        {
            ProgressRing.Fill(center, radius, 2.4f * scale, Math.Clamp(fraction, 0f, 1f), tint);
        }

        ProgressRing.CenterIcon(center, icon, tint, radius * 0.95f);
    }

    private static Rect BeginCard(int rowCount, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var card = new Rect(origin, origin + new Vector2(width, rowCount * RowHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, card.Min, card.Max, CardRounding * scale, ImGui.GetColorU32(StageCard));
        Material.EdgeSquircle(drawList, card.Min, card.Max, CardRounding * scale, scale, 0.7f);
        return card;
    }

    private static Rect CardRow(Rect card, int index, float scale)
    {
        var top = card.Min.Y + index * RowHeight * scale;
        if (index > 0)
        {
            ImGui.GetWindowDrawList().AddLine(new Vector2(card.Min.X + 56f * scale, top), new Vector2(card.Max.X - 14f * scale, top), ImGui.GetColorU32(StageSeparator), 1f);
        }

        return new Rect(new Vector2(card.Min.X + 14f * scale, top), new Vector2(card.Max.X - 14f * scale, top + RowHeight * scale));
    }

    private static void EndCard(Rect card, float scale)
    {
        ImGui.SetCursorScreenPos(card.Min);
        ImGui.Dummy(new Vector2(card.Width, card.Height + 6f * scale));
    }

    private static void SectionLabel(string title, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(0.8f))
        using (ImRaii.PushColor(ImGuiCol.Text, StageMuted))
        {
            ImGui.TextUnformatted(title.ToUpperInvariant());
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }

    private static void DrawHint(string text, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
        using (Plugin.Fonts.Push(0.9f))
        using (ImRaii.PushColor(ImGuiCol.Text, StageMuted))
        {
            ImGui.TextWrapped(text);
        }
    }

    private void ApplyDaily(bool value)
    {
        if (value == configuration.NotifyDailyReset)
        {
            return;
        }

        configuration.NotifyDailyReset = value;
        configuration.Save();
    }

    private void ApplyGrandCompany(bool value)
    {
        if (value == configuration.NotifyGrandCompanyReset)
        {
            return;
        }

        configuration.NotifyGrandCompanyReset = value;
        configuration.Save();
    }

    private void ApplyWeekly(bool value)
    {
        if (value == configuration.NotifyWeeklyReset)
        {
            return;
        }

        configuration.NotifyWeeklyReset = value;
        configuration.Save();
    }

    private static float PeriodFraction(TimeSpan remaining, double periodSeconds)
    {
        if (periodSeconds <= 0 || remaining <= TimeSpan.Zero)
        {
            return 1f;
        }

        return Math.Clamp(1f - (float)(remaining.TotalSeconds / periodSeconds), 0f, 1f);
    }

    private static string LocalTime(DateTime utc) => utc.ToLocalTime().ToString("t", Loc.Culture);

    private static string LocalDay(DateTime utc) => utc.ToLocalTime().ToString("ddd t", Loc.Culture);

    private static string TimeOfDayLabel(OceanTimeOfDay timeOfDay) => timeOfDay switch
    {
        OceanTimeOfDay.Sunset => Loc.T(L.Timers.OceanSunset),
        OceanTimeOfDay.Night => Loc.T(L.Timers.OceanNight),
        _ => Loc.T(L.Timers.OceanDay),
    };

    private static string HeroClock(TimeSpan remaining)
    {
        var totalMinutes = (int)remaining.TotalMinutes;
        if (totalMinutes < 60)
        {
            return $"{Math.Max(1, totalMinutes)}m";
        }

        var totalHours = totalMinutes / 60;
        if (totalHours < 24)
        {
            return $"{totalHours}:{totalMinutes % 60:00}";
        }

        return $"{totalHours / 24}d";
    }

    public void Dispose()
    {
    }
}
