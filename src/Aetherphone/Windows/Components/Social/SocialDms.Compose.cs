using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal struct DmSearchDebounce
{
    private const double DelaySeconds = 0.35;

    private string applied;
    private string pending;
    private double dirtyAt;

    public DmSearchDebounce()
    {
        applied = string.Empty;
        pending = string.Empty;
        dirtyAt = 0d;
    }

    public void Reset()
    {
        applied = string.Empty;
        pending = string.Empty;
        dirtyAt = 0d;
    }

    public bool Due(string draft, double now)
    {
        if (!string.Equals(draft, pending, StringComparison.Ordinal))
        {
            pending = draft;
            dirtyAt = now;
            return false;
        }

        if (string.Equals(draft, applied, StringComparison.Ordinal) || now - dirtyAt < DelaySeconds)
        {
            return false;
        }

        applied = draft;
        return true;
    }
}

internal sealed partial class SocialDms
{
    private const float CannotMessageWidth = 92f;
    private const float SharePillWidth = 76f;

    private static readonly TextStyle CannotMessageStyle = TextStyles.Footnote;

    private readonly HashSet<string> shareSentUserIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UserDto> shareThreadUsers = new(StringComparer.Ordinal);
    private DmThreadDto[] shareThreadSource = Array.Empty<DmThreadDto>();
    private DmSearchDebounce shareSearch = new();
    private string shareSearchDraft = string.Empty;
    private string newMessageDraft = string.Empty;
    private DmSearchDebounce newMessageSearch = new();

    private void OpenNewMessage()
    {
        newMessageDraft = string.Empty;
        newMessageSearch.Reset();
        store.ClearDiscover();
        host.PushDm(SocialDmScreen.NewMessage);
    }

    public void OpenShare(string postId)
    {
        shareSentUserIds.Clear();
        shareSearchDraft = string.Empty;
        shareSearch.Reset();
        store.ClearDiscover();
        host.PushDm(SocialDmScreen.Share, postId);
    }

    private Rect DrawSearchScreenTop(Rect area, string title, string id, string hint, ref string draft,
        ref DmSearchDebounce debounce)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, title);
        var searchTop = area.Min.Y + AppHeader.Height * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, searchTop),
            new Vector2(area.Max.X - CellPadX * scale, searchTop + InboxSearchHeight * scale));
        SearchField.Draw(searchRect, id, hint, ref draft, host.SearchPalette);
        RunDmSearch(ref debounce, draft);
        return new Rect(new Vector2(area.Min.X, searchRect.Max.Y + 4f * scale), area.Max);
    }

    private void DrawNewMessage(Rect area)
    {
        var listRect = DrawSearchScreenTop(area, Loc.T(labels.NewMessage), "##dmNewMessage",
            Loc.T(labels.NewMessageHint), ref newMessageDraft, ref newMessageSearch);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (newMessageDraft.Trim().Length == 0)
            {
                DrawEmptyState(listRect, Loc.T(labels.NewMessageEmpty), string.Empty);
                return;
            }

            DrawSearchResults(listRect, null);
        }
    }

    private void DrawShare(Rect area, string postId)
    {
        if (!dm.ThreadsLoaded && !dm.LoadingThreads)
        {
            dm.RefreshThreads();
        }

        var listRect = DrawSearchScreenTop(area, Loc.T(labels.SendTo), "##dmShareSearch", Loc.T(labels.SearchHint),
            ref shareSearchDraft, ref shareSearch);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (shareSearchDraft.Trim().Length > 0)
            {
                DrawSearchResults(listRect, postId);
            }
            else
            {
                DrawShareThreads(listRect, postId);
            }
        }
    }

    private void RunDmSearch(ref DmSearchDebounce debounce, string draft)
    {
        var query = draft.Trim();
        if (!debounce.Due(query, ImGui.GetTime()))
        {
            return;
        }

        if (query.Length == 0)
        {
            store.ClearDiscover();
            return;
        }

        store.Search(query);
    }

    private void DrawSearchResults(Rect listRect, string? postId)
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

            if (postId is null)
            {
                DrawNewMessageRow(results[index]);
            }
            else
            {
                DrawShareRow(postId, results[index]);
            }
        }

        ImGui.Dummy(new Vector2(0f, 24f * UiScale.Current));
    }

    private void DrawNewMessageRow(UserDto user)
    {
        var row = DrawUserRow(user, user.CanMessage ? 0f : CannotMessageWidth);
        if (!user.CanMessage)
        {
            var drawList = ImGui.GetWindowDrawList();
            var label = Typography.FitText(Loc.T(labels.CannotMessage), row.Trailing.Width, CannotMessageStyle);
            var size = Typography.Measure(label, CannotMessageStyle);
            Typography.Draw(drawList, new Vector2(row.Trailing.Max.X - size.X, row.Trailing.Center.Y - size.Y * 0.5f),
                label, Ink.MutedInk, CannotMessageStyle);
            DimRow(drawList, row.Bounds);
            return;
        }

        if (row.Tapped)
        {
            host.ReplaceDm(SocialDmScreen.Thread, user.Id);
        }
    }

    private void DrawShareThreads(Rect listRect, string postId)
    {
        var threads = dm.Threads;
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
            DrawEmptyState(listRect, Loc.T(dm.LoadingThreads ? L.Common.Loading : labels.InboxEmpty),
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

        ImGui.Dummy(new Vector2(0f, 24f * UiScale.Current));
    }

    private void RefreshShareThreadUsers(DmThreadDto[] threads)
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
        ImGui.PushID(user.Id);
        DrawShareAction(postId, user, row);
        ImGui.PopID();
    }

    private void DrawShareAction(string postId, UserDto user, SocialUserRowResult row)
    {
        if (shareSentUserIds.Contains(user.Id))
        {
            DrawGrayPill(row.Trailing, Loc.T(labels.Sent));
            return;
        }

        if (!user.CanMessage)
        {
            DrawAccentPill(row.Trailing, Loc.T(labels.Send), false);
            DimRow(ImGui.GetWindowDrawList(), row.Bounds);
            return;
        }

        if (DrawAccentPill(row.Trailing, Loc.T(labels.Send)) || row.Tapped)
        {
            dm.SendPostShare(user.Id, postId);
            store.ReportFeedSignal(postId, FeedSignalKinds.Send);
            shareSentUserIds.Add(user.Id);
        }
    }
}
