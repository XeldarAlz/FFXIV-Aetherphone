using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float PeopleListEmptyTop = 60f;

    private void DrawUserList(Rect area, string sourceId, UserListKind kind)
    {
        store.EnsureUserList(sourceId, kind);
        DrawScreenHeader(area, SocialProfilePages.UserListTitle(kind));
        var scale = UiScale.Current;
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
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
}
