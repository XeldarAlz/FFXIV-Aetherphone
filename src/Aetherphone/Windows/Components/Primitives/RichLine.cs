using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct RichLineFit
{
    public readonly RichTextLayout? Layout;
    public readonly string Text;
    public readonly string Source;
    public readonly float MaxWidth;
    public readonly float FontSize;
    public readonly int FontGeneration;
    public readonly TextStyle Style;

    public RichLineFit(RichTextLayout? layout, string text, string source, float maxWidth, float fontSize,
        int fontGeneration, TextStyle style)
    {
        Layout = layout;
        Text = text;
        Source = source;
        MaxWidth = maxWidth;
        FontSize = fontSize;
        FontGeneration = fontGeneration;
        Style = style;
    }
}

internal static class RichLine
{
    private const float UnboundedWidth = 100000f;
    private const string Ellipsis = "…";

    public static bool Valid(in RichLineFit fit, string source, float maxWidth, in TextStyle style)
    {
        if (!ReferenceEquals(fit.Source, source) || fit.MaxWidth != maxWidth || fit.Style != style
            || fit.FontGeneration != Plugin.Fonts.Generation)
        {
            return false;
        }

        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            return fit.FontSize == ImGui.GetFontSize();
        }
    }

    public static RichLineFit Fit(string text, int tailLength, float maxWidth, in TextStyle style)
    {
        var generation = Plugin.Fonts.Generation;
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            var fontSize = ImGui.GetFontSize();
            var layout = RichText.Build(text, ReadOnlySpan<MentionSpan>.Empty, UnboundedWidth);
            if (layout is null)
            {
                return new RichLineFit(null, FitPlain(text, maxWidth), text, maxWidth, fontSize, generation, style);
            }

            if (layout.Size.X <= maxWidth)
            {
                return new RichLineFit(layout, text, text, maxWidth, fontSize, generation, style);
            }

            var tailStart = Math.Clamp(text.Length - tailLength, 0, text.Length);
            var head = text.AsSpan(0, tailStart);
            var tail = text.AsSpan(tailStart);
            var bestText = string.Concat(Ellipsis, tail);
            var bestLayout = RichText.Build(bestText, ReadOnlySpan<MentionSpan>.Empty, UnboundedWidth);
            var low = 0;
            var high = head.Length;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                var cut = mid > 0 && char.IsHighSurrogate(head[mid - 1]) ? mid - 1 : mid;
                var candidate = string.Concat(head[..cut].TrimEnd(), Ellipsis, tail);
                var trial = RichText.Build(candidate, ReadOnlySpan<MentionSpan>.Empty, UnboundedWidth);
                var width = trial?.Size.X ?? ImGui.CalcTextSize(candidate).X;
                if (width <= maxWidth)
                {
                    low = mid;
                    bestText = candidate;
                    bestLayout = trial;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return new RichLineFit(bestLayout, bestText, text, maxWidth, fontSize, generation, style);
        }
    }

    public static void Draw(ImDrawListPtr drawList, in RichLineFit fit, Vector2 origin, Vector4 ink)
    {
        if (fit.Layout is null)
        {
            Typography.Draw(drawList, origin, fit.Text, ink, fit.Style);
            return;
        }

        using (Plugin.Fonts.Push(fit.Style.Scale, fit.Style.Weight))
        {
            RichText.Draw(drawList, fit.Layout, origin, new RichTextInk(ink, ink, ink, 1f, 1f, false), out _);
        }
    }

    private static string FitPlain(string text, float maxWidth)
    {
        if (ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return text;
        }

        var low = 0;
        var high = text.Length;
        var best = Ellipsis;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var cut = mid > 0 && char.IsHighSurrogate(text[mid - 1]) ? mid - 1 : mid;
            var candidate = string.Concat(text.AsSpan(0, cut).TrimEnd(), Ellipsis);
            if (ImGui.CalcTextSize(candidate).X <= maxWidth)
            {
                low = mid;
                best = candidate;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }
}
