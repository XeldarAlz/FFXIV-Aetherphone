using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

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

internal sealed partial class AethergramApp
{
    private const float CannotMessageWidth = 92f;
    private const float DisabledRowVeil = 0.45f;

    private static readonly TextStyle CannotMessageStyle = TextStyles.Footnote;

    private string newMessageDraft = string.Empty;
    private DmSearchDebounce newMessageSearch = new();

    private void OpenNewMessage()
    {
        newMessageDraft = string.Empty;
        newMessageSearch.Reset();
        store.ClearDiscover();
        router.Push(AethergramRoute.NewMessage);
    }

    private void DrawNewMessage(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Aethergram.NewMessage));
        var searchTop = area.Min.Y + AppHeader.Height * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, searchTop),
            new Vector2(area.Max.X - CellPadX * scale, searchTop + InboxSearchHeight * scale));
        SearchField.Draw(searchRect, "##aethergramNewMessage", Loc.T(L.Aethergram.NewMessageHint),
            ref newMessageDraft, AppPalettes.Aethergram);
        RunDmSearch(ref newMessageSearch, newMessageDraft);
        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y + 4f * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var query = newMessageDraft.Trim();
            var results = store.DiscoverResults;
            if (query.Length == 0)
            {
                DrawEmptyState(listRect, Loc.T(L.Aethergram.NewMessageEmpty), string.Empty);
                return;
            }

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

                DrawNewMessageRow(results[index]);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
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

    private void DrawNewMessageRow(UserDto user)
    {
        var row = DrawUserRow(user, user.CanMessage ? 0f : CannotMessageWidth);
        if (!user.CanMessage)
        {
            var drawList = ImGui.GetWindowDrawList();
            var label = Typography.FitText(Loc.T(L.Aethergram.CannotMessage), row.Trailing.Width, CannotMessageStyle);
            var size = Typography.Measure(label, CannotMessageStyle);
            Typography.Draw(drawList, new Vector2(row.Trailing.Max.X - size.X, row.Trailing.Center.Y - size.Y * 0.5f),
                label, Ink.MutedInk, CannotMessageStyle);
            DimRow(drawList, row.Bounds);
            return;
        }

        if (!row.Tapped)
        {
            return;
        }

        router.Replace(AethergramRoute.Thread(user.Id));
    }

    private static void DimRow(ImDrawListPtr drawList, Rect bounds) =>
        drawList.AddRectFilled(bounds.Min, bounds.Max,
            ImGui.GetColorU32(Palette.WithAlpha(Ink.BackdropTop, DisabledRowVeil)));
}
