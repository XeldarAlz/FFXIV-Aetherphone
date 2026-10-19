using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float PeopleListEmptyTop = 60f;
    private const float PeopleSearchHeight = 52f;

    private readonly UserListSearch userListSearch = new();

    private void DrawUserList(Rect area, string sourceId, UserListKind kind)
    {
        store.EnsureUserList(sourceId, kind);
        DrawScreenHeader(area, SocialProfilePages.UserListTitle(kind), 0, true, true,
            SocialProfilePages.UserListCount(store));
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchable = UserListSearch.Supports(kind);
        if (searchable)
        {
            var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, top),
                new Vector2(area.Max.X - CellPadX * scale, top + PeopleSearchHeight * scale));
            SearchField.Draw(searchRect, "##aethergramUserListSearch", Loc.T(L.Social.ListSearchHint),
                ref userListSearch.Draft, AethergramInk.CurrentPalette, UserListSearch.MaxLength);
            userListSearch.Update(store, sourceId, kind, ImGui.GetTime());
            top = searchRect.Max.Y + 4f * scale;
        }

        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (searchable && userListSearch.Active)
        {
            DrawUserListMatches(listRect);
            return;
        }

        var snapshot = store.UserListResults;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.UserListLoading ? Loc.T(L.Common.Loading)
                    : store.UserListFailed ? Loc.T(L.Aethergram.ProfileError)
                    : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + PeopleListEmptyTop * scale),
                    message, Ink.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawUserRowWithFollow(snapshot[index]);
            }

            if (store.UserListLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
            }
            else if (store.HasMoreUserList && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreUserList();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawUserListMatches(Rect listRect)
    {
        var scale = UiScale.Current;
        var matches = userListSearch.Matches;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (matches.Count == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + PeopleListEmptyTop * scale),
                    UserListSearch.EmptyMessage(store), Ink.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < matches.Count; index++)
            {
                DrawUserRowWithFollow(matches[index]);
            }

            if (store.UserListSearchFilling)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
            }

            var cappedNote = userListSearch.CappedNote(store);
            if (cappedNote.Length > 0)
            {
                UserListSearch.DrawNote(cappedNote, Ink.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }
}
