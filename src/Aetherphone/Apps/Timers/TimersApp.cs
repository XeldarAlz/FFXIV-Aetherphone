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
    private const float TileSize = 30f;
    private const float RefreshIntervalSeconds = 2f;

    private const double DailyPeriodSeconds = 86400;

    private static readonly Vector4 ResetTint = new(0.40f, 0.45f, 0.92f, 1f);

    public string Id => "timers";

    public string DisplayName => Loc.T(L.Apps.Timers);

    public string Glyph => "T";

    public Vector4 Accent => ResetTint;

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
        AppHeader.Draw(context, DisplayName);

        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            Refresh();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        var utcNow = DateTime.UtcNow;

        using (AppSurface.Begin(body))
        {
            DrawHero(theme, utcNow, scale);
            DrawResets(theme, utcNow);
            DrawActivities(theme, utcNow);
            DrawRetainers(theme, utcNow, scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawHero(PhoneTheme theme, DateTime utcNow, float scale)
    {
        var daily = GameSchedule.NextDailyReset(utcNow);
        var best = new HeroCandidate(Loc.T(L.Timers.DailyReset), Styling.AccentAmber, daily - utcNow, DailyPeriodSeconds, LocalTime(daily));

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var centerX = origin.X + width * 0.5f;
        var ringCenter = new Vector2(centerX, origin.Y + 86f * scale);
        var radius = 56f * scale;
        var thickness = 7f * scale;

        var fraction = best.PeriodSeconds <= 0 ? 0f : 1f - (float)(best.Remaining.TotalSeconds / best.PeriodSeconds);
        fraction = Math.Clamp(fraction, 0f, 1f);

        ProgressRing.Glow(ringCenter, radius, best.Tint, 0.45f + 0.30f * Styling.Pulse(Styling.PulseBreath));
        ProgressRing.Track(ringCenter, radius, thickness, Styling.WithAlpha(theme.TextStrong, 0.10f));
        ProgressRing.Fill(ringCenter, radius, thickness, fraction, best.Tint);

        var big = best.Remaining <= TimeSpan.Zero ? Loc.T(L.Time.Now) : HeroClock(best.Remaining);
        ProgressRing.CenterValue(ringCenter, big, null, theme.TextStrong, theme.TextMuted, 2.1f);

        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 26f * scale), best.Name, theme.TextStrong, TextStyles.Title3);

        var relative = best.Remaining <= TimeSpan.Zero ? Loc.T(L.Time.Now) : Relative(best.Remaining);
        var sub = best.Context.Length == 0 ? relative : $"{relative} · {best.Context}";
        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 50f * scale), sub, theme.TextMuted, TextStyles.Footnote);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 196f * scale));
    }

    private void DrawResets(PhoneTheme theme, DateTime utcNow)
    {
        SettingsSection.Header(Loc.T(L.Timers.ServerResets), theme);
        var card = GroupCard.Begin(theme, 3, RowHeight);

        var daily = GameSchedule.NextDailyReset(utcNow);
        ApplyDaily(DrawRow(card.NextRow(), theme, Styling.AccentAmber, FontAwesomeIcon.Sun, Loc.T(L.Timers.DailyReset), LocalTime(daily), Relative(daily - utcNow), theme.TextStrong, true, configuration.NotifyDailyReset));

        var grandCompany = GameSchedule.NextGrandCompanyReset(utcNow);
        ApplyGrandCompany(DrawRow(card.NextRow(), theme, Styling.AccentRose, FontAwesomeIcon.ShieldAlt, Loc.T(L.Timers.GrandCompanyReset), LocalTime(grandCompany), Relative(grandCompany - utcNow), theme.TextStrong, true, configuration.NotifyGrandCompanyReset));

        var weekly = GameSchedule.NextWeeklyReset(utcNow);
        ApplyWeekly(DrawRow(card.NextRow(), theme, Styling.AccentBlue, FontAwesomeIcon.CalendarAlt, Loc.T(L.Timers.WeeklyReset), LocalTime(weekly), Relative(weekly - utcNow), theme.TextStrong, true, configuration.NotifyWeeklyReset));

        card.End();
    }

    private void DrawActivities(PhoneTheme theme, DateTime utcNow)
    {
        SettingsSection.Header(Loc.T(L.Timers.Activities), theme);
        var card = GroupCard.Begin(theme, 3, RowHeight);

        var fashion = GameSchedule.FashionReport(utcNow);
        var fashionState = fashion.Active ? Loc.T(L.Timers.Open) : Loc.T(L.Timers.Closed);
        DrawRow(card.NextRow(), theme, Styling.AccentPink, FontAwesomeIcon.Tshirt, Loc.T(L.Timers.FashionReport), fashionState, Relative(fashion.NextChangeUtc - utcNow), theme.TextStrong, false, false);

        var cactpot = GameSchedule.NextJumboCactpot(utcNow);
        DrawRow(card.NextRow(), theme, Styling.AccentAmberSoft, FontAwesomeIcon.Dice, Loc.T(L.Timers.JumboCactpot), LocalDay(cactpot), Relative(cactpot - utcNow), theme.TextStrong, false, false);

        var ocean = GameSchedule.OceanFishing(utcNow);
        var route = ocean.Route.Length == 0 ? string.Empty : $"{ocean.Route} · {TimeOfDayLabel(ocean.TimeOfDay)}";
        var oceanValue = ocean.BoardingNow ? Loc.T(L.Timers.BoardingNow) : Relative(ocean.NextBoardingUtc - utcNow);
        var oceanColor = ocean.BoardingNow ? theme.Accent : theme.TextStrong;
        DrawRow(card.NextRow(), theme, Styling.AccentMint, FontAwesomeIcon.Fish, Loc.T(L.Timers.OceanFishing), route, oceanValue, oceanColor, false, false);

        card.End();
    }

    private void DrawRetainers(PhoneTheme theme, DateTime utcNow, float scale)
    {
        SettingsSection.Header(Loc.T(L.Timers.Retainers), theme);

        if (!retainersAvailable || retainers.Count == 0)
        {
            DrawHint(Loc.T(L.Timers.OpenBellOnce), theme, scale);
            return;
        }

        var card = GroupCard.Begin(theme, retainers.Count, RowHeight);
        for (var index = 0; index < retainers.Count; index++)
        {
            DrawRetainerRow(card.NextRow(), theme, retainers[index], utcNow);
        }

        card.End();

        var notifyCard = GroupCard.Begin(theme, 1);
        var notify = SettingsRow.Bool(notifyCard.NextRow(), Loc.T(L.Timers.NotifyVentures), configuration.NotifyRetainerVentures, theme);
        notifyCard.End();

        if (notify != configuration.NotifyRetainerVentures)
        {
            configuration.NotifyRetainerVentures = notify;
            configuration.Save();
        }
    }

    private static void DrawRetainerRow(Rect row, PhoneTheme theme, RetainerVenture venture, DateTime utcNow)
    {
        if (!venture.HasVenture)
        {
            DrawRow(row, theme, theme.TextMuted, FontAwesomeIcon.Briefcase, venture.Name, string.Empty, Loc.T(L.Timers.NoVenture), theme.TextMuted, false, false);
            return;
        }

        var remaining = venture.CompleteUtc - utcNow;
        if (remaining <= TimeSpan.Zero)
        {
            DrawRow(row, theme, theme.ToggleOn, FontAwesomeIcon.Briefcase, venture.Name, string.Empty, Loc.T(L.Timers.Ready), theme.ToggleOn, false, false);
            return;
        }

        DrawRow(row, theme, Styling.AccentMint, FontAwesomeIcon.Briefcase, venture.Name, LocalTime(venture.CompleteUtc), Relative(remaining), theme.TextStrong, false, false);
    }

    private static bool DrawRow(Rect row, PhoneTheme theme, Vector4 tint, FontAwesomeIcon icon, string name, string sublabel, string value, Vector4 valueColor, bool hasToggle, bool toggleValue)
    {
        var scale = ImGuiHelpers.GlobalScale;

        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, tint, icon);

        var textLeft = row.Min.X + tile + 12f * scale;
        if (sublabel.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), name, theme.TextStrong, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), sublabel, theme.TextMuted, TextStyles.Footnote);
        }
        else
        {
            var nameSize = Typography.Measure(name, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, theme.TextStrong, TextStyles.Headline);
        }

        var rightEdge = row.Max.X;
        var result = toggleValue;
        if (hasToggle)
        {
            var width = 46f * scale;
            var height = 28f * scale;
            var min = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
            result = Toggle.Draw(new Rect(min, min + new Vector2(width, height)), toggleValue, theme);
            rightEdge = min.X - 14f * scale;
        }

        if (value.Length > 0)
        {
            var valueSize = Typography.Measure(value, 1.06f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(rightEdge - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, valueColor, 1.06f, FontWeight.SemiBold);
        }

        return result;
    }

    private static void IconTile(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var dl = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(dl, center - new Vector2(half, half), center + new Vector2(half, half), size * 0.30f, ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.50f);
    }

    private static void DrawHint(string text, PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
        using (Plugin.Fonts.Push(0.9f))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
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

    private static string Relative(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return Loc.T(L.Time.Now);
        }

        var totalMinutes = (int)remaining.TotalMinutes;
        if (totalMinutes < 60)
        {
            return Loc.T(L.Time.InMinutes, Math.Max(1, totalMinutes));
        }

        var totalHours = totalMinutes / 60;
        if (totalHours < 24)
        {
            var minutes = totalMinutes % 60;
            return minutes == 0 ? Loc.T(L.Time.InHours, totalHours) : Loc.T(L.Time.InHoursMinutes, totalHours, minutes);
        }

        var days = totalHours / 24;
        var hours = totalHours % 24;
        return hours == 0 ? Loc.T(L.Timers.InDays, days) : Loc.T(L.Timers.InDaysHours, days, hours);
    }

    public void Dispose()
    {
    }

    private readonly record struct HeroCandidate(string Name, Vector4 Tint, TimeSpan Remaining, double PeriodSeconds, string Context);
}
