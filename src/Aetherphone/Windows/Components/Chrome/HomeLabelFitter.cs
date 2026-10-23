using Aetherphone.Core;

namespace Aetherphone.Windows.Components;

internal readonly record struct HomeLabelLayout(string First, string? Second, float Scale);

internal static class HomeLabelFitter
{
    public const float SingleLineMinimumScale = 0.88f;
    public const float WordMinimumScale = 0.72f;
    private const char FirstIdeographic = (char)0x2E80;
    private const int CacheLimit = 512;

    private static readonly Dictionary<LabelKey, HomeLabelLayout> Cache = new();

    public static HomeLabelLayout For(string label, float maxWidth, in TextStyle style)
    {
        var key = new LabelKey(label, (int)MathF.Round(maxWidth), style.Scale, style.Weight);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= CacheLimit)
        {
            Cache.Clear();
        }

        var measured = style;
        var layout = Compute(label, maxWidth, text => Typography.Measure(text, measured).X);
        Cache[key] = layout;
        return layout;
    }

    public static HomeLabelLayout Compute(string label, float maxWidth, Func<string, float> measure)
    {
        var full = measure(label);
        if (full <= maxWidth || maxWidth <= 0f)
        {
            return new HomeLabelLayout(label, null, 1f);
        }

        if (full * SingleLineMinimumScale <= maxWidth)
        {
            return new HomeLabelLayout(label, null, maxWidth / full);
        }

        var hasSpace = label.Contains(' ');
        if (!hasSpace && full * WordMinimumScale <= maxWidth)
        {
            return new HomeLabelLayout(label, null, maxWidth / full);
        }

        return hasSpace ? SplitAtSpace(label, maxWidth, measure) : SplitWord(label, maxWidth, full, measure);
    }

    private static HomeLabelLayout SplitAtSpace(string label, float maxWidth, Func<string, float> measure)
    {
        var bestFirst = label;
        string? bestSecond = null;
        var bestWidest = float.MaxValue;
        for (var index = 1; index < label.Length - 1; index++)
        {
            if (label[index] != ' ')
            {
                continue;
            }

            var first = label[..index].TrimEnd();
            var second = label[(index + 1)..].TrimStart();
            if (first.Length == 0 || second.Length == 0)
            {
                continue;
            }

            var widest = MathF.Max(measure(first), measure(second));
            if (widest >= bestWidest)
            {
                continue;
            }

            bestWidest = widest;
            bestFirst = first;
            bestSecond = second;
        }

        if (bestSecond is null)
        {
            return new HomeLabelLayout(label, null, maxWidth / measure(label));
        }

        return new HomeLabelLayout(bestFirst, bestSecond, MathF.Min(1f, maxWidth / bestWidest));
    }

    private static HomeLabelLayout SplitWord(string label, float maxWidth, float full, Func<string, float> measure)
    {
        var bestFirst = label;
        string? bestSecond = null;
        var bestWidest = float.MaxValue;
        for (var index = 1; index < label.Length; index++)
        {
            if (char.IsLowSurrogate(label[index]))
            {
                continue;
            }

            var first = Hyphenates(label, index) ? label[..index] + "-" : label[..index];
            var second = label[index..];
            var widest = MathF.Max(measure(first), measure(second));
            if (widest >= bestWidest)
            {
                continue;
            }

            bestWidest = widest;
            bestFirst = first;
            bestSecond = second;
        }

        if (bestSecond is null)
        {
            return new HomeLabelLayout(label, null, maxWidth / full);
        }

        return new HomeLabelLayout(bestFirst, bestSecond, MathF.Min(1f, maxWidth / bestWidest));
    }

    private static bool Hyphenates(string label, int index)
    {
        var before = label[index - 1];
        var after = label[index];
        return char.IsLetter(before) && char.IsLetter(after) && before < FirstIdeographic && after < FirstIdeographic;
    }

    private readonly record struct LabelKey(string Label, int Width, float StyleScale, FontWeight Weight);
}
