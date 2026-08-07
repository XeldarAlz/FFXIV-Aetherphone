using Aetherphone.Core;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const float WorldRowHeight = 44f;
    private const float WorldSearchHeight = 44f;

    private readonly List<HousingWorld> worldMatches = new();

    private void DrawWorldPickerRoute(Rect area)
    {
        var scale = UiScale.Current;
        var body = DrawSubHeader(area, "housing.header.worlds", Loc.T(L.Housing.SelectWorldTitle));
        var pad = 16f * scale;
        var searchBar = new Rect(new Vector2(body.Min.X + pad, body.Min.Y + 4f * scale),
            new Vector2(body.Max.X - pad, body.Min.Y + 4f * scale + WorldSearchHeight * scale));
        SearchField.Draw(searchBar, "##housingWorldSearch", Loc.T(L.Housing.SearchWorlds), ref worldSearch,
            ui.Palette, 40);
        var listBody = new Rect(new Vector2(body.Min.X, searchBar.Max.Y), body.Max);
        using (AppSurface.Begin(listBody))
        {
            var worlds = housing.Worlds;
            if (worlds.Count == 0)
            {
                var label = housing.WorldsLoading ? LoadingPulse.SafeLabel() : Loc.T(L.Housing.Offline);
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(2f * scale, 20f * scale), label, ui.MutedInk,
                    TextStyles.Subheadline);
                ImGui.Dummy(new Vector2(ScrollLayout.StableContentWidth(), 60f * scale));
                return;
            }

            var query = worldSearch.Trim();
            if (query.Length > 0)
            {
                DrawWorldSearchResults(query, scale);
                return;
            }

            DrawWorldGroups(scale);
        }
    }

    private void DrawWorldSearchResults(string query, float scale)
    {
        worldMatches.Clear();
        var worlds = housing.Worlds;
        for (var index = 0; index < worlds.Count; index++)
        {
            if (worlds[index].Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                worlds[index].DataCenterName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                worldMatches.Add(worlds[index]);
            }
        }

        if (worldMatches.Count == 0)
        {
            Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(2f * scale, 20f * scale),
                Loc.T(L.Housing.NoWorldMatches), ui.MutedInk, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(ScrollLayout.StableContentWidth(), 60f * scale));
            return;
        }

        worldMatches.Sort(static (first, second) =>
            string.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase));
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var card = GroupCard.Begin(frameTheme, worldMatches.Count, WorldRowHeight);
        for (var index = 0; index < worldMatches.Count; index++)
        {
            var world = worldMatches[index];
            if (DrawWorldRow(card.NextRow(), world.Name, DetailFor(world), IsCurrentWorld(world.Id), scale))
            {
                PickWorld(world.Id);
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawWorldGroups(float scale)
    {
        var regions = HousingRegions.Order;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            var dataCenters = CollectDataCenters(region);
            for (var dcIndex = 0; dcIndex < dataCenters.Count; dcIndex++)
            {
                var dataCenter = dataCenters[dcIndex];
                worldMatches.Clear();
                var worlds = housing.Worlds;
                for (var index = 0; index < worlds.Count; index++)
                {
                    if (string.Equals(worlds[index].DataCenterName, dataCenter, StringComparison.Ordinal))
                    {
                        worldMatches.Add(worlds[index]);
                    }
                }

                if (worldMatches.Count == 0)
                {
                    continue;
                }

                worldMatches.Sort(static (first, second) =>
                    string.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase));
                SettingsSection.Header(string.Concat(region, " · ", dataCenter), frameTheme);
                var card = GroupCard.Begin(frameTheme, worldMatches.Count, WorldRowHeight);
                for (var index = 0; index < worldMatches.Count; index++)
                {
                    var world = worldMatches[index];
                    var detail = world.Id == housing.HomeWorldId ? Loc.T(L.Housing.HomeWorld) : string.Empty;
                    if (DrawWorldRow(card.NextRow(), world.Name, detail, IsCurrentWorld(world.Id), scale))
                    {
                        PickWorld(world.Id);
                    }
                }

                card.End();
            }
        }

        ImGui.Dummy(new Vector2(0f, 20f * scale));
    }

    private readonly List<string> dataCenterBuffer = new();

    private List<string> CollectDataCenters(string region)
    {
        dataCenterBuffer.Clear();
        var worlds = housing.Worlds;
        for (var index = 0; index < worlds.Count; index++)
        {
            var world = worlds[index];
            if (!string.Equals(world.RegionName, region, StringComparison.Ordinal) ||
                dataCenterBuffer.Contains(world.DataCenterName))
            {
                continue;
            }

            dataCenterBuffer.Add(world.DataCenterName);
        }

        dataCenterBuffer.Sort(StringComparer.OrdinalIgnoreCase);
        return dataCenterBuffer;
    }

    private bool IsCurrentWorld(uint worldId) => housing.WorldId == worldId;

    private string DetailFor(HousingWorld world) =>
        world.Id == housing.HomeWorldId
            ? string.Concat(world.DataCenterName, " · ", Loc.T(L.Housing.HomeWorld))
            : world.DataCenterName;

    private void PickWorld(uint worldId)
    {
        housing.SelectWorld(worldId);
        ResetMapView();
        sheetOpen = false;
        selectedPlot = default;
        InvalidateCache();
        router.Pop();
    }

    private bool DrawWorldRow(Rect row, string name, string detail, bool selected, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X - 8f * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X + 8f * scale, row.Max.Y - 2f * scale), Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(frameTheme.Accent, 0.14f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var checkWidth = 22f * scale;
        var detailStyle = TextStyles.Footnote;
        var detailSize = detail.Length > 0 ? Typography.Measure(detail, detailStyle) : Vector2.Zero;
        var nameMax = MathF.Max(1f, row.Width - checkWidth - detailSize.X - 14f * scale);
        var nameStyle = TextStyles.Body;
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - Typography.LineHeight(nameStyle) * 0.5f),
            Typography.FitText(name, nameMax, nameStyle), frameTheme.TextStrong, nameStyle);
        if (detail.Length > 0)
        {
            Typography.Draw(drawList,
                new Vector2(row.Max.X - checkWidth - detailSize.X, row.Center.Y - detailSize.Y * 0.5f), detail,
                frameTheme.TextMuted, detailStyle);
        }

        if (selected)
        {
            var center = new Vector2(row.Max.X - 8f * scale, row.Center.Y);
            var color = ImGui.GetColorU32(frameTheme.Accent);
            drawList.AddLine(center + new Vector2(-5f * scale, 0f), center + new Vector2(-1.6f * scale, 3.8f * scale),
                color, 2f * scale);
            drawList.AddLine(center + new Vector2(-1.6f * scale, 3.8f * scale),
                center + new Vector2(5f * scale, -4.2f * scale), color, 2f * scale);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }
}
