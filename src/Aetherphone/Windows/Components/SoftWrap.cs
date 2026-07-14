using System.Text;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

// Soft word wrapping for ImGui multi-line inputs, which do not wrap on their own. The engine keeps a
// "logical" string (what the user meant, no line breaks) and a "display" string (logical text with
// soft newlines inserted at word boundaries so it stays inside the field). Newlines are never part of
// the logical value: hard breaks are filtered out and soft breaks are stripped back out on read.
internal static class SoftWrap
{
    public static int ApplyEdit(ImGuiInputTextCallbackDataPtr data, float wrapWidth, int maxLength)
    {
        if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
        {
            if (data.EventChar is '\n' or '\r')
            {
                data.EventChar = 0;
            }

            return 0;
        }

        var current = Encoding.UTF8.GetString(data.BufSpan[..data.BufTextLen]);
        var charCursor = ByteIndexToCharIndex(current, data.CursorPos);
        var logicalCursor = charCursor - CountNewlines(current, charCursor);

        var logical = StripNewlines(current);
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
                    builder.Insert(wordStart, '\n');
                    lineStart = wordStart + 1;
                    lineWidth = MeasureRange(builder, lineStart);
                    wordStart = lineStart;
                }
                else
                {
                    builder.Append('\n');
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

    public static string StripNewlines(string text)
    {
        return text.IndexOf('\n') < 0 ? text : text.Replace("\n", string.Empty);
    }

    public static int LogicalLength(string text)
    {
        var length = text.Length;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
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

    private static int CountNewlines(string text, int limit)
    {
        var count = 0;
        for (var index = 0; index < limit; index++)
        {
            if (text[index] == '\n')
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
            if (wrapped[index] != '\n')
            {
                seen++;
            }

            index++;
        }

        return index;
    }
}
