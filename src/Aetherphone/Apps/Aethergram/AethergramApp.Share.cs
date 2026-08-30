using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float SharePillWidth = 76f;

    private readonly Dictionary<string, UserDto> shareThreadUsers = new(StringComparer.Ordinal);
    private GramThreadDto[] shareThreadSource = Array.Empty<GramThreadDto>();
    private DmSearchDebounce shareSearch = new();

    private void OpenShare(string postId)
    {
        shareSentUserIds.Clear();
        shareSearchDraft = string.Empty;
        shareSearch.Reset();
        store.ClearDiscover();
        router.Push(AethergramRoute.Share(postId));
    }

    private void DrawShare(Rect area, string postId)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Aethergram.SendTo));
        if (!dmStore.ThreadsLoaded && !dmStore.LoadingThreads)
        {
            dmStore.RefreshThreads();
        }

        var searchTop = area.Min.Y + AppHeader.Height * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, searchTop),
            new Vector2(area.Max.X - CellPadX * scale, searchTop + InboxSearchHeight * scale));
        SearchField.Draw(searchRect, "##aethergramShareSearch", Loc.T(L.Aethergram.NameOrWorld),
            ref shareSearchDraft, AppPalettes.Aethergram);
        RunDmSearch(ref shareSearch, shareSearchDraft);
        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y + 4f * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (shareSearchDraft.Trim().Length > 0)
            {
                DrawShareSearchResults(listRect, postId, scale);
            }
            else
            {
                DrawShareThreads(listRect, postId, scale);
            }
        }
    }

    private void DrawShareSearchResults(Rect listRect, string postId, float scale)
    {
        var results = store.DiscoverResults;
        if (results.Length == 0)
        {
            DrawEmptyState(listRect, Loc.T(store.Searching ? L.Common.Searching : L.Social.ListEmpty),
                string.Empty);
            return;
        }

        var myId = store.Me?.Id;
        for (var index = 0; index < results.Length; index++)
        {
            if (string.Equals(results[index].Id, myId, StringComparison.Ordinal))
            {
                continue;
            }

            DrawShareRow(postId, results[index]);
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawShareThreads(Rect listRect, string postId, float scale)
    {
        var threads = dmStore.Threads;
        RefreshShareThreadUsers(threads);
        var visibleCount = 0;
        for (var index = 0; index < threads.Length; index++)
        {
            if (!threads[index].Pending)
            {
                visibleCount++;
            }
        }

        if (visibleCount == 0)
        {
            DrawEmptyState(listRect, Loc.T(dmStore.LoadingThreads ? L.Common.Loading : L.Aethergram.InboxEmpty),
                string.Empty);
            return;
        }

        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            if (thread.Pending || !shareThreadUsers.TryGetValue(thread.OtherUserId, out var user))
            {
                continue;
            }

            DrawShareRow(postId, user);
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void RefreshShareThreadUsers(GramThreadDto[] threads)
    {
        if (ReferenceEquals(threads, shareThreadSource))
        {
            return;
        }

        shareThreadSource = threads;
        shareThreadUsers.Clear();
        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            shareThreadUsers[thread.OtherUserId] = new UserDto(thread.OtherUserId, string.Empty, string.Empty,
                thread.OtherDisplayName, thread.OtherHandle, string.Empty, 0, 0, 0, false, false,
                thread.OtherAvatarUrl, 0, thread.UtcOffsetMinutes, CanMessage: true);
        }
    }

    private void DrawShareRow(string postId, UserDto user)
    {
        var row = DrawUserRow(user, SharePillWidth);
        var sent = shareSentUserIds.Contains(user.Id);
        if (sent)
        {
            DrawGrayPill(row.Trailing, Loc.T(L.Aethergram.Sent));
            return;
        }

        if (!user.CanMessage)
        {
            DrawAccentPill(row.Trailing, Loc.T(L.Aethergram.Send), false);
            DimRow(ImGui.GetWindowDrawList(), row.Bounds);
            return;
        }

        if (DrawAccentPill(row.Trailing, Loc.T(L.Aethergram.Send)) || row.Tapped)
        {
            dmStore.SendPostShare(user.Id, postId);
            shareSentUserIds.Add(user.Id);
        }
    }
}
