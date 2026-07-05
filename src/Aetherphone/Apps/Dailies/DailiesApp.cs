using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Dailies;

internal sealed class DailiesApp : IPhoneApp
{
    private const float RowHeight = 60f;
    private const float TileSize = 30f;
    private const float RefreshIntervalSeconds = 2f;
    private static readonly Vector4 DailiesTint = new(0.36f, 0.78f, 0.62f, 1f);
    public string Id => "dailies";
    public string DisplayName => Loc.T(L.Apps.Dailies);
    public string Glyph => "D";
    public Vector4 Accent => DailiesTint;
    public int BadgeCount => outstandingCount;
    private readonly Configuration configuration;
    private readonly DailyCheckStore checkStore;
    private readonly DailyAutoStatus[] autoStatuses;
    private int outstandingCount;
    private float sinceRefresh;

    public DailiesApp(Configuration configuration)
    {
        this.configuration = configuration;
        checkStore = new DailyCheckStore(configuration);
        autoStatuses = new DailyAutoStatus[DailyCatalog.Items.Length];
        RefreshAuto();
    }

    public void OnOpened() => RefreshAuto();

    public void OnClosed()
    {
    }

    private void RefreshAuto()
    {
        var items = DailyCatalog.Items;
        var utcNow = DateTime.UtcNow;
        var outstanding = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var status = item.Tracking == DailyTracking.Manual
                ? DailyAutoStatus.Unavailable
                : DailiesReader.Read(item.Tracking, item.Goal);
            autoStatuses[index] = status;
            if (IsOutstanding(item, status, utcNow))
            {
                outstanding++;
            }
        }

        outstandingCount = outstanding;
        sinceRefresh = 0f;
    }

    private bool IsOutstanding(in DailyItem item, in DailyAutoStatus status, DateTime utcNow)
    {
        if (item.Tracking == DailyTracking.Levequests)
        {
            return false;
        }

        if (item.Tracking != DailyTracking.Manual && status.Available)
        {
            return !status.Complete;
        }

        return !checkStore.IsChecked(item, utcNow);
    }

    public void Draw(in PhoneContext context)
    {
        AppHeader.Draw(context, DisplayName);
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            RefreshAuto();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        var utcNow = DateTime.UtcNow;
        using (AppSurface.Begin(body))
        {
            DrawHero(theme, utcNow, scale);
            DrawCadence(theme, utcNow, DailyCadence.Daily, L.Dailies.DailyTasks, GameSchedule.NextDailyReset(utcNow));
            DrawCadence(theme, utcNow, DailyCadence.Weekly, L.Dailies.WeeklyTasks,
                GameSchedule.NextWeeklyReset(utcNow));
            DrawNotify(theme);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawHero(PhoneTheme theme, DateTime utcNow, float scale)
    {
        var items = DailyCatalog.Items;
        var tracked = 0;
        var done = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.Tracking == DailyTracking.Levequests)
            {
                continue;
            }

            tracked++;
            if (!IsOutstanding(item, autoStatuses[index], utcNow))
            {
                done++;
            }
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var centerX = origin.X + width * 0.5f;
        var ringCenter = new Vector2(centerX, origin.Y + 86f * scale);
        var radius = 56f * scale;
        var thickness = 7f * scale;
        var fraction = tracked == 0 ? 1f : done / (float)tracked;
        fraction = Math.Clamp(fraction, 0f, 1f);
        ProgressRing.Glow(ringCenter, radius, DailiesTint, 0.45f + 0.30f * Styling.Pulse(Styling.PulseBreath));
        ProgressRing.Track(ringCenter, radius, thickness, Styling.WithAlpha(theme.TextStrong, 0.10f));
        ProgressRing.Fill(ringCenter, radius, thickness, fraction, DailiesTint);
        var remaining = tracked - done;
        if (remaining <= 0)
        {
            ProgressRing.CenterIcon(ringCenter, FontAwesomeIcon.Star, DailiesTint, radius * 0.62f);
        }
        else
        {
            ProgressRing.CenterValue(ringCenter, remaining.ToString(Loc.Culture), $"/ {tracked}", theme.TextStrong,
                theme.TextMuted, TextStyles.LargeTitle);
        }

        var title = remaining <= 0 ? Loc.T(L.Dailies.AllDone) : Loc.T(L.Dailies.Remaining, remaining);
        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 26f * scale), title, theme.TextStrong,
            TextStyles.Title3);
        var sub = remaining <= 0 ? Loc.T(L.Dailies.NothingLeft) : $"{done} / {tracked}";
        Typography.DrawCentered(new Vector2(centerX, ringCenter.Y + radius + 50f * scale), sub, theme.TextMuted,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 196f * scale));
    }

    private void DrawCadence(PhoneTheme theme, DateTime utcNow, DailyCadence cadence, LocString sectionTitle,
        DateTime nextReset)
    {
        var items = DailyCatalog.Items;
        var rowCount = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Cadence == cadence)
            {
                rowCount++;
            }
        }

        if (rowCount == 0)
        {
            return;
        }

        var countdown = Relative(nextReset - utcNow);
        SectionHeaderWithCountdown(Loc.T(sectionTitle), Loc.T(L.Dailies.Resets, countdown), theme);
        var card = GroupCard.Begin(theme, rowCount, RowHeight);
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Cadence != cadence)
            {
                continue;
            }

            DrawItemRow(card.NextRow(), theme, items[index], autoStatuses[index], utcNow);
        }

        card.End();
    }

    private void DrawItemRow(Rect row, PhoneTheme theme, in DailyItem item, in DailyAutoStatus status, DateTime utcNow)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, item.Accent, item.Icon);
        var auto = item.Tracking != DailyTracking.Manual && status.Available;
        var complete = auto ? status.Complete : checkStore.IsChecked(item, utcNow);
        var info = item.Tracking == DailyTracking.Levequests;
        var textLeft = row.Min.X + tile + 12f * scale;
        var sublabel = BuildSublabel(item, status, auto, info);
        if (sublabel.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), Loc.T(item.Label), theme.TextStrong,
                TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), sublabel, theme.TextMuted,
                TextStyles.Footnote);
        }
        else
        {
            var nameSize = Typography.Measure(Loc.T(item.Label), TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), Loc.T(item.Label),
                theme.TextStrong, TextStyles.Headline);
        }

        if (info)
        {
            DrawCountValue(row, theme, status);
            return;
        }

        var markCenter = new Vector2(row.Max.X - 13f * scale, row.Center.Y);
        DrawCheckmark(markCenter, 13f * scale, complete, auto, theme, item.Accent);
        if (auto)
        {
            return;
        }

        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            checkStore.SetChecked(item, !complete, utcNow);
            RefreshAuto();
        }
    }

    private static string BuildSublabel(in DailyItem item, in DailyAutoStatus status, bool auto, bool info)
    {
        if (info)
        {
            return Loc.T(L.Dailies.AutoTracked);
        }

        if (!auto)
        {
            return string.Empty;
        }

        if (status.Complete)
        {
            return Loc.T(L.Dailies.AutoTracked);
        }

        return string.Concat(Loc.T(L.Dailies.AutoTracked), " · ", Loc.T(L.Dailies.Remaining, status.Remaining));
    }

    private static void DrawCountValue(Rect row, PhoneTheme theme, in DailyAutoStatus status)
    {
        var value = status.Available ? $"{status.Remaining} / {status.Goal}" : "--";
        var valueSize = Typography.Measure(value, TextStyles.Headline);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
            theme.TextStrong, TextStyles.Headline);
    }

    private static void DrawCheckmark(Vector2 center, float radius, bool complete, bool auto, PhoneTheme theme,
        Vector4 accent)
    {
        var dl = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        if (complete)
        {
            dl.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), 32);
            var tip = new Vector2(center.X - 1.5f * scale, center.Y + 4f * scale);
            var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
            dl.AddLine(tip - new Vector2(4f * scale, 4f * scale), tip, color, 2f * scale);
            dl.AddLine(tip, new Vector2(tip.X + 7f * scale, tip.Y - 8f * scale), color, 2f * scale);
            return;
        }

        var ringColor = auto ? Styling.WithAlpha(theme.TextMuted, 0.55f) : theme.TextMuted;
        dl.AddCircle(center, radius, ImGui.GetColorU32(ringColor), 32, 2f * scale);
    }

    private void DrawNotify(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var notify = SettingsRow.Bool(card.NextRow(), Loc.T(L.Dailies.NotifyReset), configuration.NotifyDailiesReset,
            theme);
        card.End();
        if (notify == configuration.NotifyDailiesReset)
        {
            return;
        }

        configuration.NotifyDailiesReset = notify;
        configuration.Save();
    }

    private static void IconTile(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var dl = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(dl, center - new Vector2(half, half), center + new Vector2(half, half), size * 0.30f,
            ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.50f);
    }

    private static void SectionHeaderWithCountdown(string title, string countdown, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var top = ImGui.GetCursorScreenPos();
        var right = top.X + ImGui.GetContentRegionAvail().X - 16f * scale;
        SettingsSection.Header(title, theme);
        var countdownSize = Typography.Measure(countdown, TextStyles.Caption1);
        var titleSize = Typography.Measure(title, TextStyles.Footnote);
        var titleTop = top.Y + 10f * scale;
        Typography.Draw(new Vector2(right - countdownSize.X, titleTop + (titleSize.Y - countdownSize.Y) * 0.5f),
            countdown, theme.TextMuted, TextStyles.Caption1);
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
}
