using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal readonly struct LinkTextRun
{
    public readonly string Text;
    public readonly Vector2 Offset;
    public readonly float Width;
    public readonly int UrlIndex;

    public LinkTextRun(string text, Vector2 offset, float width, int urlIndex)
    {
        Text = text;
        Offset = offset;
        Width = width;
        UrlIndex = urlIndex;
    }
}

internal sealed class LinkTextLayout
{
    public readonly LinkTextRun[] Runs;
    public readonly string[] Urls;
    public readonly Vector2 Size;
    public readonly float WrapWidth;
    public readonly float FontSize;

    public LinkTextLayout(LinkTextRun[] runs, string[] urls, Vector2 size, float wrapWidth, float fontSize)
    {
        Runs = runs;
        Urls = urls;
        Size = size;
        WrapWidth = wrapWidth;
        FontSize = fontSize;
    }
}

internal static class LinkText
{
    private const int CacheLimit = 768;
    private static readonly Dictionary<string, LinkTextLayout?> LayoutCache = new();

    public static LinkTextLayout? LayoutFor(string text, float wrapWidth)
    {
        if (text.Length < 7 || wrapWidth <= 0f || !HasCandidate(text))
        {
            return null;
        }

        var fontSize = ImGui.GetFontSize();
        if (LayoutCache.TryGetValue(text, out var cached))
        {
            if (cached is null)
            {
                return null;
            }

            if (cached.WrapWidth == wrapWidth && cached.FontSize == fontSize)
            {
                return cached;
            }
        }

        if (LayoutCache.Count > CacheLimit)
        {
            LayoutCache.Clear();
        }

        var spans = ParseLinks(text);
        if (spans is null)
        {
            LayoutCache[text] = null;
            return null;
        }

        var layout = BuildLayout(text, spans, wrapWidth, fontSize);
        LayoutCache[text] = layout;
        return layout;
    }

    public static void Draw(ImDrawListPtr drawList, LinkTextLayout layout, Vector2 origin, float pop, Vector4 ink,
        Vector4 linkInk, float alpha, bool interactive)
    {
        var font = ImGui.GetFont();
        var fontSize = layout.FontSize * pop;
        var scale = ImGuiHelpers.GlobalScale;
        var runs = layout.Runs;
        var hovered = -1;
        if (interactive)
        {
            var tooltip = Loc.T(L.Common.OpenInBrowser);
            for (var index = 0; index < runs.Length; index++)
            {
                var run = runs[index];
                if (run.UrlIndex < 0)
                {
                    continue;
                }

                var rectMin = origin + run.Offset * pop;
                var rectMax = rectMin + new Vector2(run.Width * pop, fontSize);
                HoverTooltip.Show(new Rect(rectMin, rectMax), tooltip, HoverLabelSide.Above);
                if (hovered < 0 && UiInteract.Hover(rectMin, rectMax))
                {
                    hovered = run.UrlIndex;
                }
            }
        }

        var inkPacked = ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * alpha));
        var thickness = MathF.Max(1f, scale);
        for (var index = 0; index < runs.Length; index++)
        {
            var run = runs[index];
            var position = origin + run.Offset * pop;
            if (run.UrlIndex < 0)
            {
                drawList.AddText(font, fontSize, position, inkPacked, run.Text);
                continue;
            }

            var emphasis = run.UrlIndex == hovered ? 1f : 0.88f;
            var linkPacked = ImGui.GetColorU32(Palette.WithAlpha(linkInk, linkInk.W * alpha * emphasis));
            drawList.AddText(font, fontSize, position, linkPacked, run.Text);
            var underlineY = position.Y + fontSize - thickness * 0.5f;
            drawList.AddLine(new Vector2(position.X, underlineY),
                new Vector2(position.X + run.Width * pop, underlineY), linkPacked, thickness);
        }

        if (hovered < 0)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            UrlActions.OpenInBrowser(layout.Urls[hovered]);
        }
    }

    private static bool HasCandidate(string text)
    {
        return text.Contains("http", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("www.", StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct UrlSpan
    {
        public readonly int Start;
        public readonly int Length;
        public readonly string Url;

        public UrlSpan(int start, int length, string url)
        {
            Start = start;
            Length = length;
            Url = url;
        }
    }

    private static List<UrlSpan>? ParseLinks(string text)
    {
        List<UrlSpan>? spans = null;
        var position = 0;
        var length = text.Length;
        while (position < length)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                position++;
                continue;
            }

            var tokenStart = position;
            while (position < length && !char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            var tokenEnd = position;
            var isBare = false;
            var linkStart = IndexOfScheme(text, tokenStart, tokenEnd, out var schemeLength);
            if (linkStart < 0)
            {
                var trimmed = tokenStart;
                while (trimmed < tokenEnd && IsLeadingWrapper(text[trimmed]))
                {
                    trimmed++;
                }

                if (!StartsWithIgnoreCase(text, trimmed, tokenEnd, "www."))
                {
                    continue;
                }

                linkStart = trimmed;
                schemeLength = 4;
                isBare = true;
            }

            var linkEnd = TrimTrailing(text, linkStart + schemeLength, tokenEnd);
            if (!HasHost(text, linkStart + schemeLength, linkEnd))
            {
                continue;
            }

            spans ??= new List<UrlSpan>();
            var raw = text.Substring(linkStart, linkEnd - linkStart);
            spans.Add(new UrlSpan(linkStart, linkEnd - linkStart, isBare ? "https://" + raw : raw));
        }

        return spans;
    }

    private static int IndexOfScheme(string text, int start, int end, out int schemeLength)
    {
        for (var index = start; index + 7 <= end; index++)
        {
            if (text[index] != 'h' && text[index] != 'H')
            {
                continue;
            }

            if (StartsWithIgnoreCase(text, index, end, "https://"))
            {
                schemeLength = 8;
                return index;
            }

            if (StartsWithIgnoreCase(text, index, end, "http://"))
            {
                schemeLength = 7;
                return index;
            }
        }

        schemeLength = 0;
        return -1;
    }

    private static bool StartsWithIgnoreCase(string text, int start, int end, string prefix)
    {
        if (end - start < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (char.ToLowerInvariant(text[start + index]) != prefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLeadingWrapper(char character)
    {
        return character is '(' or '[' or '<' or '"' or '\'';
    }

    private static int TrimTrailing(string text, int bodyStart, int end)
    {
        while (end > bodyStart)
        {
            var last = text[end - 1];
            if (last is '.' or ',' or '!' or '?' or ';' or ':' or '\'' or '"' or '>')
            {
                end--;
                continue;
            }

            if (last is ')' && CountChar(text, bodyStart, end, '(') < CountChar(text, bodyStart, end, ')'))
            {
                end--;
                continue;
            }

            if (last is ']' && CountChar(text, bodyStart, end, '[') < CountChar(text, bodyStart, end, ']'))
            {
                end--;
                continue;
            }

            break;
        }

        return end;
    }

    private static int CountChar(string text, int start, int end, char target)
    {
        var count = 0;
        for (var index = start; index < end; index++)
        {
            if (text[index] == target)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasHost(string text, int bodyStart, int end)
    {
        if (end - bodyStart < 3)
        {
            return false;
        }

        for (var index = bodyStart + 1; index < end - 1; index++)
        {
            if (text[index] == '.')
            {
                return true;
            }
        }

        return false;
    }

    private static LinkTextLayout BuildLayout(string text, List<UrlSpan> spans, float wrapWidth, float fontSize)
    {
        var runs = new List<LinkTextRun>();
        var lineHeight = ImGui.GetTextLineHeight();
        var x = 0f;
        var y = 0f;
        var maxWidth = 0f;
        var runStart = 0;
        var runX = 0f;
        var position = 0;
        var length = text.Length;

        void Flush(int endExclusive)
        {
            if (endExclusive <= runStart)
            {
                return;
            }

            var runText = text.Substring(runStart, endExclusive - runStart);
            runs.Add(new LinkTextRun(runText, new Vector2(runX, y), x - runX, UrlIndexAt(spans, runStart)));
            if (x > maxWidth)
            {
                maxWidth = x;
            }
        }

        void StartLine(int nextStart)
        {
            x = 0f;
            y += lineHeight;
            runStart = nextStart;
            runX = 0f;
        }

        while (position < length)
        {
            var character = text[position];
            if (character == '\n')
            {
                Flush(position);
                position++;
                StartLine(position);
                continue;
            }

            if (character == '\r')
            {
                Flush(position);
                position++;
                runStart = position;
                runX = x;
                continue;
            }

            var urlIndex = UrlIndexAt(spans, position);
            if (position > runStart && urlIndex != UrlIndexAt(spans, runStart))
            {
                Flush(position);
                runStart = position;
                runX = x;
            }

            var isSpace = character is ' ' or '\t';
            var atomEnd = position + 1;
            if (!isSpace)
            {
                while (atomEnd < length)
                {
                    var next = text[atomEnd];
                    if (next is ' ' or '\t' or '\n' or '\r' || UrlIndexAt(spans, atomEnd) != urlIndex)
                    {
                        break;
                    }

                    atomEnd++;
                }
            }

            var atomWidth = MeasureWidth(text, position, atomEnd);
            if (!isSpace && x > 0f && x + atomWidth > wrapWidth)
            {
                Flush(position);
                StartLine(position);
            }

            if (!isSpace && atomWidth > wrapWidth)
            {
                var charPosition = position;
                while (charPosition < atomEnd)
                {
                    var runeLength = char.IsHighSurrogate(text[charPosition]) && charPosition + 1 < atomEnd &&
                                     char.IsLowSurrogate(text[charPosition + 1])
                        ? 2
                        : 1;
                    var charWidth = MeasureWidth(text, charPosition, charPosition + runeLength);
                    if (x > 0f && x + charWidth > wrapWidth)
                    {
                        Flush(charPosition);
                        StartLine(charPosition);
                    }

                    x += charWidth;
                    charPosition += runeLength;
                }

                position = atomEnd;
                continue;
            }

            x += atomWidth;
            position = atomEnd;
        }

        Flush(length);
        var height = length == 0 ? lineHeight : y + lineHeight;
        return new LinkTextLayout(runs.ToArray(), BuildUrls(spans), new Vector2(maxWidth, height), wrapWidth,
            fontSize);
    }

    private static string[] BuildUrls(List<UrlSpan> spans)
    {
        var urls = new string[spans.Count];
        for (var index = 0; index < spans.Count; index++)
        {
            urls[index] = spans[index].Url;
        }

        return urls;
    }

    private static int UrlIndexAt(List<UrlSpan> spans, int position)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (position >= span.Start && position < span.Start + span.Length)
            {
                return index;
            }
        }

        return -1;
    }

    private static float MeasureWidth(string text, int start, int end)
    {
        return ImGui.CalcTextSize(text.AsSpan(start, end - start)).X;
    }
}
