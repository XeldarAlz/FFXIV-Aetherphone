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

namespace Aetherphone.Apps.Timers;

internal sealed class TimersApp : IPhoneApp
{
    private const float RowHeight = 62f;
    private const float TileSize = 30f;
    private const float CardRounding = 18f;
    private const float CardGap = 10f;
    private const float RowPadding = 14f;
    private const float RefreshIntervalSeconds = 2f;
    private const double DailyPeriodSeconds = 86400;
    private const double WeeklyPeriodSeconds = 604800;
    private const double OceanPeriodSeconds = 7200;

    public string Id => "timers";

    public string DisplayName => Loc.T(L.Apps.Timers);

    public string Glyph => "T";

    public int BadgeCount => 0;

    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Timers);
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
        var utcNow = DateTime.UtcNow;

        ui.Theme = context.Theme;
        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);
        AppHeader.Draw(context, DisplayName);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawHero(utcNow, scale);
            DrawResets(utcNow, scale);
            DrawActivities(utcNow, scale);
            DrawRetainers(utcNow, scale);
            DrawReminders(scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawHero(DateTime utcNow, float scale)
    {
        var daily = GameSchedule.NextDailyReset(utcNow);
        var remaining = daily - utcNow;
        var fraction = Math.Clamp(1f - (float)(remaining.TotalSeconds / DailyPeriodSeconds), 0f, 1f);
        var big = remaining <= TimeSpan.Zero ? Loc.T(L.Time.Now) : HeroClock(remaining);
        var relative = remaining <= TimeSpan.Zero ? Loc.T(L.Time.Now) : TimeFormat.Relative(remaining);
        HeroRing.Draw(fraction, AppPalettes.Timers.Accent, AppPalettes.Timers.TitleInk, AppPalettes.Timers.MutedInk,
            big, null, Loc.T(L.Timers.DailyReset), $"{relative} · {LocalTime(daily)}");
    }

    private void DrawResets(DateTime utcNow, float scale)
    {
        ui.SectionLabel(Loc.T(L.Timers.ServerResets), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(3, scale);

        var daily = GameSchedule.NextDailyReset(utcNow);
        DrawTimerRow(CardRow(card, 0, scale), Styling.AccentAmber, FontAwesomeIcon.Sun, Loc.T(L.Timers.DailyReset),
            LocalTime(daily), TimeFormat.Relative(daily - utcNow), AppPalettes.Timers.TitleInk);

        var grandCompany = GameSchedule.NextGrandCompanyReset(utcNow);
        DrawTimerRow(CardRow(card, 1, scale), Styling.AccentRose, FontAwesomeIcon.ShieldAlt,
            Loc.T(L.Timers.GrandCompanyReset), LocalTime(grandCompany), TimeFormat.Relative(grandCompany - utcNow),
            AppPalettes.Timers.TitleInk);

        var weekly = GameSchedule.NextWeeklyReset(utcNow);
        DrawTimerRow(CardRow(card, 2, scale), Styling.AccentBlue, FontAwesomeIcon.CalendarAlt,
            Loc.T(L.Timers.WeeklyReset), LocalTime(weekly), TimeFormat.Relative(weekly - utcNow), AppPalettes.Timers.TitleInk);

        EndCard(card, scale);
    }

    private void DrawActivities(DateTime utcNow, float scale)
    {
        ui.SectionLabel(Loc.T(L.Timers.Activities), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(3, scale);

        var fashion = GameSchedule.FashionReport(utcNow);
        var fashionState = fashion.Active ? Loc.T(L.Timers.Open) : Loc.T(L.Timers.Closed);
        DrawTimerRow(CardRow(card, 0, scale), Styling.AccentPink, FontAwesomeIcon.Tshirt,
            Loc.T(L.Timers.FashionReport), fashionState, TimeFormat.Relative(fashion.NextChangeUtc - utcNow),
            AppPalettes.Timers.TitleInk);

        var cactpot = GameSchedule.NextJumboCactpot(utcNow);
        DrawTimerRow(CardRow(card, 1, scale), Styling.AccentAmberSoft, FontAwesomeIcon.Dice,
            Loc.T(L.Timers.JumboCactpot), LocalDay(cactpot), TimeFormat.Relative(cactpot - utcNow), AppPalettes.Timers.TitleInk);

        var ocean = GameSchedule.OceanFishing(utcNow);
        var route = ocean.Route.Length == 0 ? string.Empty : $"{ocean.Route} · {TimeOfDayLabel(ocean.TimeOfDay)}";
        var oceanValue = ocean.BoardingNow
            ? Loc.T(L.Timers.BoardingNow)
            : TimeFormat.Relative(ocean.NextBoardingUtc - utcNow);
        var oceanColor = ocean.BoardingNow ? AppPalettes.Timers.Accent : AppPalettes.Timers.TitleInk;
        DrawTimerRow(CardRow(card, 2, scale), Styling.AccentMint, FontAwesomeIcon.Fish, Loc.T(L.Timers.OceanFishing),
            route, oceanValue, oceanColor);

        EndCard(card, scale);
    }

    private void DrawRetainers(DateTime utcNow, float scale)
    {
        ui.SectionLabel(Loc.T(L.Timers.Retainers), TextStyles.FootnoteEmphasized, 6f);
        if (!retainersAvailable || retainers.Count == 0)
        {
            DrawHint(Loc.T(L.Timers.OpenBellOnce), scale);
            return;
        }

        var card = BeginCard(retainers.Count, scale);
        for (var index = 0; index < retainers.Count; index++)
        {
            DrawRetainerRow(CardRow(card, index, scale), retainers[index], utcNow);
        }

        EndCard(card, scale);
    }

    private void DrawReminders(float scale)
    {
        ui.SectionLabel(Loc.T(L.Timers.Reminders), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(4, scale);

        ApplyDaily(DrawNotifyRow(CardRow(card, 0, scale), Loc.T(L.Timers.DailyReset), configuration.NotifyDailyReset,
            scale));
        ApplyGrandCompany(DrawNotifyRow(CardRow(card, 1, scale), Loc.T(L.Timers.GrandCompanyReset),
            configuration.NotifyGrandCompanyReset, scale));
        ApplyWeekly(DrawNotifyRow(CardRow(card, 2, scale), Loc.T(L.Timers.WeeklyReset), configuration.NotifyWeeklyReset,
            scale));
        ApplyVentures(DrawNotifyRow(CardRow(card, 3, scale), Loc.T(L.Timers.NotifyVentures),
            configuration.NotifyRetainerVentures, scale));

        EndCard(card, scale);
    }

    private static void DrawRetainerRow(Rect row, RetainerVenture venture, DateTime utcNow)
    {
        if (!venture.HasVenture)
        {
            DrawTimerRow(row, AppPalettes.Timers.MutedInk, FontAwesomeIcon.Briefcase, venture.Name, string.Empty,
                Loc.T(L.Timers.NoVenture), AppPalettes.Timers.MutedInk);
            return;
        }

        var remaining = venture.CompleteUtc - utcNow;
        if (remaining <= TimeSpan.Zero)
        {
            DrawTimerRow(row, PhoneTheme.Default.ToggleOn, FontAwesomeIcon.Briefcase, venture.Name, string.Empty,
                Loc.T(L.Timers.Ready), PhoneTheme.Default.ToggleOn);
            return;
        }

        DrawTimerRow(row, Styling.AccentMint, FontAwesomeIcon.Briefcase, venture.Name, LocalTime(venture.CompleteUtc),
            TimeFormat.Relative(remaining), AppPalettes.Timers.TitleInk);
    }

    private static void DrawTimerRow(Rect row, Vector4 tint, FontAwesomeIcon icon, string name, string sublabel,
        string value, Vector4 valueColor)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, tint, icon);

        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        var valueRight = row.Max.X;
        Typography.Draw(new Vector2(valueRight - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, valueColor,
            TextStyles.SubheadlineEmphasized);

        var textLeft = row.Min.X + tile + 12f * scale;
        var textRight = valueRight - valueSize.X - 12f * scale;
        ImGui.PushClipRect(new Vector2(textLeft, row.Min.Y), new Vector2(textRight, row.Max.Y), true);
        if (sublabel.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), name, AppPalettes.Timers.TitleInk,
                TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), sublabel, AppPalettes.Timers.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            var nameSize = Typography.Measure(name, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, AppPalettes.Timers.TitleInk,
                TextStyles.Headline);
        }

        ImGui.PopClipRect();
    }

    private static bool DrawNotifyRow(Rect row, string label, bool value, float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, AppPalettes.Timers.BodyInk,
            TextStyles.Body);
        var width = 46f * scale;
        var height = 28f * scale;
        var min = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        return Toggle.Draw(new Rect(min, min + new Vector2(width, height)), value, PhoneTheme.Default);
    }

    private Rect BeginCard(int rowCount, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var card = new Rect(origin, origin + new Vector2(width, rowCount * RowHeight * scale));
        ui.Card(ImGui.GetWindowDrawList(), card.Min, card.Max, CardRounding * scale, elevated: true);
        return card;
    }

    private static Rect CardRow(Rect card, int index, float scale)
    {
        var top = card.Min.Y + index * RowHeight * scale;
        if (index > 0)
        {
            ImGui.GetWindowDrawList().AddLine(new Vector2(card.Min.X + RowPadding * 2f * scale + TileSize * scale, top),
                new Vector2(card.Max.X - RowPadding * scale, top), ImGui.GetColorU32(AppPalettes.Timers.CardStroke), 1f);
        }

        return new Rect(new Vector2(card.Min.X + RowPadding * scale, top),
            new Vector2(card.Max.X - RowPadding * scale, top + RowHeight * scale));
    }

    private static void EndCard(Rect card, float scale)
    {
        ImGui.SetCursorScreenPos(card.Min);
        ImGui.Dummy(new Vector2(card.Width, card.Height + CardGap * scale));
    }

    private static void DrawHint(string text, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(TextStyles.Footnote.Scale))
        using (Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Timers.MutedInk))
        {
            ImGui.TextWrapped(text);
        }

        ImGui.Dummy(new Vector2(0f, CardGap * scale));
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

    private void ApplyVentures(bool value)
    {
        if (value == configuration.NotifyRetainerVentures)
        {
            return;
        }

        configuration.NotifyRetainerVentures = value;
        configuration.Save();
    }

    private static string LocalTime(DateTime utc) => utc.ToLocalTime().ToString("t", Loc.Culture);

    private static string LocalDay(DateTime utc) => utc.ToLocalTime().ToString("ddd t", Loc.Culture);

    private static string TimeOfDayLabel(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
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

    private static void IconTile(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var drawList = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half), size * 0.30f,
            ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.50f);
    }

    public void Dispose()
    {
    }
}
