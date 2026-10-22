using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class UserListSearch
{
    public const int MaxLength = 40;

    private const double DebounceSeconds = 0.3;
    private const float NotePadX = 24f;
    private const float NotePadY = 8f;

    private static readonly TextStyle NoteStyle = TextStyles.Footnote;

    private readonly List<UserDto> matches = new();
    private string draft = string.Empty;
    private string seenDraft = string.Empty;
    private string pending = string.Empty;
    private string query = string.Empty;
    private string handleQuery = string.Empty;
    private double dirtyAt;
    private string? sourceId;
    private UserListKind kind;
    private UserDto[]? matchedSource;
    private string matchedQuery = string.Empty;
    private string? cappedTemplate;
    private string cappedLabel = string.Empty;

    public ref string Draft => ref draft;

    public bool Active => query.Length > 0;

    public List<UserDto> Matches => matches;

    public static bool Supports(UserListKind kind) => kind != UserListKind.Likers;

    public void Update(SocialFeedStore store, string listSourceId, UserListKind listKind, double now)
    {
        if (!string.Equals(sourceId, listSourceId, StringComparison.Ordinal) || kind != listKind)
        {
            Reset(listSourceId, listKind);
        }

        ApplyDraft(now);
        if (!Active)
        {
            return;
        }

        var snapshot = store.UserListResults;
        if (!ReferenceEquals(snapshot, matchedSource) || !string.Equals(query, matchedQuery, StringComparison.Ordinal))
        {
            Rebuild(snapshot);
        }

        store.FillUserListForSearch();
    }

    public static string EmptyMessage(SocialFeedStore store) =>
        store.UserListLoading || store.UserListSearchFilling
            ? Loc.T(L.Common.Searching)
            : Loc.T(L.Social.ListSearchNoMatches);

    public string CappedNote(SocialFeedStore store)
    {
        if (!store.UserListSearchCapped)
        {
            return string.Empty;
        }

        var template = Loc.T(L.Social.ListSearchCapped);
        if (!ReferenceEquals(template, cappedTemplate))
        {
            cappedTemplate = template;
            cappedLabel = Loc.T(L.Social.ListSearchCapped, store.UserListSearchLimit);
        }

        return cappedLabel;
    }

    public static void DrawNote(string text, Vector4 ink)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var maxWidth = MathF.Max(1f, width - NotePadX * 2f * scale);
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y + NotePadY * scale),
            text, ink, NoteStyle, maxWidth);
        ImGui.Dummy(new Vector2(width, height + NotePadY * 2f * scale));
    }

    private void Reset(string listSourceId, UserListKind listKind)
    {
        sourceId = listSourceId;
        kind = listKind;
        draft = string.Empty;
        seenDraft = string.Empty;
        pending = string.Empty;
        query = string.Empty;
        handleQuery = string.Empty;
        matchedSource = null;
        matchedQuery = string.Empty;
        matches.Clear();
    }

    private void ApplyDraft(double now)
    {
        if (!ReferenceEquals(draft, seenDraft))
        {
            seenDraft = draft;
            var trimmed = draft.Trim();
            if (trimmed.Length == 0)
            {
                pending = string.Empty;
                query = string.Empty;
                handleQuery = string.Empty;
                matches.Clear();
                matchedSource = null;
            }
            else if (!string.Equals(trimmed, pending, StringComparison.Ordinal))
            {
                pending = trimmed;
                dirtyAt = now;
            }
        }

        if (pending.Length == 0 || string.Equals(pending, query, StringComparison.Ordinal)
            || now - dirtyAt < DebounceSeconds)
        {
            return;
        }

        query = pending;
        handleQuery = query.Length > 1 && query[0] == '@' ? query[1..] : query;
    }

    private void Rebuild(UserDto[] snapshot)
    {
        matchedSource = snapshot;
        matchedQuery = query;
        matches.Clear();
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (IsMatch(snapshot[index]))
            {
                matches.Add(snapshot[index]);
            }
        }
    }

    private bool IsMatch(UserDto user) =>
        user.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || user.Handle.Contains(handleQuery, StringComparison.OrdinalIgnoreCase)
        || user.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
}
