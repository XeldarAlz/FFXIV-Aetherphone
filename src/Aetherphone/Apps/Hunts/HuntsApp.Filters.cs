using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const int StatusColumns = 2;

    private static readonly string[] StatusTileIds =
    {
        "hunts.filter.status.open", "hunts.filter.status.capped", "hunts.filter.status.closed",
        "hunts.filter.status.unmet",
    };

    private static readonly string[] FilterExpansionTileIds =
    {
        "hunts.filter.expansion.0", "hunts.filter.expansion.1", "hunts.filter.expansion.2",
        "hunts.filter.expansion.3", "hunts.filter.expansion.4", "hunts.filter.expansion.5",
    };

    private readonly ChipRail dataCenterRail = new();
    private string[] dataCenterLabels = Array.Empty<string>();
    private bool[] dataCenterActive = Array.Empty<bool>();
    private readonly List<string> foreignWorlds = new();
    private string? filterDataCenter;
    private int savedFilterRevision = -1;

    private void OpenFilters()
    {
        savedFilterRevision = filter.Revision;
        Push(new HuntsView(HuntsRoute.Filters, BackTitle: RootTitle()));
    }

    private void SyncFilterDataCenter()
    {
        if (hunts.CurrentDataCenter is not { Length: > 0 } dataCenter ||
            string.Equals(dataCenter, filterDataCenter, StringComparison.Ordinal))
        {
            return;
        }

        var worlds = HuntDataCenterWorlds.WorldsFor(dataCenter);
        if (worlds.Length == 0)
        {
            return;
        }

        filterDataCenter = dataCenter;
        if (filter.Worlds.Count == 0)
        {
            return;
        }

        foreignWorlds.Clear();
        foreach (var selected in filter.Worlds)
        {
            if (!ContainsWorld(worlds, selected))
            {
                foreignWorlds.Add(selected);
            }
        }

        if (foreignWorlds.Count == 0)
        {
            return;
        }

        for (var index = 0; index < foreignWorlds.Count; index++)
        {
            filter.ToggleWorld(foreignWorlds[index]);
        }

        filterStore.Save(filter.ToSnapshot());
    }

    private static bool ContainsWorld(string[] worlds, string worldId)
    {
        for (var index = 0; index < worlds.Length; index++)
        {
            if (string.Equals(worlds[index], worldId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SaveFiltersIfDirty()
    {
        if (savedFilterRevision < 0 || filter.Revision == savedFilterRevision)
        {
            return;
        }

        savedFilterRevision = filter.Revision;
        filterStore.Save(filter.ToSnapshot());
    }

    private void DrawFilters(in PhoneContext context, HuntsView view)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("hunts.filters"))
        using (AppSurface.Begin(navBar.Body))
        {
            DrawDataCenterRail();
            DrawFilterWorlds(scale);
            DrawFilterRanks(scale);
            DrawFilterStatuses(scale);
            DrawFilterExpansions(scale);
            BottomSpacer(scale);
        }

        navButtons[0] = new NavBarButton(PhoneIcons.Refresh, Loc.T(L.Hunts.ClearFilters));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "hunts.filters.nav", Loc.T(L.Hunts.FiltersTitle),
            NavBarStyle.From(ui), navButtons.AsSpan(0, 1), view.BackTitle, back);
        if (pressed == 0)
        {
            filter.Reset();
            UiFeedback.Play(UiSound.Refresh);
        }
    }

    private void DrawDataCenterRail()
    {
        var all = MusterDataCenters.All;
        if (all.Length == 0)
        {
            return;
        }

        if (dataCenterLabels.Length != all.Length)
        {
            dataCenterLabels = new string[all.Length];
            dataCenterActive = new bool[all.Length];
            for (var index = 0; index < all.Length; index++)
            {
                dataCenterLabels[index] = all[index].Name;
            }
        }

        var current = hunts.CurrentDataCenter;
        for (var index = 0; index < all.Length; index++)
        {
            dataCenterActive[index] = string.Equals(all[index].Name, current, StringComparison.OrdinalIgnoreCase);
        }

        ui.SectionLabel(Loc.T(L.Hunts.DataCenterLabel), TextStyles.FootnoteEmphasized, 6f);
        var tapped = dataCenterRail.Draw(ui, dataCenterLabels, dataCenterActive, "hunts.filters.datacenter");
        if (tapped >= 0 && !dataCenterActive[tapped])
        {
            hunts.SelectDataCenter(all[tapped].Name);
            filter.ClearWorlds();
            boardDirty = true;
            UiFeedback.Play(UiSound.Tap);
        }

        Gap(HuntsArt.CardGap);
    }

    private void DrawFilterWorlds(float scale)
    {
        if (hunts.CurrentDataCenter is not { Length: > 0 } dataCenter)
        {
            return;
        }

        var worlds = HuntDataCenterWorlds.WorldsFor(dataCenter);
        if (worlds.Length == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Hunts.WorldsLabel), TextStyles.FootnoteEmphasized, 6f);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var showingAll = filter.Worlds.Count == 0;
        for (var index = 0; index < worlds.Length; index++)
        {
            var active = showingAll || filter.IsWorldSelected(worlds[index]);
            var rect = HuntsArt.TileRect(origin, width, index, WorldColumns, scale);
            var label = ResolveWorldLabel(worlds[index]);
            if (!HuntsArt.ToggleTile(ui, label, rect, label, string.Empty, active, ui.Accent, true, scale))
            {
                continue;
            }

            ToggleFilterWorld(worlds, index, showingAll);
            UiFeedback.Play(active ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.Dummy(new Vector2(width, HuntsArt.TileGridHeight(worlds.Length, WorldColumns, scale) +
                                       HuntsArt.CardGap * scale));
    }

    private void ToggleFilterWorld(string[] worlds, int tapped, bool showingAll)
    {
        if (showingAll)
        {
            for (var index = 0; index < worlds.Length; index++)
            {
                if (index != tapped)
                {
                    filter.ToggleWorld(worlds[index]);
                }
            }

            return;
        }

        filter.ToggleWorld(worlds[tapped]);
        var selected = 0;
        for (var index = 0; index < worlds.Length; index++)
        {
            if (filter.IsWorldSelected(worlds[index]))
            {
                selected++;
            }
        }

        if (selected == 0 || selected == worlds.Length)
        {
            filter.ClearWorlds();
        }
    }

    private void DrawFilterRanks(float scale)
    {
        ui.SectionLabel(Loc.T(L.Hunts.RanksLabel), TextStyles.FootnoteEmphasized, 6f);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        for (var index = 0; index < RankLabels.Length; index++)
        {
            var active = FilterRank(index);
            var rect = HuntsArt.TileRect(origin, width, index, RankColumns, scale);
            if (!HuntsArt.ToggleTile(ui, FilterRankTileIds[index], rect, RankLabels[index], string.Empty, active,
                    HuntsArt.RankColor(RankLabels[index], ui.Accent), true, scale))
            {
                continue;
            }

            SetFilterRank(index, !active);
            UiFeedback.Play(active ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.Dummy(new Vector2(width, HuntsArt.TileGridHeight(RankLabels.Length, RankColumns, scale) +
                                       HuntsArt.CardGap * scale));
    }

    private bool FilterRank(int index) => index switch
    {
        0 => filter.RankSS,
        1 => filter.RankS,
        2 => filter.RankA,
        3 => filter.RankB,
        _ => filter.RankF,
    };

    private void SetFilterRank(int index, bool value)
    {
        switch (index)
        {
            case 0:
                filter.RankSS = value;
                break;
            case 1:
                filter.RankS = value;
                break;
            case 2:
                filter.RankA = value;
                break;
            case 3:
                filter.RankB = value;
                break;
            default:
                filter.RankF = value;
                break;
        }
    }

    private void DrawFilterStatuses(float scale)
    {
        ui.SectionLabel(Loc.T(L.Hunts.StatusLabel), TextStyles.FootnoteEmphasized, 6f);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        for (var index = 0; index < StatusTileIds.Length; index++)
        {
            var status = FilterStatusAt(index);
            var active = FilterStatus(status);
            var rect = HuntsArt.TileRect(origin, width, index, StatusColumns, scale);
            if (!HuntsArt.ToggleTile(ui, StatusTileIds[index], rect, StatusLabel(status), string.Empty, active,
                    HuntsArt.StatusColor(status, ui.Accent), true, scale))
            {
                continue;
            }

            SetFilterStatus(status, !active);
            UiFeedback.Play(active ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.Dummy(new Vector2(width, HuntsArt.TileGridHeight(StatusTileIds.Length, StatusColumns, scale) +
                                       HuntsArt.CardGap * scale));
    }

    private static HuntWindowStatus FilterStatusAt(int index) => index switch
    {
        0 => HuntWindowStatus.Open,
        1 => HuntWindowStatus.Capped,
        2 => HuntWindowStatus.Closed,
        _ => HuntWindowStatus.Unmet,
    };

    private bool FilterStatus(HuntWindowStatus status) => status switch
    {
        HuntWindowStatus.Open => filter.StatusOpen,
        HuntWindowStatus.Capped => filter.StatusCapped,
        HuntWindowStatus.Closed => filter.StatusClosed,
        _ => filter.StatusUnmet,
    };

    private void SetFilterStatus(HuntWindowStatus status, bool value)
    {
        switch (status)
        {
            case HuntWindowStatus.Open:
                filter.StatusOpen = value;
                break;
            case HuntWindowStatus.Capped:
                filter.StatusCapped = value;
                break;
            case HuntWindowStatus.Closed:
                filter.StatusClosed = value;
                break;
            default:
                filter.StatusUnmet = value;
                break;
        }
    }

    private void DrawFilterExpansions(float scale)
    {
        ui.SectionLabel(Loc.T(L.Hunts.ExpansionsLabel), TextStyles.FootnoteEmphasized, 6f);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var count = HuntExpansions.Ids.Length;
        for (var index = 0; index < count; index++)
        {
            var active = filter.IsExpansionActive(index);
            var rect = HuntsArt.TileRect(origin, width, index, ExpansionColumns, scale);
            if (!HuntsArt.ToggleTile(ui, FilterExpansionTileIds[index], rect, ExpansionName(index),
                    HuntExpansions.Labels[index], active, ui.Accent, true, scale))
            {
                continue;
            }

            filter.ToggleExpansion(index);
            UiFeedback.Play(active ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.Dummy(new Vector2(width, HuntsArt.TileGridHeight(count, ExpansionColumns, scale) +
                                       HuntsArt.CardGap * scale));
    }
}
