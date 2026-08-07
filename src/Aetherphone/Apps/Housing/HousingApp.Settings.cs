using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const float SettingsRowHeight = Metrics.Size.Row;

    private void DrawSettingsRoute(Rect area)
    {
        var scale = UiScale.Current;
        var body = DrawSubHeader(area, "housing.header.settings", Loc.T(L.Housing.Settings));
        using (AppSurface.Begin(body))
        {
            DrawWorldSettings(scale);
            DrawDataSettings(scale);
            DrawReminderSettings(scale);
            DrawMapSettings(scale);
            DrawDiagnostics(scale);
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawWorldSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Housing.SettingsWorld), frameTheme);
        var card = GroupCard.Begin(frameTheme, 2, SettingsRowHeight);
        var worldName = housing.WorldName;
        if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Housing.PreferredWorld),
                worldName.Length > 0 ? worldName : Loc.T(L.Housing.ChooseWorld), frameTheme))
        {
            worldSearch = string.Empty;
            Push(HousingRoute.WorldPicker);
        }

        var follow = SettingsRow.Bool(card.NextRow(), Loc.T(L.Housing.FollowCurrentWorld),
            configuration.HousingFollowCurrentWorld, frameTheme);
        if (follow != configuration.HousingFollowCurrentWorld)
        {
            housing.SetFollowCurrentWorld(follow);
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Housing.FollowCurrentWorldHint), frameTheme);
    }

    private void DrawDataSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Housing.SettingsData), frameTheme);
        var card = GroupCard.Begin(frameTheme, 4, SettingsRowHeight);
        var autoRefresh = SettingsRow.Bool(card.NextRow(), Loc.T(L.Housing.AutoRefresh),
            configuration.HousingAutoRefresh, frameTheme);
        if (autoRefresh != configuration.HousingAutoRefresh)
        {
            configuration.HousingAutoRefresh = autoRefresh;
            configuration.Save();
        }

        var interval = DrawNumberRow(card.NextRow(), Loc.T(L.Housing.RefreshInterval), "##housingRefreshMinutes",
            housing.RefreshMinutes, HousingDefaults.MinRefreshMinutes, HousingDefaults.MaxRefreshMinutes,
            HousingDefaults.RefreshStepMinutes, scale);
        if (interval != housing.RefreshMinutes)
        {
            housing.SetRefreshMinutes(interval);
        }

        var live = DrawNumberRow(card.NextRow(), Loc.T(L.Housing.FreshnessThreshold), "##housingLiveMinutes",
            configuration.HousingLiveMinutes, 5, 45, 5, scale);
        if (live != configuration.HousingLiveMinutes)
        {
            SetFreshnessMinutes(live);
        }

        if (SettingsRow.Action(card.NextRow(), Loc.T(L.Housing.ClearCache), frameTheme.Danger, frameTheme))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Housing.SettingsData),
                Message = Loc.T(L.Housing.ClearCache),
                ConfirmLabel = Loc.T(L.Housing.ClearCache),
                CancelLabel = Loc.T(L.Common.Cancel),
                Confirm = () =>
                {
                    housing.ClearCache();
                    InvalidateCache();
                },
            });
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Housing.RefreshIntervalHint, HousingDefaults.MinRefreshMinutes), frameTheme);
    }

    private void DrawReminderSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Housing.SettingsNotifications), frameTheme);
        var card = GroupCard.Begin(frameTheme, 3, SettingsRowHeight);
        var notifyEntry = SettingsRow.Bool(card.NextRow(), Loc.T(L.Housing.NotifyEntry),
            configuration.HousingNotifyEntry, frameTheme);
        if (notifyEntry != configuration.HousingNotifyEntry)
        {
            configuration.HousingNotifyEntry = notifyEntry;
            configuration.Save();
        }

        var notifyResults = SettingsRow.Bool(card.NextRow(), Loc.T(L.Housing.NotifyResults),
            configuration.HousingNotifyResults, frameTheme);
        if (notifyResults != configuration.HousingNotifyResults)
        {
            configuration.HousingNotifyResults = notifyResults;
            configuration.Save();
        }

        var lead = DrawNumberRow(card.NextRow(), Loc.T(L.Housing.ReminderLead), "##housingLeadMinutes",
            configuration.HousingReminderMinutes, 1, 240, 5, scale);
        if (lead != configuration.HousingReminderMinutes)
        {
            configuration.HousingReminderMinutes = lead;
            configuration.Save();
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Housing.ReminderLeadHint), frameTheme);
    }

    private void DrawMapSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Housing.SettingsMap), frameTheme);
        var card = GroupCard.Begin(frameTheme, 2, SettingsRowHeight);
        var allPlots = SettingsRow.Bool(card.NextRow(), Loc.T(L.Housing.ShowAllPlots), housing.Filters.ShowAllPlots,
            frameTheme);
        if (allPlots != housing.Filters.ShowAllPlots)
        {
            housing.Filters.ShowAllPlots = allPlots;
            housing.PersistFilterDefaults();
            InvalidateCache();
        }

        if (SettingsRow.Action(card.NextRow(), Loc.T(L.Housing.ResetMap), ui.Accent, frameTheme))
        {
            ResetMapView();
            configuration.HousingMapHintDismissed = false;
            configuration.Save();
        }

        card.End();
        SettingsSection.Hint(
            housing.GameMap is null
                ? Loc.T(L.Housing.GameMapUnavailableDetail, housing.GameMapDetail)
                : Loc.T(L.Housing.GameMapHint), frameTheme);
    }

    private void DrawDiagnostics(float scale)
    {
        SettingsSection.Header(Loc.T(L.Housing.SettingsDiagnostics), frameTheme);
        var snapshot = housing.Snapshot;
        var gameMap = housing.GameMap;
        var proxyAge = housing.ProxyCacheAgeSeconds;
#if DEBUG
        var rows = proxyAge is null ? 7 : 8;
#else
        var rows = proxyAge is null ? 6 : 7;
#endif
        var card = GroupCard.Begin(frameTheme, rows, SettingsRowHeight);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Housing.MapSourceLabel),
            gameMap is null
                ? string.Concat(Loc.T(L.Housing.GameMapUnavailable), " · ", housing.GameMapFailure.ToString())
                : gameMap.Main.MapId, frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Housing.ProviderStatus), housing.ProviderName, frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Housing.ApiEndpointLabel), housing.ApiBaseUrl, frameTheme);
        if (proxyAge is { } age)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Housing.ProxyCacheAge),
                HousingFormat.Countdown(TimeSpan.FromSeconds(age)), frameTheme);
        }

        SettingsRow.Info(card.NextRow(), Loc.T(L.Housing.LastRefresh),
            housing.LastSuccessUtc == default
                ? Loc.T(L.Housing.NotReported)
                : HousingFormat.ExactLocalTime(housing.LastSuccessUtc), frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Housing.OpenPlotsReported),
            snapshot is null ? Loc.T(L.Housing.NotReported) : snapshot.OpenPlotCount.ToString(Loc.Culture),
            frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Housing.StatusLabel),
            housing.LastError ?? HousingFormat.FreshnessLabel(FooterFreshness()), frameTheme);
#if DEBUG
        if (SettingsRow.Action(card.NextRow(), Loc.T(L.Housing.CopyMapDiagnostics), ui.Accent, frameTheme))
        {
            ImGui.SetClipboardText(housing.DescribeGameMap());
            ShowToast(Loc.T(L.Housing.CopiedMapDiagnostics));
        }
#endif

        card.End();
        SettingsSection.Hint(Loc.T(L.Housing.DataSourceNotice), frameTheme);
    }

    private int DrawNumberRow(Rect row, string label, string id, int value, int minimum, int maximum, int step,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var controlWidth = 146f * scale;
        var labelMax = MathF.Max(1f, row.Width - controlWidth - 12f * scale);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - Typography.LineHeight(TextStyles.Body) * 0.5f),
            Typography.FitText(label, labelMax, TextStyles.Body), frameTheme.TextStrong, TextStyles.Body);
        var height = 28f * scale;
        var control = new Rect(new Vector2(row.Max.X - controlWidth, row.Center.Y - height * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + height * 0.5f));
        return HousingChrome.NumberStepper(control, id, value, minimum, maximum, step,
            Loc.T(L.Housing.MinutesSuffix), ui);
    }

    private void SetFreshnessMinutes(int minutes)
    {
        configuration.HousingLiveMinutes = Math.Clamp(minutes, 5, 45);
        configuration.HousingRecentMinutes =
            Math.Max(configuration.HousingLiveMinutes + 15, configuration.HousingRecentMinutes);
        configuration.Save();
        InvalidateCache();
    }
}
