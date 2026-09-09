using System.Text;

namespace Aetherphone.Windows.Components;

internal sealed class SoftWrapBuffer
{
    private readonly Func<string, float, string> wrapLine;
    private readonly List<int> breaks = new(16);
    private string source = string.Empty;
    private string display = string.Empty;
    private float width;

    public SoftWrapBuffer(Func<string, float, string> wrapLine) => this.wrapLine = wrapLine;

    public string Display => display;

    public int LineCount { get; private set; } = 1;

    public void Reset(string text)
    {
        source = string.Empty;
        display = text;
        width = 0f;
        breaks.Clear();
        LineCount = 1;
    }

    public bool Rewrap(string logical, float wrapWidth)
    {
        if (MathF.Abs(wrapWidth - width) < 0.5f && string.Equals(source, logical, StringComparison.Ordinal))
        {
            return false;
        }

        width = wrapWidth;
        source = logical;
        var wrapped = Wrap(logical, wrapWidth);
        LineCount = CountLines(wrapped);
        if (string.Equals(wrapped, display, StringComparison.Ordinal))
        {
            return false;
        }

        display = wrapped;
        return true;
    }

    public string Merge(string logical, string current, int cursor, out int logicalCursor)
    {
        var prefix = CommonPrefix(display, current);
        var suffix = CommonSuffix(display, current, prefix);
        var removedEnd = display.Length - suffix;
        var inserted = current[prefix..(current.Length - suffix)];
        var start = Math.Clamp(LogicalIndexOf(prefix), 0, logical.Length);
        var end = Math.Clamp(LogicalIndexOf(removedEnd), start, logical.Length);
        var merged = string.Concat(logical.AsSpan(0, start), inserted.AsSpan(), logical.AsSpan(end));
        if (cursor <= prefix)
        {
            logicalCursor = LogicalIndexOf(cursor);
        }
        else if (cursor >= prefix + inserted.Length)
        {
            var shift = inserted.Length - (removedEnd - prefix);
            logicalCursor = LogicalIndexOf(cursor - shift) + start + inserted.Length - end;
        }
        else
        {
            logicalCursor = start + cursor - prefix;
        }

        logicalCursor = Math.Clamp(logicalCursor, 0, merged.Length);
        return merged;
    }

    public int LogicalIndexOf(int displayIndex)
    {
        var soft = 0;
        while (soft < breaks.Count && breaks[soft] < displayIndex)
        {
            soft++;
        }

        return displayIndex - soft;
    }

    public int DisplayIndexOf(int logicalIndex)
    {
        var displayIndex = logicalIndex;
        for (var index = 0; index < breaks.Count && breaks[index] <= displayIndex; index++)
        {
            displayIndex++;
        }

        return Math.Clamp(displayIndex, 0, display.Length);
    }

    private string Wrap(string logical, float wrapWidth)
    {
        breaks.Clear();
        if (logical.IndexOf('\n') < 0)
        {
            var single = wrapLine(logical, wrapWidth);
            Collect(single, 0);
            return single;
        }

        var builder = new StringBuilder(logical.Length + 16);
        var start = 0;
        while (true)
        {
            var found = logical.IndexOf('\n', start);
            var stop = found < 0 ? logical.Length : found;
            var segment = wrapLine(logical[start..stop], wrapWidth);
            Collect(segment, builder.Length);
            builder.Append(segment);
            if (found < 0)
            {
                break;
            }

            builder.Append('\n');
            start = found + 1;
        }

        return builder.ToString();
    }

    private void Collect(string segment, int offset)
    {
        for (var index = 0; index < segment.Length; index++)
        {
            if (segment[index] == '\n')
            {
                breaks.Add(offset + index);
            }
        }
    }

    private static int CommonPrefix(string previous, string current)
    {
        var limit = Math.Min(previous.Length, current.Length);
        var index = 0;
        while (index < limit && previous[index] == current[index])
        {
            index++;
        }

        if (index > 0 && char.IsHighSurrogate(previous[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static int CommonSuffix(string previous, string current, int prefix)
    {
        var limit = Math.Min(previous.Length, current.Length) - prefix;
        var index = 0;
        while (index < limit && previous[previous.Length - 1 - index] == current[current.Length - 1 - index])
        {
            index++;
        }

        if (index > 0 && char.IsLowSurrogate(previous[previous.Length - index]))
        {
            index--;
        }

        return index;
    }

    private static int CountLines(string text)
    {
        var lines = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }
}
