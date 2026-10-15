using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Strip;

internal readonly record struct TileLabelFit(string[] Lines, TextStyle Style);

internal static class TileLabel
{
    private const char IdeographicStart = (char)0x2E80;

    public static TileLabelFit Fit(string text, float width, int maxLines, in TextStyle primary,
        in TextStyle fallback)
    {
        var lines = Typography.WrapText(text, primary, width);
        if (Keeps(text, lines, maxLines))
        {
            return new TileLabelFit(lines, primary);
        }

        lines = Typography.WrapText(text, fallback, width);
        if (Keeps(text, lines, maxLines))
        {
            return new TileLabelFit(lines, fallback);
        }

        var shrunk = new TextStyle(
            Typography.FitScale(text, width, fallback.Scale, TextStyles.Caption1.Scale, fallback.Weight),
            fallback.Weight);
        var single = Typography.FitText(text, width, shrunk);
        return new TileLabelFit(Typography.WrapText(single, shrunk, float.MaxValue), shrunk);
    }

    public static float Height(in TileLabelFit fit) => fit.Lines.Length * Typography.LineHeight(fit.Style);

    internal static bool Keeps(ReadOnlySpan<char> text, string[] lines, int maxLines) =>
        lines.Length <= maxLines && WholeWords(text, lines);

    internal static bool WholeWords(ReadOnlySpan<char> text, string[] lines)
    {
        var position = 0;
        for (var lineIndex = 0; lineIndex < lines.Length - 1; lineIndex++)
        {
            while (position < text.Length && text[position] == ' ')
            {
                position++;
            }

            var line = lines[lineIndex];
            if (line.Length == 0 || !text[position..].StartsWith(line, StringComparison.Ordinal))
            {
                return true;
            }

            position += line.Length;
            if (position >= text.Length)
            {
                return true;
            }

            var before = line[^1];
            var after = text[position];
            if (after != ' ' && before != '-' && before < IdeographicStart && after < IdeographicStart)
            {
                return false;
            }
        }

        return true;
    }
}
