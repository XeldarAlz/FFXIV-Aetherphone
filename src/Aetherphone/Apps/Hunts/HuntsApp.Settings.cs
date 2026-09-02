using Aetherphone.Core;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const float NotificationsDisabledAlpha = 0.45f;

    private readonly ChipRail notifyRankRail = new();
    private readonly ChipRail notifyExpansionRail = new();
    private readonly bool[] notifyRankChipActive = new bool[5];
    private readonly bool[] notifyExpansionChipActive = new bool[HuntExpansions.Labels.Length];
    private readonly List<string> notifyWorldOptionsList = new();
    private bool notifySettingsDirty;

    private void DrawSettingsHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + area.Height * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), Loc.T(L.Hunts.SettingsTab), ui.TitleInk,
            1.05f, FontWeight.SemiBold);
    }

    private void DrawSettings(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            Gap(8f);

            var nativeMapMarkersCard = GroupCard.Begin(frameTheme, 1);
            var nativeMapMarkersRow = nativeMapMarkersCard.NextRow();
            UiAnchors.Report("hunts.settings.nativeMapMarkers", nativeMapMarkersRow);
            var nativeMapMarkersValue = SettingsRow.Bool(nativeMapMarkersRow, Loc.T(L.Hunts.NativeMapMarkersLabel),
                configuration.HuntsNativeMapMarkers, frameTheme, "hunts.settings.nativeMapMarkers");
            if (nativeMapMarkersValue != configuration.HuntsNativeMapMarkers)
            {
                configuration.HuntsNativeMapMarkers = nativeMapMarkersValue;
                configuration.Save();
                if (nativeMapMarkersValue)
                {
                    huntsMapMarkers.ForceRedraw();
                }
            }

            nativeMapMarkersCard.End();
            Gap(20f);

            var tutorialCard = GroupCard.Begin(frameTheme, 1);
            if (SettingsRow.Disclosure(tutorialCard.NextRow(), Loc.T(L.Hunts.ResetTutorial), string.Empty,
                    frameTheme))
            {
                OnboardingState.Reset("hunts");
                navigation.GoHome();
            }

            tutorialCard.End();
            Gap(24f);

            DrawNotificationsSectionHeader();

            var notificationsSignedIn = hunts.IsAuthenticated;
            if (!notificationsSignedIn)
            {
                SettingsSection.Hint(Loc.T(L.Hunts.NotificationsSignInHint), frameTheme);
                Gap(12f);
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * NotificationsDisabledAlpha);
            }

            SettingsSection.Header(Loc.T(L.Hunts.RanksLabel), frameTheme);
            DrawNotifyRankChips(notificationsSignedIn);
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.ExpansionsLabel), frameTheme);
            DrawNotifyExpansionChips(notificationsSignedIn);
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.WorldsLabel), frameTheme);
            var worldsCard = GroupCard.Begin(frameTheme, 1);
            var worldsRow = worldsCard.NextRow();
            if (SettingsRow.Disclosure(worldsRow, Loc.T(L.Hunts.WorldsLabel), NotifyWorldsValueText(), frameTheme,
                    "hunts.settings.worlds", dimmed: !notificationsSignedIn, interactive: notificationsSignedIn))
            {
                OpenNotifyWorldMenu(worldsRow);
            }

            worldsCard.End();
            Gap(20f);

            var markCard = GroupCard.Begin(frameTheme, 1);
            if (SettingsRow.Link(markCard.NextRow(), FontAwesomeIcon.Bell, frameTheme.Accent,
                    Loc.T(L.Hunts.MarkNotificationsTitle), MarkNotificationsCountValueText(), frameTheme,
                    id: "hunts.settings.markNotifications", interactive: notificationsSignedIn))
            {
                OpenMarkNotifications();
            }

            markCard.End();
            Gap(20f);

            var resetNotificationsCard = GroupCard.Begin(frameTheme, 1);
            if (SettingsRow.Action(resetNotificationsCard.NextRow(), Loc.T(L.Hunts.ResetToDefault),
                    frameTheme.Accent, frameTheme, interactive: notificationsSignedIn))
            {
                hunts.NotificationSettings.ResetToDefault();
                notifySettingsDirty = true;
            }

            resetNotificationsCard.End();
            Gap(24f);

            if (!notificationsSignedIn)
            {
                ImGui.PopStyleVar();
            }
        }
    }

    private void DrawNotificationsSectionHeader()
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var style = new TextStyle(1.05f, FontWeight.SemiBold);
        var lineHeight = Typography.LineHeight(style);
        var origin = ImGui.GetCursorScreenPos();
        var center = new Vector2(origin.X + ImGui.GetContentRegionAvail().X * 0.5f, origin.Y + lineHeight * 0.5f);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), center, Loc.T(L.Hunts.NotificationsSectionHeader),
            ui.TitleInk, style.Scale, style.Weight);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, lineHeight));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    private string MarkNotificationsCountValueText()
    {
        var count = hunts.NotificationSettings.MobOverrideCount;
        return count == 0 ? string.Empty : Loc.T(L.Hunts.MarkNotificationsCount, count);
    }

    private void DrawNotifyRankChips(bool interactive)
    {
        var settings = hunts.NotificationSettings;
        notifyRankChipActive[0] = settings.RankSS;
        notifyRankChipActive[1] = settings.RankS;
        notifyRankChipActive[2] = settings.RankA;
        notifyRankChipActive[3] = settings.RankB;
        notifyRankChipActive[4] = settings.RankF;
        var tapped = notifyRankRail.Draw(ui, RankChipLabels, notifyRankChipActive, interactive: interactive);
        switch (tapped)
        {
            case 0:
                settings.RankSS = !settings.RankSS;
                notifySettingsDirty = true;
                break;
            case 1:
                settings.RankS = !settings.RankS;
                notifySettingsDirty = true;
                break;
            case 2:
                settings.RankA = !settings.RankA;
                notifySettingsDirty = true;
                break;
            case 3:
                settings.RankB = !settings.RankB;
                notifySettingsDirty = true;
                break;
            case 4:
                settings.RankF = !settings.RankF;
                notifySettingsDirty = true;
                break;
        }
    }

    private void DrawNotifyExpansionChips(bool interactive)
    {
        var settings = hunts.NotificationSettings;
        for (var index = 0; index < notifyExpansionChipActive.Length; index++)
        {
            notifyExpansionChipActive[index] = settings.IsExpansionActive(index);
        }

        var tapped = notifyExpansionRail.Draw(ui, HuntExpansions.Labels, notifyExpansionChipActive,
            labelPadding: ChipRail.CompactLabelPadding, interactive: interactive);
        if (tapped >= 0)
        {
            settings.ToggleExpansion(tapped);
            notifySettingsDirty = true;
        }
    }

    private string NotifyWorldsValueText()
    {
        var dataCenter = hunts.CurrentDataCenter;
        if (dataCenter is null)
        {
            return Loc.T(L.Hunts.AllWorlds);
        }

        var worlds = HuntDataCenterWorlds.WorldsFor(dataCenter);
        var settings = hunts.NotificationSettings;
        var enabledCount = 0;
        for (var index = 0; index < worlds.Length; index++)
        {
            if (settings.IsWorldEnabled(worlds[index]))
            {
                enabledCount++;
            }
        }

        return enabledCount == worlds.Length ? Loc.T(L.Hunts.AllWorlds) : Loc.T(L.Hunts.WorldsSelected, enabledCount);
    }

    private void OpenNotifyWorldMenu(Rect anchor)
    {
        menuTarget = HuntsMenuTarget.NotifyWorld;
        menu.KeepOpen = true;
        PopulateNotifyWorldMenuItems();
        menu.Toggle("hunts.settings.worlds", anchor);
    }

    private void PopulateNotifyWorldMenuItems()
    {
        notifyWorldOptionsList.Clear();
        menuItems.Clear();
        if (hunts.CurrentDataCenter is not { } dataCenter)
        {
            return;
        }

        var settings = hunts.NotificationSettings;
        var worlds = HuntDataCenterWorlds.WorldsFor(dataCenter);
        for (var index = 0; index < worlds.Length; index++)
        {
            var worldId = worlds[index];
            notifyWorldOptionsList.Add(worldId);
            menuItems.Add(new DropdownMenu.Item(Prettify(worldId), string.Empty, false,
                settings.IsWorldEnabled(worldId)));
        }
    }

    private void SaveNotificationSettingsIfDirty()
    {
        if (!notifySettingsDirty)
        {
            return;
        }

        notifySettingsDirty = false;
        hunts.SaveNotificationSettings();
    }
}
