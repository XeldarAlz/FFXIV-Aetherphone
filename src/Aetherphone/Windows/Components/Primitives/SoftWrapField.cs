using Aetherphone.Core;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SoftWrapField
{
    private static readonly Dictionary<string, SoftWrapEditor> Editors = new(StringComparer.Ordinal);

    public static void Multiline(string id, ref string value, int maxLength, Vector2 size, float wrapWidth,
        MentionAutocomplete? mentions = null)
    {
        Draw(id, SoftWrapLines.SingleLine, ref value, maxLength, size, wrapWidth, mentions);
    }

    public static void Paragraphs(string id, ref string value, int maxWeight, Vector2 size, float wrapWidth,
        MentionAutocomplete? mentions = null)
    {
        Draw(id, SoftWrapLines.Paragraphs, ref value, maxWeight, size, wrapWidth, mentions);
    }

    private static void Draw(string id, SoftWrapLines lines, ref string value, int maxLength, Vector2 size,
        float wrapWidth, MentionAutocomplete? mentions)
    {
        var editor = GetEditor(id, lines, mentions is not null);
        var logical = value ?? string.Empty;
        if (!string.Equals(editor.Text, logical, StringComparison.Ordinal))
        {
            editor.Adopt(logical);
        }

        editor.Rewrap(wrapWidth);
        var navigated = mentions is not null && Prepare(editor, mentions, maxLength);
        editor.Draw(id, size, maxLength, 0);
        value = editor.Text;

        if (mentions is null)
        {
            return;
        }

        Follow(editor, mentions, navigated);
    }

    private static bool Prepare(SoftWrapEditor editor, MentionAutocomplete mentions, int maxLength)
    {
        var navigated = mentions.HandleNavigation();
        if (navigated)
        {
            editor.MoveCursor(editor.Cursor);
        }

        mentions.ConsumedEscape();
        if (!mentions.TryTakeCommit(out var handle))
        {
            return navigated;
        }

        if (!MentionTokenScanner.TryFind(editor.Text, editor.Cursor, out var start, out var length))
        {
            return navigated;
        }

        var replacement = string.Concat("@", handle, " ");
        var updated = string.Concat(editor.Text.AsSpan(0, start), replacement,
            editor.Text.AsSpan(start + length));
        if (PostText.Weight(updated) > maxLength)
        {
            return navigated;
        }

        editor.Replace(updated, start + replacement.Length);
        ImGui.SetKeyboardFocusHere();
        return navigated;
    }

    private static void Follow(SoftWrapEditor editor, MentionAutocomplete mentions, bool navigated)
    {
        if (ImGui.IsItemActive())
        {
            mentions.Anchor = new Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            if (!navigated)
            {
                mentions.Track(editor.Text, editor.Cursor, ImGui.GetIO().DeltaTime);
            }
        }
        else if (!mentions.PointerOverPopup)
        {
            mentions.Close();
        }

        if (editor.CompletionRequested)
        {
            mentions.RequestCommit();
        }
    }

    private static SoftWrapEditor GetEditor(string id, SoftWrapLines lines, bool completesOnTab)
    {
        if (!Editors.TryGetValue(id, out var editor))
        {
            editor = new SoftWrapEditor(lines, completesOnTab);
            Editors[id] = editor;
        }

        return editor;
    }
}
