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

namespace Aetherphone.Apps.Fishing;

internal sealed class FishingApp : IPhoneApp
{
    private const int VoyageCount = 12;
    private const float RefreshIntervalSeconds = 5f;
    private const float UpcomingRowHeight = 64f;
    private const double VoyagePeriodSeconds = 7200;
    private static readonly Vector4 OceanTint = AppAccents.For("fishing");
    public string Id => "fishing";
    public string DisplayName => Loc.T(L.Apps.Fishing);
    public string Glyph => "F";
    public int BadgeCount => 0;
    private readonly OceanVoyageSlot[] voyages = new OceanVoyageSlot[VoyageCount];
    private float sinceRefresh;
    public void OnOpened() => Refresh();

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        GameSchedule.UpcomingOceanVoyages(DateTime.UtcNow, voyages);
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
            DrawUpcoming(theme, utcNow, scale);
            DrawNote(theme, scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawHero(PhoneTheme theme, DateTime utcNow, float scale)
    {
        var current = voyages[0];
        var plan = OceanRoutes.Resolve(current.Destination, current.Time);
        var remaining = current.BoardingUtc - utcNow;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var centerX = origin.X + width * 0.5f;
        var ringCenter = new Vector2(centerX, origin.Y + 84f * scale);
        var radius = 56f * scale;
        var thickness = 7f * scale;
        var fraction = current.BoardingNow
            ? 1f
            : 1f - (float)Math.Clamp(remaining.TotalSeconds / VoyagePeriodSeconds, 0f, 1f);
        var tint = current.BoardingNow ? theme.Accent : OceanTint;
        ProgressRing.Glow(ringCenter, radius, tint, 0.45f + 0.30f * Styling.Pulse(Styling.PulseBreath));
        ProgressRing.Track(ringCenter, radius, thickness, Palette.WithAlpha(theme.TextStrong, 0.10f));
        ProgressRing.Fill(ringCenter, radius, thickness, fraction, tint);
        ProgressRing.CenterIcon(ringCenter, FontAwesomeIcon.Fish, tint, radius * 0.52f);
        var status = current.BoardingNow ? Loc.T(L.Fishing.NowBoarding) : Loc.T(L.Fishing.NextVoyage);
        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 24f * scale), status.ToUpperInvariant(),
            tint, TextStyles.FootnoteEmphasized);
        var heading = $"{plan.RouteName} · {TimeOfDayLabel(plan.TimeOfDay)}";
        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 46f * scale), heading, theme.TextStrong,
            TextStyles.Title3);
        var when = current.BoardingNow
            ? Loc.T(L.Time.Now)
            : $"{LocalTime(current.BoardingUtc)} · {Relative(remaining)}";
        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 70f * scale), when, theme.TextMuted,
            TextStyles.Subheadline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 218f * scale));
        DrawBlueFish(theme, plan, scale);
    }

    private static void DrawBlueFish(PhoneTheme theme, in OceanRoutePlan plan, float scale)
    {
        SettingsSection.Header(Loc.T(L.Fishing.BlueFish), theme);
        if (plan.BlueFish.Length == 0)
        {
            var card = GroupCard.Begin(theme, 1);
            var row = card.NextRow();
            var labelSize = Typography.Measure(Loc.T(L.Fishing.NoBlueFish), TextStyles.Subheadline);
            Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), Loc.T(L.Fishing.NoBlueFish),
                theme.TextMuted, TextStyles.Subheadline);
            card.End();
            return;
        }

        var fishCard = GroupCard.Begin(theme, plan.BlueFish.Length, 52f);
        for (var index = 0; index < plan.BlueFish.Length; index++)
        {
            DrawBlueFishRow(fishCard.NextRow(), theme, plan.BlueFish[index], scale);
        }

        fishCard.End();
    }

    private static void DrawBlueFishRow(Rect row, PhoneTheme theme, in OceanBlueFish fish, float scale)
    {
        var tile = 30f * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, Styling.AccentBlue, FontAwesomeIcon.Fish);
        var textLeft = row.Min.X + tile + 12f * scale;
        Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), fish.Name, theme.TextStrong,
            TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), fish.Bait, theme.TextMuted,
            TextStyles.Footnote);
    }

    private void DrawUpcoming(PhoneTheme theme, DateTime utcNow, float scale)
    {
        SettingsSection.Header(Loc.T(L.Fishing.Upcoming), theme);
        var card = GroupCard.Begin(theme, VoyageCount - 1, UpcomingRowHeight);
        for (var index = 1; index < VoyageCount; index++)
        {
            DrawVoyageRow(card.NextRow(), theme, voyages[index], utcNow, scale);
        }

        card.End();
    }

    private static void DrawVoyageRow(Rect row, PhoneTheme theme, in OceanVoyageSlot voyage, DateTime utcNow,
        float scale)
    {
        var plan = OceanRoutes.Resolve(voyage.Destination, voyage.Time);
        var tile = 34f * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, TimeOfDayTint(plan.TimeOfDay), TimeOfDayIcon(plan.TimeOfDay));
        var textLeft = row.Min.X + tile + 12f * scale;
        var heading = $"{plan.RouteName} · {TimeOfDayLabel(plan.TimeOfDay)}";
        Typography.Draw(new Vector2(textLeft, row.Center.Y - 19f * scale), heading, theme.TextStrong,
            TextStyles.Headline);
        var detail = plan.BlueFish.Length == 0 ? plan.Stops : BlueFishNames(plan);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 2f * scale), detail, theme.TextMuted, TextStyles.Footnote);
        var time = LocalTime(voyage.BoardingUtc);
        var timeSize = Typography.Measure(time, TextStyles.SubheadlineEmphasized);
        Typography.Draw(new Vector2(row.Max.X - timeSize.X, row.Center.Y - 19f * scale), time, theme.TextStrong,
            TextStyles.SubheadlineEmphasized);
        var relative = Relative(voyage.BoardingUtc - utcNow);
        var relativeSize = Typography.Measure(relative, TextStyles.Caption1);
        Typography.Draw(new Vector2(row.Max.X - relativeSize.X, row.Center.Y + 4f * scale), relative, theme.TextMuted,
            TextStyles.Caption1);
    }

    private static void DrawNote(PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(0.78f))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Fishing.DeparturesNote));
        }
    }

    private static void IconTile(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var drawList = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half), size * 0.30f,
            ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.50f);
    }

    private static string BlueFishNames(in OceanRoutePlan plan)
    {
        if (plan.BlueFish.Length == 1)
        {
            return plan.BlueFish[0].Name;
        }

        return string.Concat(plan.BlueFish[0].Name, " · ", plan.BlueFish[1].Name);
    }

    private static Vector4 TimeOfDayTint(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => Styling.AccentAmber,
            OceanTimeOfDay.Night => Styling.AccentViolet,
            _ => Styling.AccentBlue,
        };

    private static FontAwesomeIcon TimeOfDayIcon(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => FontAwesomeIcon.CloudSun,
            OceanTimeOfDay.Night => FontAwesomeIcon.Moon,
            _ => FontAwesomeIcon.Sun,
        };

    private static string TimeOfDayLabel(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => Loc.T(L.Fishing.Sunset),
            OceanTimeOfDay.Night => Loc.T(L.Fishing.Night),
            _ => Loc.T(L.Fishing.Day),
        };

    private static string LocalTime(DateTime utc) => utc.ToLocalTime().ToString("ddd t", Loc.Culture);

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
        return Loc.T(L.Fishing.InDays, days, hours);
    }

    public void Dispose()
    {
    }
}
