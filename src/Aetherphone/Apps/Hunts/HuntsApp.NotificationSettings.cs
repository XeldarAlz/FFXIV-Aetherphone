using Aetherphone.Core;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private readonly ChipRail notifyRankRail = new();
    private readonly ChipRail notifyExpansionRail = new();
    private readonly bool[] notifyRankChipActive = new bool[5];
    private readonly bool[] notifyExpansionChipActive = new bool[HuntExpansions.Labels.Length];
    private readonly List<string> notifyWorldOptionsList = new();
    private bool notifySettingsDirty;

    private void DrawNotificationSettingsHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + area.Height * 0.5f;
        var resetLabel = Loc.T(L.Hunts.ResetToDefault);
        var buttonHeight = 28f * scale;
        var buttonWidth = AppSkin.PillWidthFor(resetLabel, buttonHeight);
        var buttonRect = new Rect(new Vector2(area.Max.X - buttonWidth, rowCenterY - buttonHeight * 0.5f),
            new Vector2(area.Max.X, rowCenterY + buttonHeight * 0.5f));

        var titleStyle = new TextStyle(1.05f, FontWeight.SemiBold);
        var titleY = rowCenterY - Typography.LineHeight(titleStyle) * 0.5f;
        var titleMaxWidth = MathF.Max(0f, buttonRect.Min.X - area.Min.X - 12f * scale);
        Marquee.DrawLeftAuto("hunts.notificationSettings.header", Loc.T(L.Hunts.NotificationSettingsTitle),
            area.Min.X, titleY, titleMaxWidth, titleStyle, ui.TitleInk);

        if (ui.PillButton(buttonRect, resetLabel, true))
        {
            hunts.NotificationSettings.ResetToDefault();
            notifySettingsDirty = true;
        }
    }

    private void DrawNotificationSettings(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            Gap(8f);

            SettingsSection.Header(Loc.T(L.Hunts.RanksLabel), frameTheme);
            DrawNotifyRankChips();
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.ExpansionsLabel), frameTheme);
            DrawNotifyExpansionChips();
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.WorldsLabel), frameTheme);
            var worldsCard = GroupCard.Begin(frameTheme, 1);
            var worldsRow = worldsCard.NextRow();
            if (SettingsRow.Disclosure(worldsRow, Loc.T(L.Hunts.WorldsLabel), NotifyWorldsValueText(), frameTheme,
                    "hunts.notificationSettings.worlds"))
            {
                OpenNotifyWorldMenu(worldsRow);
            }

            worldsCard.End();
            Gap(20f);

            var markCard = GroupCard.Begin(frameTheme, 1);
            if (SettingsRow.Link(markCard.NextRow(), FontAwesomeIcon.Bell, frameTheme.Accent,
                    Loc.T(L.Hunts.MarkNotificationsTitle), MarkNotificationsCountValueText(), frameTheme,
                    id: "hunts.notificationSettings.markNotifications"))
            {
                OpenMarkNotifications();
            }

            markCard.End();
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
        }
    }

    private string MarkNotificationsCountValueText()
    {
        var count = hunts.NotificationSettings.MobOverrideCount;
        return count == 0 ? string.Empty : Loc.T(L.Hunts.MarkNotificationsCount, count);
    }

    private void DrawNotifyRankChips()
    {
        var settings = hunts.NotificationSettings;
        notifyRankChipActive[0] = settings.RankSS;
        notifyRankChipActive[1] = settings.RankS;
        notifyRankChipActive[2] = settings.RankA;
        notifyRankChipActive[3] = settings.RankB;
        notifyRankChipActive[4] = settings.RankF;
        var tapped = notifyRankRail.Draw(ui, RankChipLabels, notifyRankChipActive);
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

    private void DrawNotifyExpansionChips()
    {
        var settings = hunts.NotificationSettings;
        for (var index = 0; index < notifyExpansionChipActive.Length; index++)
        {
            notifyExpansionChipActive[index] = settings.IsExpansionActive(index);
        }

        var tapped = notifyExpansionRail.Draw(ui, HuntExpansions.Labels, notifyExpansionChipActive,
            labelPadding: ChipRail.CompactLabelPadding);
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
        menu.Toggle("hunts.notificationSettings.worlds", anchor);
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
