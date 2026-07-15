using System.Text;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class MentionField
{
    private sealed class Editor
    {
        public string? PendingHandle;
        public int MaxLength;
        public int Cursor;
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
                return 0;
            }

            var current = Encoding.UTF8.GetString(data.BufSpan[..data.BufTextLen]);
            if (PendingHandle is null)
            {
                Cursor = ByteIndexToCharIndex(current, data.CursorPos);
                return 0;
            }

            var handle = PendingHandle;
            PendingHandle = null;
            if (!MentionTokenScanner.TryFind(current, Cursor, out var start, out var length))
            {
                return 0;
            }

            var replacement = "@" + handle + " ";
            var updated = string.Concat(current.AsSpan(0, start), replacement, current.AsSpan(start + length));
            if (updated.Length > MaxLength)
            {
                return 0;
            }

            var cursor = start + replacement.Length;
            data.DeleteChars(0, data.BufTextLen);
            data.InsertChars(0, updated);
            var byteCursor = Encoding.UTF8.GetByteCount(updated.AsSpan(0, cursor));
            data.CursorPos = byteCursor;
            data.SelectionStart = byteCursor;
            data.SelectionEnd = byteCursor;
            Cursor = cursor;
            return 0;
        }

        private static int ByteIndexToCharIndex(string text, int byteIndex)
        {
            if (byteIndex <= 0)
            {
                return 0;
            }

            var bytes = 0;
            for (var index = 0; index < text.Length; index++)
            {
                if (bytes >= byteIndex)
                {
                    return index;
                }

                bytes += Encoding.UTF8.GetByteCount(text.AsSpan(index, 1));
            }

            return text.Length;
        }
    }

    private static readonly Dictionary<string, Editor> Editors = new(StringComparer.Ordinal);

    public static bool SingleLineWithHint(string id, string hint, ref string value, int maxLength,
        MentionAutocomplete? mentions)
    {
        if (mentions is null)
        {
            return ImGui.InputTextWithHint(id, hint, ref value, maxLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var editor = GetEditor(id, maxLength);
        editor.MaxLength = maxLength;
        editor.TabRequested = false;

        var navigated = mentions.HandleNavigation();
        var escaped = mentions.ConsumedEscape();
        if (mentions.TryTakeCommit(out var handle))
        {
            editor.PendingHandle = handle;
        }

        if (editor.PendingHandle is not null)
        {
            ImGui.SetKeyboardFocusHere();
        }

        const ImGuiInputTextFlags flags = ImGuiInputTextFlags.EnterReturnsTrue
            | ImGuiInputTextFlags.CallbackAlways
            | ImGuiInputTextFlags.CallbackCompletion
            | ImGuiInputTextFlags.CallbackHistory;
        var submitted = ImGui.InputTextWithHint(id, hint, ref value, maxLength, flags, editor.Callback);

        if (ImGui.IsItemActive())
        {
            mentions.Anchor = new Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            mentions.Track(value, editor.Cursor, ImGui.GetIO().DeltaTime);
        }
        else if (!mentions.PointerOver)
        {
            mentions.Close();
        }

        if (editor.TabRequested && mentions.RequestCommit())
        {
            return false;
        }

        if (escaped || navigated)
        {
            return false;
        }

        if (submitted && mentions.IsOpen)
        {
            mentions.RequestCommit();
            return false;
        }

        return submitted;
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
