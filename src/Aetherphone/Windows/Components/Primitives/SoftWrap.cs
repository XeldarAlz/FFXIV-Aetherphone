using System.Text;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SoftWrap
{
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
            var runeLength = RuneLength(text, index);
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

    public static int RuneLength(string text, int index)
    {
        return char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
            ? 2
            : 1;
    }

    public static int CharIndexOf(string text, int byteIndex)
    {
        if (byteIndex <= 0)
        {
            return 0;
        }

        var bytes = 0;
        var index = 0;
        while (index < text.Length && bytes < byteIndex)
        {
            var runeLength = RuneLength(text, index);
            bytes += Encoding.UTF8.GetByteCount(text.AsSpan(index, runeLength));
            index += runeLength;
        }

        return index;
    }

    private static float MeasureRange(StringBuilder builder, int start)
    {
        if (start >= builder.Length)
        {
            return 0f;
        }

        return ImGui.CalcTextSize(builder.ToString(start, builder.Length - start)).X;
    }
}
