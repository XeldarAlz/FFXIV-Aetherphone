using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private enum PeopleScope : byte
    {
        Friends,
        Online,
        Lodestone,
    }

    private const float PeopleSearchHeight = 52f;
    private const float PeopleChipsHeight = 44f;
    private const float PeopleSearchRevealSeconds = 0.16f;
    private const float PeopleEmptyTop = 60f;

    private readonly ChipRail peopleRail = new();
    private readonly string[] peopleScopeLabels = new string[3];
    private readonly bool[] peopleScopeActive = new bool[3];
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
        var lodestoneScope = peopleScope == PeopleScope.Lodestone;
        var top = DrawPeopleSearchRow(content, scale, lodestoneScope);
        var railTop = top + (PeopleChipsHeight - ChipRail.RowHeight) * 0.5f * scale;
        var rail = new Rect(new Vector2(content.Min.X + CellPadX * scale, railTop),
            new Vector2(content.Max.X - CellPadX * scale, railTop + ChipRail.RowHeight * scale));
        UiAnchors.Report("people.scope", rail);
        peopleScopeLabels[0] = Loc.T(L.Linkpearl.ScopeFriends);
        peopleScopeLabels[1] = Loc.T(L.Contacts.Online);
        peopleScopeLabels[2] = Loc.T(L.Linkpearl.ScopeLodestone);
        for (var index = 0; index < peopleScopeActive.Length; index++)
        {
            peopleScopeActive[index] = (int)peopleScope == index;
        }

        var tapped = peopleRail.Draw(rail, ui, peopleScopeLabels, peopleScopeActive, false, "people.scope",
            ChipRail.CompactLabelPadding);
        if (tapped >= 0 && tapped != (int)peopleScope)
        {
            peopleScope = (PeopleScope)tapped;
            if (peopleScope == PeopleScope.Lodestone)
            {
                peopleSearchFocus = true;
                if (peopleSearch.Trim().Length > 0)
                {
                    SubmitPeopleSearch();
                }
            }
        }

        top += PeopleChipsHeight * scale;
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        if (lodestoneScope)
        {
            DrawLodestoneScope(body, scale);
            return;
        }

        DrawFriendsScope(body, peopleScope == PeopleScope.Online, scale);
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
        UiAnchors.Report("people.search", bar);
        if (forced)
        {
            if (peopleSearchFocus)
            {
                ImGui.SetKeyboardFocusHere();
            }

            if (SearchField.DrawSubmit(bar, "##peopleSearch", Loc.T(L.FindPeople.NameHint), ref peopleSearch,
                    ui.Palette))
            {
                SubmitPeopleSearch();
            }
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

    private void DrawFriendsScope(Rect body, bool onlineOnly, float scale)
    {
        UiAnchors.Report("people.list", body);
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
        var kindTop = body.Min.Y + Metrics.Space.Xs * scale;
        var kindRow = new Rect(new Vector2(body.Min.X + CellPadX * scale, kindTop),
            new Vector2(body.Max.X - CellPadX * scale, kindTop + ChipRail.RowHeight * scale));
        UiAnchors.Report("findpeople.kind", kindRow);
        findSegmentLabels[0] = Loc.T(L.FindPeople.Character);
        findSegmentLabels[1] = Loc.T(L.FindPeople.FreeCompany);
        findSegmentActive[0] = findKind == LookupKind.Character;
        findSegmentActive[1] = findKind == LookupKind.FreeCompany;
        var selected = findRail.Draw(kindRow, ui, findSegmentLabels, findSegmentActive, false, "findpeople.kind",
            ChipRail.CompactLabelPadding);
        if (selected >= 0 && selected != (int)findKind)
        {
            findKind = (LookupKind)selected;
            if (hasQuery)
            {
                SubmitPeopleSearch();
            }
        }

        var worldTop = kindRow.Max.Y + Metrics.Space.Sm * scale;
        var worldBar = new Rect(new Vector2(body.Min.X + CellPadX * scale, worldTop),
            new Vector2(body.Max.X - CellPadX * scale, worldTop + FindFieldRowHeight * scale));
        UiAnchors.Report("findpeople.name", worldBar);
        if (SubmitField.Draw(worldBar, "##peopleWorldField", Loc.T(L.FindPeople.WorldHint), ref findWorldInput,
                frameTheme))
        {
            SubmitPeopleSearch();
        }

        var results = new Rect(new Vector2(body.Min.X, worldBar.Max.Y + Metrics.Space.Sm * scale), body.Max);
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
