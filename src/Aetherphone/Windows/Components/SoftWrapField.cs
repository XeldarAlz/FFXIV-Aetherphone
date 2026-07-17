using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

// Drop-in replacement for ImGui.InputTextMultiline that soft-wraps long lines. Callers keep their own
// plain string (the logical value, never containing line breaks); this helper owns the wrapped display
// buffer and the edit callback per field id, so every multi-line composer wraps identically without
// duplicating the wrap bookkeeping. Call it while the font that renders the field is pushed so the
// wrap measurements match what the user sees.
internal static class SoftWrapField
{
    private sealed class Editor
    {
        public string Display = string.Empty;
        public string Logical = string.Empty;
        public float WrapWidth;
        public int MaxLength;
        public string? PendingHandle;
        public int LogicalCursor;
        public int? RestoreCursor;
        public bool TabRequested;
        public readonly ImGui.ImGuiInputTextCallbackPtrDelegate Callback;

        public Editor(int maxLength)
        {
            MaxLength = maxLength;
            Callback = Apply;
        }

        private int Apply(ImGuiInputTextCallbackDataPtr data)
        {
            if (data.EventFlag == ImGuiInputTextFlags.CallbackCompletion)
            {
                TabRequested = true;
                return 0;
            }

            if (data.EventFlag != ImGuiInputTextFlags.CallbackAlways)
            {
                return SoftWrap.ApplyEdit(data, WrapWidth, MaxLength);
            }

            SoftWrap.ReadLogical(data, out var display, out var logical, out var cursor);
            if (RestoreCursor is { } target)
            {
                RestoreCursor = null;
                LogicalCursor = target;
                SoftWrap.SetCursor(data, display, target);
                return 0;
            }

            if (PendingHandle is null)
            {
                LogicalCursor = cursor;
                return 0;
            }

            var handle = PendingHandle;
            PendingHandle = null;
            if (!MentionTokenScanner.TryFind(logical, LogicalCursor, out var start, out var length))
            {
                return 0;
            }

            var replacement = "@" + handle + " ";
            var updated = string.Concat(logical.AsSpan(0, start), replacement, logical.AsSpan(start + length));
            if (updated.Length > MaxLength)
            {
                return 0;
            }

            LogicalCursor = start + replacement.Length;
            SoftWrap.WriteLogical(data, updated, LogicalCursor, WrapWidth);
            Logical = updated;
            Display = SoftWrap.WrapText(updated, WrapWidth);
            return 0;
        }
    }

    private static readonly Dictionary<string, Editor> Editors = new(StringComparer.Ordinal);

    public static void Multiline(string id, ref string value, int maxLength, Vector2 size, float wrapWidth,
        MentionAutocomplete? mentions = null)
    {
        var editor = GetEditor(id, maxLength);
        editor.WrapWidth = wrapWidth;
        editor.MaxLength = maxLength;
        editor.TabRequested = false;

        var logical = value ?? string.Empty;
        if (!string.Equals(editor.Logical, logical, StringComparison.Ordinal))
        {
            editor.Logical = logical;
            editor.Display = SoftWrap.WrapText(logical, wrapWidth);
        }

        var navigated = false;
        if (mentions is not null)
        {
            navigated = mentions.HandleNavigation();
            if (navigated)
            {
                editor.RestoreCursor = editor.LogicalCursor;
            }

            mentions.ConsumedEscape();
            if (mentions.TryTakeCommit(out var handle))
            {
                editor.PendingHandle = handle;
            }
        }

        Plugin.Fonts.NoticeText(editor.Display);
        var bufferBytes = maxLength * 4 + 1024;
        var flags = ImGuiInputTextFlags.CallbackEdit | ImGuiInputTextFlags.CallbackCharFilter;
        if (mentions is not null)
        {
            flags |= ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackCompletion;
        }

        if (editor.PendingHandle is not null)
        {
            ImGui.SetKeyboardFocusHere();
        }

        var edited = ImGui.InputTextMultiline(id, ref editor.Display, bufferBytes, size, flags, editor.Callback);
        if (edited)
        {
            editor.Logical = SoftWrap.StripNewlines(editor.Display);
        }

        value = editor.Logical;

        if (mentions is null)
        {
            return;
        }

        if (ImGui.IsItemActive())
        {
            mentions.Anchor = new Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            if (!navigated)
            {
                mentions.Track(editor.Logical, editor.LogicalCursor, ImGui.GetIO().DeltaTime);
            }
        }
        else if (!mentions.PointerOver)
        {
            mentions.Close();
        }

        if (editor.TabRequested)
        {
            mentions.RequestCommit();
        }
    }

    private static Editor GetEditor(string id, int maxLength)
    {
        if (!Editors.TryGetValue(id, out var editor))
        {
            editor = new Editor(maxLength);
            Editors[id] = editor;
        }

        return editor;
    }
}
