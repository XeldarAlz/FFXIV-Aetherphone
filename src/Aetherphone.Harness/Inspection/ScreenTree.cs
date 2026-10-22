using System.Globalization;
using System.Text;
using Aetherphone.Core;

namespace Aetherphone.Harness.Inspection;

internal static class ScreenTree
{
    private const float RowSnap = 4f;
    private static readonly Comparison<ScreenLine> ByReadingOrder = ReadingOrder;

    public static List<ScreenLine> Visible(List<ScreenLine> lines, Rect area, bool includeCovered)
    {
        var visible = new List<ScreenLine>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var overlap = line.Bounds.Intersect(area);
            if (overlap.Width <= 0f || overlap.Height <= 0f || (line.Covered && !includeCovered))
            {
                continue;
            }

            visible.Add(line);
        }

        visible.Sort(ByReadingOrder);
        return visible;
    }

    public static bool TryFind(List<ScreenLine> lines, string query, out ScreenLine found, out Rect span)
    {
        found = default;
        span = default;
        if (query.Length == 0)
        {
            return false;
        }

        for (var pass = 0; pass < 3; pass++)
        {
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var start = pass switch
                {
                    0 => string.Equals(line.Text, query, StringComparison.Ordinal) ? 0 : -1,
                    1 => line.Text.IndexOf(query, StringComparison.Ordinal),
                    _ => line.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase),
                };
                if (start < 0)
                {
                    continue;
                }

                found = line;
                span = line.Span(start, query.Length);
                return true;
            }
        }

        return false;
    }

    public static string Format(List<ScreenLine> lines, List<KeyValuePair<string, Rect>> anchors, Vector2 origin,
        string header)
    {
        var builder = new StringBuilder(header).Append("  ").Append(lines.Count).Append(" lines\n");
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            builder.Append("  ");
            AppendRect(builder, line.Bounds.Translate(-origin));
            builder.Append("  ").Append(Number(line.Size)).Append("px  ").Append(line.Text);
            if (line.Clipped)
            {
                builder.Append("  (clipped)");
            }

            if (line.Covered)
            {
                builder.Append("  (covered)");
            }

            builder.Append('\n');
        }

        if (anchors.Count == 0)
        {
            return builder.ToString();
        }

        builder.Append("anchors\n");
        for (var index = 0; index < anchors.Count; index++)
        {
            builder.Append("  ").Append(anchors[index].Key).Append("  ");
            AppendRect(builder, anchors[index].Value.Translate(-origin));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static int ReadingOrder(ScreenLine left, ScreenLine right)
    {
        var row = MathF.Round(left.Bounds.Min.Y / RowSnap).CompareTo(MathF.Round(right.Bounds.Min.Y / RowSnap));
        return row != 0 ? row : left.Bounds.Min.X.CompareTo(right.Bounds.Min.X);
    }

    private static void AppendRect(StringBuilder builder, Rect rect) =>
        builder.Append(Number(rect.Min.X)).Append(',').Append(Number(rect.Min.Y)).Append(' ')
            .Append(Number(rect.Width)).Append('x').Append(Number(rect.Height));

    private static string Number(float value) => MathF.Round(value).ToString(CultureInfo.InvariantCulture);
}
