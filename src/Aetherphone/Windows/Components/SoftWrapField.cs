using System.Collections.Generic;
using System.Numerics;
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
        public readonly ImGui.ImGuiInputTextCallbackPtrDelegate Callback;

        public Editor(int maxLength)
        {
            MaxLength = maxLength;
            Callback = data => SoftWrap.ApplyEdit(data, WrapWidth, MaxLength);
        }
    }

    private static readonly Dictionary<string, Editor> Editors = new(StringComparer.Ordinal);

    public static void Multiline(string id, ref string value, int maxLength, Vector2 size, float wrapWidth)
    {
        var editor = GetEditor(id, maxLength);
        editor.WrapWidth = wrapWidth;
        editor.MaxLength = maxLength;

        var logical = value ?? string.Empty;
        if (!string.Equals(editor.Logical, logical, StringComparison.Ordinal))
        {
            editor.Logical = logical;
            editor.Display = SoftWrap.WrapText(logical, wrapWidth);
        }

        var bufferBytes = maxLength * 4 + 1024;
        var edited = ImGui.InputTextMultiline(id, ref editor.Display, bufferBytes, size,
            ImGuiInputTextFlags.CallbackEdit | ImGuiInputTextFlags.CallbackCharFilter, editor.Callback);
        if (edited)
        {
            editor.Logical = SoftWrap.StripNewlines(editor.Display);
        }

        value = editor.Logical;
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
