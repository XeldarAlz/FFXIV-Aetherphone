using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

// Shared brain behind the @ autocomplete. Both the single line and multi line fields drive one of these,
// so token tracking, debouncing and keyboard navigation exist once. The field owns the text; this owns
// the decision of whether a popup is open and which row is selected.
internal sealed class MentionAutocomplete
{
    private const float DebounceSeconds = 0.20f;

    private readonly MentionSuggestions suggestions;
    private string pending = string.Empty;
    private string applied = string.Empty;
    private float debounce;
    private bool tokenLive;
    private string? commitHandle;

    public MentionAutocomplete(MentionSuggestions suggestions)
    {
        this.suggestions = suggestions;
    }

    public int SelectedIndex { get; private set; }

    public Rect Anchor { get; set; }

    public MentionSuggestDto[] Rows => suggestions.Results;

    public bool IsOpen => tokenLive && (suggestions.Results.Length > 0 || suggestions.Loading);

    public void Track(string logical, int logicalCursor, float deltaSeconds)
    {
        if (!MentionTokenScanner.TryFind(logical, logicalCursor, out var start, out var length))
        {
            Close();
            return;
        }

        var query = MentionTokenScanner.QueryOf(logical, start, length);
        if (query.Length < 1)
        {
            Close();
            return;
        }

        tokenLive = true;
        if (!string.Equals(query, pending, StringComparison.Ordinal))
        {
            pending = query;
            debounce = 0f;
            SelectedIndex = 0;
        }

        if (string.Equals(pending, applied, StringComparison.Ordinal))
        {
            return;
        }

        debounce += deltaSeconds;
        if (debounce < DebounceSeconds)
        {
            return;
        }

        applied = pending;
        suggestions.Request(pending);
    }

    public bool HandleNavigation()
    {
        if (!IsOpen)
        {
            return false;
        }

        var rows = suggestions.Results.Length;
        if (rows == 0)
        {
            return false;
        }

        if (SelectedIndex >= rows)
        {
            SelectedIndex = rows - 1;
        }

        var moved = false;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow, true))
        {
            SelectedIndex = (SelectedIndex + 1) % rows;
            moved = true;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true))
        {
            SelectedIndex = (SelectedIndex + rows - 1) % rows;
            moved = true;
        }

        return moved;
    }

    public bool ConsumedEscape()
    {
        if (!IsOpen || !ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            return false;
        }

        Close();
        return true;
    }

    public bool RequestCommit()
    {
        if (!IsOpen)
        {
            return false;
        }

        var rows = suggestions.Results;
        if (SelectedIndex < 0 || SelectedIndex >= rows.Length)
        {
            return false;
        }

        return Pick(SelectedIndex);
    }

    public bool Pick(int index)
    {
        var rows = suggestions.Results;
        if (index < 0 || index >= rows.Length)
        {
            return false;
        }

        commitHandle = rows[index].Handle;
        Close();
        return true;
    }

    public bool TryTakeCommit(out string handle)
    {
        handle = commitHandle ?? string.Empty;
        if (commitHandle is null)
        {
            return false;
        }

        commitHandle = null;
        return true;
    }

    public void Close()
    {
        tokenLive = false;
        pending = string.Empty;
        applied = string.Empty;
        debounce = 0f;
        SelectedIndex = 0;
        suggestions.Clear();
    }
}
