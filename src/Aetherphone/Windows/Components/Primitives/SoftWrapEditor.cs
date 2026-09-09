using System.Text;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum SoftWrapLines : byte
{
    SubmitOnReturn,
    BreakOnReturn,
    SingleLine,
}

internal sealed class SoftWrapEditor
{
    private const int BufferSlack = 1024;

    private readonly SoftWrapBuffer wrapped = new(SoftWrap.WrapText);
    private readonly ImGui.ImGuiInputTextCallbackPtrDelegate callback;
    private readonly SoftWrapLines lines;
    private readonly bool completesOnTab;
    private string text = string.Empty;
    private string? pendingText;
    private float wrapWidth;
    private int cursorBytes;
    private int? pendingCursor;
    private int maxCharacters;
    private int maxBytes;
    private bool pendingSync;
    private bool enterPressed;

    public SoftWrapEditor(SoftWrapLines lines = SoftWrapLines.SubmitOnReturn, bool completesOnTab = false)
    {
        this.lines = lines;
        this.completesOnTab = completesOnTab;
        callback = OnCallback;
    }

    public string Text => text;

    public int Cursor => wrapped.LogicalIndexOf(SoftWrap.CharIndexOf(wrapped.Display, cursorBytes));

    public bool Edited { get; private set; }

    public bool CompletionRequested { get; private set; }

    public int LineCount => wrapped.LineCount;

    public bool HasContent
    {
        get
        {
            for (var index = 0; index < text.Length; index++)
            {
                if (!char.IsWhiteSpace(text[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Adopt(string value)
    {
        var adopted = value ?? string.Empty;
        text = lines == SoftWrapLines.SingleLine ? SoftWrap.StripNewlines(adopted) : adopted;
        wrapped.Reset(text);
        cursorBytes = Encoding.UTF8.GetByteCount(text);
        pendingCursor = null;
        pendingSync = true;
    }

    public void MoveCursor(int logicalCursor)
    {
        pendingCursor = Math.Clamp(logicalCursor, 0, text.Length);
    }

    public void Replace(string value, int logicalCursor)
    {
        pendingText = value ?? string.Empty;
        pendingCursor = Math.Clamp(logicalCursor, 0, pendingText.Length);
    }

    public void Append(string value)
    {
        Adopt(text + value);
    }

    public void Rewrap(float width)
    {
        wrapWidth = width;
        if (wrapped.Rewrap(text, width))
        {
            pendingSync = true;
        }
    }

    public float Growth(int maxLines) =>
        (Math.Clamp(wrapped.LineCount, 1, maxLines) - 1) * ImGui.GetTextLineHeight();

    public bool Draw(string id, Vector2 size, int characterLimit, int byteLimit)
    {
        maxCharacters = characterLimit;
        maxBytes = byteLimit;
        enterPressed = false;
        Edited = false;
        CompletionRequested = false;
        var display = wrapped.Display;
        Plugin.Fonts.NoticeText(display);
        var buffer = Math.Max(byteLimit, characterLimit * 4) + BufferSlack;
        var flags = ImGuiInputTextFlags.CallbackEdit | ImGuiInputTextFlags.CallbackCharFilter |
                    ImGuiInputTextFlags.CallbackAlways;
        if (completesOnTab)
        {
            flags |= ImGuiInputTextFlags.CallbackCompletion;
        }

        ImGui.InputTextMultiline(id, ref display, buffer, size, flags, callback);
        if (!ImGui.IsItemActive())
        {
            pendingSync = false;
        }

        return enterPressed;
    }

    private int OnCallback(ImGuiInputTextCallbackDataPtr data)
    {
        if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
        {
            return FilterCharacter(data);
        }

        if (data.EventFlag == ImGuiInputTextFlags.CallbackCompletion)
        {
            CompletionRequested = true;
            return 0;
        }

        if (data.EventFlag == ImGuiInputTextFlags.CallbackAlways)
        {
            Settle(data);
            return 0;
        }

        ApplyWrap(data);
        return 0;
    }

    private void Settle(ImGuiInputTextCallbackDataPtr data)
    {
        if (pendingText is { } replacement)
        {
            pendingText = null;
            if (!string.Equals(text, replacement, StringComparison.Ordinal))
            {
                Edited = true;
            }

            text = replacement;
            wrapped.Rewrap(text, wrapWidth);
            pendingSync = false;
            WriteBuffer(data, TakeCursor());
            return;
        }

        if (pendingSync)
        {
            pendingSync = false;
            WriteBuffer(data, TakeCursor());
            return;
        }

        if (pendingCursor is not null)
        {
            PlaceCursor(data, TakeCursor());
            return;
        }

        cursorBytes = data.CursorPos;
    }

    private int TakeCursor()
    {
        var target = Math.Clamp(pendingCursor ?? text.Length, 0, text.Length);
        pendingCursor = null;
        return target;
    }

    private int FilterCharacter(ImGuiInputTextCallbackDataPtr data)
    {
        if (data.EventChar == '\r')
        {
            data.EventChar = 0;
            return 0;
        }

        if (data.EventChar != '\n')
        {
            return 0;
        }

        if (lines == SoftWrapLines.BreakOnReturn)
        {
            return 0;
        }

        if (lines == SoftWrapLines.SingleLine)
        {
            data.EventChar = 0;
            return 0;
        }

        if (ImGui.GetIO().KeyShift)
        {
            return 0;
        }

        enterPressed = true;
        data.EventChar = 0;
        return 0;
    }

    private void WriteBuffer(ImGuiInputTextCallbackDataPtr data, int logicalCursor)
    {
        var display = wrapped.Display;
        data.DeleteChars(0, data.BufTextLen);
        if (display.Length > 0)
        {
            data.InsertChars(0, display);
        }

        PlaceCursor(data, logicalCursor);
    }

    private void PlaceCursor(ImGuiInputTextCallbackDataPtr data, int logicalCursor)
    {
        cursorBytes = Encoding.UTF8.GetByteCount(
            wrapped.Display.AsSpan(0, wrapped.DisplayIndexOf(logicalCursor)));
        data.CursorPos = cursorBytes;
        data.SelectionStart = cursorBytes;
        data.SelectionEnd = cursorBytes;
    }

    private void ApplyWrap(ImGuiInputTextCallbackDataPtr data)
    {
        var current = Encoding.UTF8.GetString(data.BufSpan[..data.BufTextLen]);
        var charCursor = SoftWrap.CharIndexOf(current, data.CursorPos);
        var logical = wrapped.Merge(text, current, charCursor, out var logicalCursor);
        logical = Cap(logical, ref logicalCursor);
        if (!string.Equals(text, logical, StringComparison.Ordinal))
        {
            Edited = true;
        }

        text = logical;
        wrapped.Rewrap(logical, wrapWidth);
        if (string.Equals(wrapped.Display, current, StringComparison.Ordinal))
        {
            cursorBytes = data.CursorPos;
            return;
        }

        WriteBuffer(data, logicalCursor);
    }

    private string Cap(string value, ref int cursor)
    {
        var capped = maxCharacters > 0 ? CapCharacters(value, maxCharacters, ref cursor) : value;
        return maxBytes > 0 ? CapBytes(capped, maxBytes, ref cursor) : capped;
    }

    private static string CapCharacters(string value, int limit, ref int cursor)
    {
        if (value.Length <= limit)
        {
            return value;
        }

        var end = limit;
        if (end > 0 && char.IsHighSurrogate(value[end - 1]))
        {
            end--;
        }

        if (cursor > end)
        {
            cursor = end;
        }

        return value[..end];
    }

    private static string CapBytes(string value, int limit, ref int cursor)
    {
        if (Encoding.UTF8.GetByteCount(value) <= limit)
        {
            return value;
        }

        var bytes = 0;
        var index = 0;
        while (index < value.Length)
        {
            var runeLength = SoftWrap.RuneLength(value, index);
            var runeBytes = Encoding.UTF8.GetByteCount(value.AsSpan(index, runeLength));
            if (bytes + runeBytes > limit)
            {
                break;
            }

            bytes += runeBytes;
            index += runeLength;
        }

        if (cursor > index)
        {
            cursor = index;
        }

        return value[..index];
    }
}
