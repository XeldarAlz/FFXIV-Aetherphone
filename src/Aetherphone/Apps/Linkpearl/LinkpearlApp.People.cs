using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Geography;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private enum PeopleScope : byte
    {
        Friends,
        Online,
        Lodestone,
    }

    private readonly record struct RegionOption(string Value, bool DataCenter);

    private const float PeopleSearchHeight = 52f;
    private const float PeopleChipsHeight = 44f;
    private const float PeopleKindHeight = 44f;
    private const float PeopleSearchRevealSeconds = 0.16f;
    private const float PeopleEmptyTop = 60f;
    private const float RegionPillMinWidth = 96f;
    private const float RegionPillMaxShare = 0.45f;
    private const float RegionGlyphInset = 16f;
    private const float RegionGlyphSize = 16f;
    private const float RegionChevronSize = 14f;
    private const float RegionLabelLeft = 30f;
    private const float RegionLabelRight = 28f;
    private const string RegionMenuId = "linkpearl.people.region";
    private static readonly TextStyle RegionLabelStyle = TextStyles.Body;

    private readonly ChipRail peopleRail = new();
    private readonly string[] peopleScopeLabels = new string[3];
    private readonly bool[] peopleScopeActive = new bool[3];
    private readonly DropdownMenu regionMenu = new();
    private RegionOption[] regionOptions = Array.Empty<RegionOption>();
    private DropdownMenu.Item[] regionItems = Array.Empty<DropdownMenu.Item>();
    private string peopleSearch = string.Empty;
    private PeopleScope peopleScope;
    private Spring peopleSearchReveal;
    private bool peopleSearchOpen;
    private bool peopleSearchFocus;

    private void ResetPeopleState()
    {
        peopleSearch = string.Empty;
        peopleScope = PeopleScope.Friends;
        peopleSearchOpen = false;
        peopleSearchFocus = false;
        peopleSearchReveal.SnapTo(0f);
        regionMenu.Close();
        ResetFindState();
    }

    private void TogglePeopleSearch()
    {
        if (peopleSearchOpen)
        {
            peopleSearchOpen = false;
            peopleSearch = string.Empty;
            return;
        }

        peopleSearchOpen = true;
        peopleSearchFocus = true;
    }

    private void DrawPeopleTab(Rect content)
    {
        var scale = UiScale.Current;
        var top = DrawPeopleScopeRail(content, scale);
        var lodestoneScope = peopleScope == PeopleScope.Lodestone;
        top = DrawPeopleSearchRow(new Rect(new Vector2(content.Min.X, top), content.Max), scale, lodestoneScope);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        if (!lodestoneScope)
        {
            DrawFriendsScope(body, peopleScope == PeopleScope.Online, scale);
            return;
        }

        DrawLodestoneScope(body, scale);
        DrawRegionMenu();
    }

    private float DrawPeopleScopeRail(Rect content, float scale)
    {
        var railTop = content.Min.Y + (PeopleChipsHeight - ChipRail.RowHeight) * 0.5f * scale;
        var rail = new Rect(new Vector2(content.Min.X + CellPadX * scale, railTop),
            new Vector2(content.Max.X - CellPadX * scale, railTop + ChipRail.RowHeight * scale));
        peopleScopeLabels[0] = Loc.T(L.Linkpearl.ScopeFriends);
        peopleScopeLabels[1] = Loc.T(L.Contacts.Online);
        peopleScopeLabels[2] = Loc.T(L.Linkpearl.ScopeLodestone);
        for (var index = 0; index < peopleScopeActive.Length; index++)
        {
            peopleScopeActive[index] = (int)peopleScope == index;
        }

        var tapped = peopleRail.Draw(rail, ui, peopleScopeLabels, peopleScopeActive, false, "people.scope");
        if (tapped >= 0 && tapped != (int)peopleScope)
        {
            SelectPeopleScope((PeopleScope)tapped);
        }

        return content.Min.Y + PeopleChipsHeight * scale;
    }

    private void SelectPeopleScope(PeopleScope scope)
    {
        peopleScope = scope;
        regionMenu.Close();
        if (scope != PeopleScope.Lodestone)
        {
            return;
        }

        peopleSearchFocus = true;
        if (peopleSearch.Trim().Length > 0)
        {
            SubmitPeopleSearch();
        }
    }

    private float DrawPeopleSearchRow(Rect area, float scale, bool forced)
    {
        var target = peopleSearchOpen || forced ? 1f : 0f;
        var frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var reveal = peopleSearchReveal.Step(target, PeopleSearchRevealSeconds, frameSeconds);
        if (peopleSearchReveal.IsResting(target, 0.005f, 0.05f))
        {
            peopleSearchReveal.SnapTo(target);
            reveal = target;
        }

        var height = PeopleSearchHeight * scale * Math.Clamp(reveal, 0f, 1f);
        if (height < 1f)
        {
            return area.Min.Y;
        }

        var drawList = ImGui.GetWindowDrawList();
        var bottom = area.Min.Y + height;
        drawList.PushClipRect(area.Min, new Vector2(area.Max.X, bottom), true);
        var bar = new Rect(new Vector2(area.Min.X + CellPadX * scale, bottom - PeopleSearchHeight * scale),
            new Vector2(area.Max.X - CellPadX * scale, bottom));
        if (forced)
        {
            DrawLodestoneSearchBar(bar, scale);
        }
        else
        {
            SearchField.Draw(bar, "##peopleSearch", Loc.T(L.Common.Search), ref peopleSearch, ui.Palette,
                focus: peopleSearchFocus);
        }

        peopleSearchFocus = false;
        drawList.PopClipRect();
        return bottom;
    }

    private void DrawLodestoneSearchBar(Rect bar, float scale)
    {
        var regionLabel = findRegion.Length > 0 ? findRegion : Loc.T(L.FindPeople.AllWorlds);
        var labelWidth = Typography.Measure(regionLabel, RegionLabelStyle).X;
        var pillWidth = Math.Clamp(labelWidth + (RegionLabelLeft + RegionLabelRight) * scale,
            RegionPillMinWidth * scale, MathF.Max(RegionPillMinWidth * scale, bar.Width * RegionPillMaxShare));
        var pillHalfHeight = Metrics.Size.FieldHeight * 0.5f * scale;
        var pill = new Rect(new Vector2(bar.Max.X - pillWidth, bar.Center.Y - pillHalfHeight),
            new Vector2(bar.Max.X, bar.Center.Y + pillHalfHeight));
        var field = new Rect(bar.Min, new Vector2(pill.Min.X - Metrics.Space.Sm * scale, bar.Max.Y));
        if (peopleSearchFocus)
        {
            ImGui.SetKeyboardFocusHere();
        }

        if (SearchField.DrawSubmit(field, "##peopleSearch", Loc.T(L.FindPeople.NameHint), ref peopleSearch,
                ui.Palette, sideInset: 0f))
        {
            SubmitPeopleSearch();
        }

        if (DrawRegionPill(pill, regionLabel, scale))
        {
            OpenRegionMenu(pill);
        }
    }

    private bool DrawRegionPill(Rect pill, string label, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var palette = ui.Palette;
        var hovered = UiInteract.Hover(pill.Min, pill.Max);
        var lifted = hovered || regionMenu.IsOpenFor(RegionMenuId);
        Squircle.Fill(drawList, pill.Min, pill.Max, pill.Height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(palette.TitleInk, lifted ? FillLevel.Secondary : FillLevel.Tertiary)));
        PhoneIcon.Draw(drawList, new Vector2(pill.Min.X + RegionGlyphInset * scale, pill.Center.Y), PhoneIcons.World,
            palette.MutedInk, RegionGlyphSize * scale);
        PhoneIcon.Draw(drawList, new Vector2(pill.Max.X - RegionGlyphInset * scale, pill.Center.Y),
            PhoneIcons.ChevronDown, palette.MutedInk, RegionChevronSize * scale);
        var labelLeft = pill.Min.X + RegionLabelLeft * scale;
        var labelRoom = MathF.Max(1f, pill.Max.X - RegionLabelRight * scale - labelLeft);
        var fitted = Typography.FitText(label, labelRoom, RegionLabelStyle);
        var fittedSize = Typography.Measure(fitted, RegionLabelStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, pill.Center.Y - fittedSize.Y * 0.5f), fitted,
            palette.TitleInk, RegionLabelStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(pill.Min, pill.Max, hovered);
    }

    private void OpenRegionMenu(Rect anchor)
    {
        if (regionOptions.Length == 0)
        {
            regionOptions = BuildRegionOptions();
            regionItems = new DropdownMenu.Item[regionOptions.Length];
        }

        var allWorldsGlyph = IconGlyph.Of(FontAwesomeIcon.Globe);
        var dataCenterGlyph = IconGlyph.Of(FontAwesomeIcon.Server);
        for (var index = 0; index < regionOptions.Length; index++)
        {
            var option = regionOptions[index];
            var selected = string.Equals(option.Value, findRegion, StringComparison.OrdinalIgnoreCase);
            if (option.Value.Length == 0)
            {
                regionItems[index] = new DropdownMenu.Item(Loc.T(L.FindPeople.AllWorlds), allWorldsGlyph,
                    Selected: selected);
                continue;
            }

            regionItems[index] = new DropdownMenu.Item(option.Value, option.DataCenter ? dataCenterGlyph : string.Empty,
                Selected: selected);
        }

        regionMenu.Header = Loc.T(L.FindPeople.WorldMenu);
        regionMenu.Toggle(RegionMenuId, anchor);
    }

    private void DrawRegionMenu()
    {
        if (!regionMenu.IsOpenFor(RegionMenuId))
        {
            return;
        }

        var picked = regionMenu.Draw(screenRect, frameTheme, regionItems);
        if (picked < 0)
        {
            return;
        }

        var region = regionOptions[picked].Value;
        if (string.Equals(region, findRegion, StringComparison.Ordinal))
        {
            return;
        }

        findRegion = region;
        if (peopleSearch.Trim().Length > 0)
        {
            SubmitPeopleSearch();
        }
    }

    private static RegionOption[] BuildRegionOptions()
    {
        var regions = WorldGeography.Regions;
        var count = 1;
        for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
        {
            var dataCenters = regions[regionIndex].DataCenters;
            for (var dataCenterIndex = 0; dataCenterIndex < dataCenters.Length; dataCenterIndex++)
            {
                count += 1 + dataCenters[dataCenterIndex].Worlds.Length;
            }
        }

        var options = new RegionOption[count];
        options[0] = new RegionOption(string.Empty, false);
        var cursor = 1;
        for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
        {
            var dataCenters = regions[regionIndex].DataCenters;
            for (var dataCenterIndex = 0; dataCenterIndex < dataCenters.Length; dataCenterIndex++)
            {
                var dataCenter = dataCenters[dataCenterIndex];
                options[cursor++] = new RegionOption(dataCenter.Name, true);
                for (var worldIndex = 0; worldIndex < dataCenter.Worlds.Length; worldIndex++)
                {
                    options[cursor++] = new RegionOption(dataCenter.Worlds[worldIndex], false);
                }
            }
        }

        return options;
    }

    private void DrawFriendsScope(Rect body, bool onlineOnly, float scale)
    {
        if (friends.Count == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Contacts.Empty), ink.MutedInk);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawFriendSection(true);
            if (!onlineOnly)
            {
                DrawFriendSection(false);
            }

            if (!AnyFriendMatches(onlineOnly))
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + PeopleEmptyTop * scale),
                    Loc.T(L.Linkpearl.NoMatches), ink.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawLodestoneScope(Rect body, float scale)
    {
        var kindRow = new Rect(new Vector2(body.Min.X + CellPadX * scale, body.Min.Y),
            new Vector2(body.Max.X - CellPadX * scale, body.Min.Y + PeopleKindHeight * scale));
        findSegmentLabels[0] = Loc.T(L.FindPeople.Character);
        findSegmentLabels[1] = Loc.T(L.FindPeople.FreeCompany);
        var selected = SegmentStrip.Draw("findpeople.kind", kindRow, findSegmentLabels, (int)findKind, ui.Palette);
        if (selected >= 0 && selected != (int)findKind)
        {
            findKind = (LookupKind)selected;
            if (hasQuery)
            {
                SubmitPeopleSearch();
            }
        }

        var results = new Rect(new Vector2(body.Min.X, kindRow.Max.Y + Metrics.Space.Xxs * scale), body.Max);
        if (!hasQuery)
        {
            DrawFindPrompt(results, scale);
            return;
        }

        if (findKind == LookupKind.Character)
        {
            DrawCharacterResults(results, scale);
        }
        else
        {
            DrawFreeCompanyResults(results, scale);
        }
    }

    private void SubmitPeopleSearch()
    {
        findNameInput = peopleSearch.Trim();
        SubmitSearch();
    }

    private bool AnyFriendMatches(bool onlineOnly)
    {
        for (var index = 0; index < friends.Count; index++)
        {
            if ((!onlineOnly || friends[index].Online) && MatchesContact(friends[index]))
            {
                return true;
            }
        }

        return false;
    }
}
