using Aetherphone.Core;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private enum HuntsMenuTarget : byte
    {
        None,
        DataCenter,
        World,
        NotifyWorld,
    }

    private static readonly string[] RankChipLabels = { "SS", "S", "A", "B", "F" };

    private readonly DropdownMenu menu = new();
    private readonly List<DropdownMenu.Item> menuItems = new();
    private readonly List<string> worldOptionsList = new();
    private readonly SortedSet<string> worldOptionsSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly ChipRail rankRail = new();
    private readonly ChipRail statusRail = new();
    private readonly ChipRail expansionRail = new();
    private readonly bool[] rankChipActive = new bool[5];
    private readonly string[] statusChipLabels = new string[4];
    private readonly bool[] statusChipActive = new bool[4];
    private readonly bool[] expansionChipActive = new bool[HuntExpansions.Labels.Length];
    private HuntsMenuTarget menuTarget = HuntsMenuTarget.None;

    private void OpenFilters() => router.Push(new HuntsView(HuntsRoute.Filters));

    private void CloseFilters()
    {
        filterStore.Save(filter.ToSnapshot());
        router.Pop();
        menu.Close();
    }

    private void DrawFiltersHeader(Rect content, float scale)
    {
        var hasNarrowing = filter.HasNarrowingFilters;
        var resetLabel = Loc.T(L.Hunts.ClearFilters);
        var reserve = 12f * scale;
        if (hasNarrowing)
        {
            reserve += AppSkin.HeaderActionWidth(resetLabel);
        }

        AppHeader.DrawTitleWithReserve(content, "hunts.filters.header", Loc.T(L.Hunts.FiltersTitle), reserve,
            ui.TitleInk, scale);

        if (AppHeader.DrawBack(content, scale, "hunts.filters.back", ui.Accent))
        {
            CloseFilters();
        }

        if (hasNarrowing && ui.HeaderAction(content, resetLabel, true))
        {
            filter.Reset();
        }
    }

    private void DrawFiltersBody(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            Gap(8f);

            SettingsSection.Header(Loc.T(L.Hunts.DataCenterLabel), frameTheme);
            var dataCenterCard = GroupCard.Begin(frameTheme, 1);
            var dataCenterRow = dataCenterCard.NextRow();
            var dataCenterValue = hunts.CurrentDataCenter is { Length: > 0 } dataCenter
                ? dataCenter
                : Loc.T(L.Hunts.ChooseDataCenter);
            if (SettingsRow.Disclosure(dataCenterRow, Loc.T(L.Hunts.DataCenterLabel), dataCenterValue, frameTheme,
                    "hunts.filters.datacenter"))
            {
                OpenDataCenterMenu(dataCenterRow);
            }

            dataCenterCard.End();
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.WorldsLabel), frameTheme);
            var worldsCard = GroupCard.Begin(frameTheme, 1);
            var worldsRow = worldsCard.NextRow();
            if (SettingsRow.Disclosure(worldsRow, Loc.T(L.Hunts.WorldsLabel), WorldsValueText(), frameTheme,
                    "hunts.filters.worlds"))
            {
                OpenWorldMenu(worldsRow);
            }

            worldsCard.End();
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.RanksLabel), frameTheme);
            DrawRankChips();
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.StatusLabel), frameTheme);
            DrawStatusChips();
            Gap(20f);

            SettingsSection.Header(Loc.T(L.Hunts.ExpansionsLabel), frameTheme);
            DrawExpansionChips();
            Gap(24f);

            if (ui.PillButton(Reserve(40f), Loc.T(L.Hunts.Submit), true))
            {
                CloseFilters();
            }

            Gap(40f);
        }
    }

    private void DrawRankChips()
    {
        rankChipActive[0] = filter.RankSS;
        rankChipActive[1] = filter.RankS;
        rankChipActive[2] = filter.RankA;
        rankChipActive[3] = filter.RankB;
        rankChipActive[4] = filter.RankF;
        var tapped = rankRail.Draw(ui, RankChipLabels, rankChipActive);
        switch (tapped)
        {
            case 0:
                filter.RankSS = !filter.RankSS;
                break;
            case 1:
                filter.RankS = !filter.RankS;
                break;
            case 2:
                filter.RankA = !filter.RankA;
                break;
            case 3:
                filter.RankB = !filter.RankB;
                break;
            case 4:
                filter.RankF = !filter.RankF;
                break;
        }
    }

    private void DrawStatusChips()
    {
        statusChipLabels[0] = Loc.T(L.Hunts.Closed);
        statusChipActive[0] = filter.StatusClosed;
        statusChipLabels[1] = Loc.T(L.Hunts.Open);
        statusChipActive[1] = filter.StatusOpen;
        statusChipLabels[2] = Loc.T(L.Hunts.Capped);
        statusChipActive[2] = filter.StatusCapped;
        statusChipLabels[3] = Loc.T(L.Hunts.Unmet);
        statusChipActive[3] = filter.StatusUnmet;
        var tapped = statusRail.Draw(ui, statusChipLabels, statusChipActive);
        switch (tapped)
        {
            case 0:
                filter.StatusClosed = !filter.StatusClosed;
                break;
            case 1:
                filter.StatusOpen = !filter.StatusOpen;
                break;
            case 2:
                filter.StatusCapped = !filter.StatusCapped;
                break;
            case 3:
                filter.StatusUnmet = !filter.StatusUnmet;
                break;
        }
    }

    private void DrawExpansionChips()
    {
        for (var index = 0; index < expansionChipActive.Length; index++)
        {
            expansionChipActive[index] = filter.IsExpansionActive(index);
        }

        var tapped = expansionRail.Draw(ui, HuntExpansions.Labels, expansionChipActive,
            labelPadding: ChipRail.CompactLabelPadding);
        if (tapped >= 0)
        {
            filter.ToggleExpansion(tapped);
        }
    }

    private string WorldsValueText()
    {
        var count = filter.Worlds.Count;
        return count == 0 ? Loc.T(L.Hunts.AllWorlds) : Loc.T(L.Hunts.WorldsSelected, count);
    }

    private void OpenDataCenterMenu(Rect anchor)
    {
        menuItems.Clear();
        var all = MusterDataCenters.All;
        for (var index = 0; index < all.Length; index++)
        {
            var name = all[index].Name;
            menuItems.Add(new DropdownMenu.Item(name, string.Empty, false,
                string.Equals(name, hunts.CurrentDataCenter, StringComparison.OrdinalIgnoreCase)));
        }

        menuTarget = HuntsMenuTarget.DataCenter;
        menu.KeepOpen = false;
        menu.Toggle("hunts.filters.datacenter", anchor);
    }

    private void OpenWorldMenu(Rect anchor)
    {
        menuTarget = HuntsMenuTarget.World;
        menu.KeepOpen = true;
        PopulateWorldMenuItems();
        menu.Toggle("hunts.filters.worlds", anchor);
    }

    private void PopulateWorldMenuItems()
    {
        HuntsFilterState.CollectWorlds(hunts.Windows, worldOptionsSet);
        worldOptionsList.Clear();
        menuItems.Clear();
        foreach (var worldId in worldOptionsSet)
        {
            worldOptionsList.Add(worldId);
            menuItems.Add(new DropdownMenu.Item(Prettify(worldId), string.Empty, false,
                filter.IsWorldSelected(worldId)));
        }
    }

    private void DrawMenu()
    {
        if (!menu.Open || menuItems.Count == 0)
        {
            return;
        }

        var picked = menu.Draw(frameScreen, frameTheme, System.Runtime.InteropServices.CollectionsMarshal
            .AsSpan(menuItems));
        if (picked < 0)
        {
            return;
        }

        if (menuTarget == HuntsMenuTarget.DataCenter)
        {
            var all = MusterDataCenters.All;
            if (picked < all.Length)
            {
                hunts.SelectDataCenter(all[picked].Name);
                filter.ClearWorlds();
            }

            menuTarget = HuntsMenuTarget.None;
            menuItems.Clear();
            return;
        }

        if (menuTarget == HuntsMenuTarget.World && picked < worldOptionsList.Count)
        {
            filter.ToggleWorld(worldOptionsList[picked]);
            PopulateWorldMenuItems();
            return;
        }

        if (menuTarget == HuntsMenuTarget.NotifyWorld && picked < notifyWorldOptionsList.Count)
        {
            hunts.NotificationSettings.ToggleWorld(notifyWorldOptionsList[picked]);
            notifySettingsDirty = true;
            PopulateNotifyWorldMenuItems();
        }
    }

    private static Rect Reserve(float heightUnscaled)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + heightUnscaled * scale));
        ImGui.Dummy(new Vector2(width, heightUnscaled * scale));
        return rect;
    }

    private static void Gap(float pixels) => ImGui.Dummy(new Vector2(0f, pixels * UiScale.Current));
}
