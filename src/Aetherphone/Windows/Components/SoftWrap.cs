using System.Text;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SoftWrap
{
    private const char SoftWrapSentinel = '\u200B';

    public static int ApplyEdit(ImGuiInputTextCallbackDataPtr data, float wrapWidth, int maxLength,
        bool allowNewlines)
    {
        if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
        {
            if (data.EventChar is '\n' or '\r')
            {
                var shift = ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
                data.EventChar = allowNewlines && shift ? '\n' : (char)0;
            }

            return 0;
        }

        var current = Encoding.UTF8.GetString(data.BufSpan[..data.BufTextLen]);
        var charCursor = ByteIndexToCharIndex(current, data.CursorPos);
        var logicalCursor = charCursor - CountNonLogical(current, charCursor);

        var logical = StripSoftWraps(current);
        if (logical.Length > maxLength)
        {
            logical = logical[..maxLength];
            if (logicalCursor > maxLength)
            {
                logicalCursor = maxLength;
            }
        }

        var wrapped = WrapText(logical, wrapWidth);
        if (string.Equals(wrapped, current, StringComparison.Ordinal))
        {
            return 0;
        }

        var wrappedCursor = LogicalToWrappedIndex(wrapped, logicalCursor);
        var byteCursor = Encoding.UTF8.GetByteCount(wrapped.AsSpan(0, wrappedCursor));

        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, wrapped);
        data.CursorPos = byteCursor;
        data.SelectionStart = byteCursor;
        data.SelectionEnd = byteCursor;
        return 0;
    }

    public static string WrapText(string text, float wrapWidth)
    {
        if (text.Length == 0 || wrapWidth <= 0f)
        {
            return text;
        }

        Plugin.Fonts.NoticeText(text);

        var builder = new StringBuilder(text.Length + 16);
        var lineWidth = 0f;
        var lineStart = 0;
        var wordStart = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '\n')
            {
                builder.Append('\n');
                lineStart = builder.Length;
                lineWidth = 0f;
                wordStart = builder.Length;
                index++;
                continue;
            }

            var runeLength = char.IsHighSurrogate(text[index]) && index + 1 < text.Length &&
                             char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;
            var isSpace = runeLength == 1 && text[index] is ' ' or '\t';
            var characterWidth = ImGui.CalcTextSize(text.Substring(index, runeLength)).X;

            if (!isSpace && lineWidth > 0f && lineWidth + characterWidth > wrapWidth)
            {
                if (wordStart > lineStart)
                {
                    builder.Insert(wordStart, SoftWrapSentinel + "\n");
                    lineStart = wordStart + 2;
                    lineWidth = MeasureRange(builder, lineStart);
                    wordStart = lineStart;
                }
                else
                {
                    builder.Append(SoftWrapSentinel).Append('\n');
                    lineStart = builder.Length;
                    lineWidth = 0f;
                    wordStart = builder.Length;
                }
            }

            builder.Append(text, index, runeLength);
            lineWidth += characterWidth;
            if (isSpace)
            {
                wordStart = builder.Length;
            }

            index += runeLength;
        }

        return builder.ToString();
    }

    public static string StripSoftWraps(string text)
    {
        if (text.IndexOf(SoftWrapSentinel) < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == SoftWrapSentinel)
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }

    public static void ReadLogical(ImGuiInputTextCallbackDataPtr data, out string display, out string logical,
        out int logicalCursor)
    {
        display = Encoding.UTF8.GetString(data.BufSpan[..data.BufTextLen]);
        var charCursor = ByteIndexToCharIndex(display, data.CursorPos);
        logicalCursor = charCursor - CountNonLogical(display, charCursor);
        logical = StripSoftWraps(display);
    }

    public static void SetCursor(ImGuiInputTextCallbackDataPtr data, string display, int logicalCursor)
    {
        var wrappedCursor = LogicalToWrappedIndex(display, logicalCursor);
        var byteCursor = Encoding.UTF8.GetByteCount(display.AsSpan(0, wrappedCursor));
        data.CursorPos = byteCursor;
        data.SelectionStart = byteCursor;
        data.SelectionEnd = byteCursor;
    }

    public static void WriteLogical(ImGuiInputTextCallbackDataPtr data, string logical, int logicalCursor,
        float wrapWidth)
    {
        var wrapped = WrapText(logical, wrapWidth);
        var wrappedCursor = LogicalToWrappedIndex(wrapped, logicalCursor);
        var byteCursor = Encoding.UTF8.GetByteCount(wrapped.AsSpan(0, wrappedCursor));
        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, wrapped);
        data.CursorPos = byteCursor;
        data.SelectionStart = byteCursor;
        data.SelectionEnd = byteCursor;
    }

    public static int LogicalLength(string text)
    {
        var length = text.Length;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n' || text[index] == SoftWrapSentinel)
            {
                length--;
            }
        }

        return length;
    }

    private static float MeasureRange(StringBuilder builder, int start)
    {
        if (start >= builder.Length)
        {
            return 0f;
        }

        return ImGui.CalcTextSize(builder.ToString(start, builder.Length - start)).X;
    }

    private static int CountNonLogical(string text, int limit)
    {
        var count = 0;
        for (var index = 0; index < limit; index++)
        {
            if (text[index] == '\n' || text[index] == SoftWrapSentinel)
            {
                count++;
            }
        }

        return count;
    }

    private static int ByteIndexToCharIndex(string text, int byteIndex)
    {
        if (byteIndex <= 0)
        {
            return 0;
        }

        var bytes = 0;
        var index = 0;
        while (index < text.Length && bytes < byteIndex)
        {
            var runeLength = char.IsHighSurrogate(text[index]) && index + 1 < text.Length &&
                             char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;
            bytes += Encoding.UTF8.GetByteCount(text.AsSpan(index, runeLength));
            index += runeLength;
        }

        return index;
    }

    private static int LogicalToWrappedIndex(string wrapped, int logicalCursor)
    {
        var seen = 0;
        var index = 0;
        while (index < wrapped.Length && seen < logicalCursor)
        {
            if (wrapped[index] != '\n' && wrapped[index] != SoftWrapSentinel)
            {
                seen++;
            }

            index++;
        }

        return index;
    }
}
