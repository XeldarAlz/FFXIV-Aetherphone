using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum RichTextRunKind : byte
{
    Plain,
    Link,
    Mention,
    Emoji,
}

internal readonly struct MentionSpan
{
    public readonly string Handle;
    public readonly string UserId;
    public readonly string DisplayName;

    public MentionSpan(string handle, string userId, string displayName)
    {
        Handle = handle;
        UserId = userId;
        DisplayName = displayName;
    }
}

internal readonly struct RichTextRun
{
    public readonly string Text;
    public readonly Vector2 Offset;
    public readonly float Width;
    public readonly RichTextRunKind Kind;
    public readonly int TargetIndex;

    public RichTextRun(string text, Vector2 offset, float width, RichTextRunKind kind, int targetIndex)
    {
        Text = text;
        Offset = offset;
        Width = width;
        Kind = kind;
        TargetIndex = targetIndex;
    }
}

internal sealed class RichTextLayout
{
    public readonly RichTextRun[] Runs;
    public readonly string[] Urls;
    public readonly string[] EmojiFiles;
    public readonly MentionSpan[] Mentions;
    public readonly Vector2 Size;
    public readonly float WrapWidth;
    public readonly float FontSize;
    public readonly int FontGeneration;

    public RichTextLayout(RichTextRun[] runs, string[] urls, string[] emojiFiles, MentionSpan[] mentions, Vector2 size,
        float wrapWidth, float fontSize, int fontGeneration)
    {
        Runs = runs;
        Urls = urls;
        EmojiFiles = emojiFiles;
        Mentions = mentions;
        Size = size;
        WrapWidth = wrapWidth;
        FontSize = fontSize;
        FontGeneration = fontGeneration;
    }
}

internal readonly struct RichTextInk
{
    public readonly Vector4 Body;
    public readonly Vector4 Link;
    public readonly Vector4 Mention;
    public readonly float Alpha;
    public readonly float Pop;
    public readonly bool Interactive;

    public RichTextInk(Vector4 body, Vector4 link, Vector4 mention, float alpha = 1f, float pop = 1f,
        bool interactive = true)
    {
        Body = body;
        Link = link;
        Mention = mention;
        Alpha = alpha;
        Pop = pop;
        Interactive = interactive;
    }
}

internal readonly struct RichTextHit
{
    public readonly RichTextRunKind Kind;
    public readonly int TargetIndex;
    public readonly bool Clicked;

    public RichTextHit(RichTextRunKind kind, int targetIndex, bool clicked)
    {
        Kind = kind;
        TargetIndex = targetIndex;
        Clicked = clicked;
    }
}

internal static class RichText
{
    private static readonly MentionSpan[] NoMentions = Array.Empty<MentionSpan>();
    private static readonly string[] NoUrls = Array.Empty<string>();
    private static readonly string[] NoEmoji = Array.Empty<string>();

    public static RichTextLayout? Build(string text, ReadOnlySpan<MentionSpan> mentions, float wrapWidth)
    {
        if (wrapWidth <= 0f)
        {
            return null;
        }

        var hasMention = mentions.Length > 0 && text.IndexOf('@') >= 0;
        var hasLink = text.Length >= 7 && HasLinkCandidate(text);
        var hasEmoji = EmojiScanner.MightContain(text);
        if (!hasMention && !hasLink && !hasEmoji)
        {
            return null;
        }

        var spans = ParseSpans(text, mentions, out var urls, out var emojiFiles);
        if (spans is null)
        {
            return null;
        }

        Plugin.Fonts.NoticeText(text);
        return BuildLayout(text, spans, urls, emojiFiles, mentions, wrapWidth, ImGui.GetFontSize(),
            Plugin.Fonts.Generation);
    }

    public static void Draw(ImDrawListPtr drawList, RichTextLayout layout, Vector2 origin, in RichTextInk ink,
        out RichTextHit hit)
    {
        var font = ImGui.GetFont();
        var pop = ink.Pop;
        var fontSize = layout.FontSize * pop;
        var scale = ImGuiHelpers.GlobalScale;
        var runs = layout.Runs;
        var hoveredKind = RichTextRunKind.Plain;
        var hoveredIndex = -1;
        if (ink.Interactive)
        {
            for (var index = 0; index < runs.Length; index++)
            {
                var run = runs[index];
                if (run.Kind is RichTextRunKind.Plain or RichTextRunKind.Emoji)
                {
                    continue;
                }

                var rectMin = origin + run.Offset * pop;
                var rectMax = rectMin + new Vector2(run.Width * pop, fontSize);
                var tooltip = run.Kind == RichTextRunKind.Link
                    ? Loc.T(L.Common.OpenInBrowser)
                    : Loc.T(L.Social.ViewProfile);
                HoverTooltip.Show(new Rect(rectMin, rectMax), tooltip, HoverLabelSide.Above);
                if (hoveredIndex < 0 && UiInteract.Hover(rectMin, rectMax))
                {
                    hoveredKind = run.Kind;
                    hoveredIndex = run.TargetIndex;
                }
            }
        }

        var inkPacked = ImGui.GetColorU32(Palette.WithAlpha(ink.Body, ink.Body.W * ink.Alpha));
        var thickness = MathF.Max(1f, scale);
        for (var index = 0; index < runs.Length; index++)
        {
            var run = runs[index];
            var position = origin + run.Offset * pop;
            if (run.Kind == RichTextRunKind.Plain)
            {
                drawList.AddText(font, fontSize, position, inkPacked, run.Text);
                continue;
            }

            if (run.Kind == RichTextRunKind.Emoji)
            {
                EmojiRender.Draw(drawList, layout.EmojiFiles[run.TargetIndex], position, fontSize, ink.Alpha);
                continue;
            }

            var accent = run.Kind == RichTextRunKind.Link ? ink.Link : ink.Mention;
            var emphasis = run.Kind == hoveredKind && run.TargetIndex == hoveredIndex ? 1f : 0.88f;
            var packed = ImGui.GetColorU32(Palette.WithAlpha(accent, accent.W * ink.Alpha * emphasis));
            drawList.AddText(font, fontSize, position, packed, run.Text);
            if (run.Kind != RichTextRunKind.Link)
            {
                continue;
            }

            var underlineY = position.Y + fontSize - thickness * 0.5f;
            drawList.AddLine(new Vector2(position.X, underlineY),
                new Vector2(position.X + run.Width * pop, underlineY), packed, thickness);
        }

        if (hoveredIndex < 0)
        {
            hit = new RichTextHit(RichTextRunKind.Plain, -1, false);
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        hit = new RichTextHit(hoveredKind, hoveredIndex, ImGui.IsMouseClicked(ImGuiMouseButton.Left));
    }

    private static bool HasLinkCandidate(string text)
    {
        return text.Contains("http", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("www.", StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct RichSpan
    {
        public readonly int Start;
        public readonly int Length;
        public readonly RichTextRunKind Kind;
        public readonly int TargetIndex;

        public RichSpan(int start, int length, RichTextRunKind kind, int targetIndex)
        {
            Start = start;
            Length = length;
            Kind = kind;
            TargetIndex = targetIndex;
        }
    }

    private static List<RichSpan>? ParseSpans(string text, ReadOnlySpan<MentionSpan> mentions, out string[] urls,
        out string[] emojiFiles)
    {
        var spans = ParseLinks(text, out urls);
        ScanMentions(text, mentions, ref spans);
        emojiFiles = ScanEmoji(text, ref spans);
        if (spans is null || spans.Count == 0)
        {
            return null;
        }

        spans.Sort(static (first, second) => first.Start.CompareTo(second.Start));
        return spans;
    }

    private static string[] ScanEmoji(string text, ref List<RichSpan>? spans)
    {
        if (!EmojiCatalog.Ready)
        {
            return NoEmoji;
        }

        var found = new List<EmojiSpan>();
        EmojiScanner.Collect(text, found);
        if (found.Count == 0)
        {
            return NoEmoji;
        }

        List<string>? files = null;
        for (var index = 0; index < found.Count; index++)
        {
            var emoji = found[index];
            if (InsideLink(spans, emoji.Start))
            {
                continue;
            }

            spans ??= new List<RichSpan>();
            files ??= new List<string>();
            spans.Add(new RichSpan(emoji.Start, emoji.Length, RichTextRunKind.Emoji, files.Count));
            files.Add(emoji.File);
        }

        return files is null ? NoEmoji : files.ToArray();
    }

    private static void ScanMentions(string text, ReadOnlySpan<MentionSpan> mentions, ref List<RichSpan>? spans)
    {
        if (mentions.Length == 0)
        {
            return;
        }

        var length = text.Length;
        for (var position = 0; position < length; position++)
        {
            if (text[position] != '@')
            {
                continue;
            }

            if (position > 0 && SocialIdentity.IsHandleChar(text[position - 1]))
            {
                continue;
            }

            if (InsideLink(spans, position))
            {
                continue;
            }

            var tokenStart = position + 1;
            var tokenEnd = tokenStart;
            while (tokenEnd < length && SocialIdentity.IsHandleChar(text[tokenEnd]))
            {
                tokenEnd++;
            }

            var tokenLength = tokenEnd - tokenStart;
            if (tokenLength == 0)
            {
                continue;
            }

            var mentionIndex = IndexOfHandle(mentions, text, tokenStart, tokenLength);
            if (mentionIndex < 0)
            {
                position = tokenEnd - 1;
                continue;
            }

            spans ??= new List<RichSpan>();
            spans.Add(new RichSpan(position, tokenEnd - position, RichTextRunKind.Mention, mentionIndex));
            position = tokenEnd - 1;
        }
    }

    private static bool InsideLink(List<RichSpan>? spans, int position)
    {
        if (spans is null)
        {
            return false;
        }

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (position >= span.Start && position < span.Start + span.Length)
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOfHandle(ReadOnlySpan<MentionSpan> mentions, string text, int start, int length)
    {
        var token = text.AsSpan(start, length);
        for (var index = 0; index < mentions.Length; index++)
        {
            if (token.Equals(mentions[index].Handle, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static List<RichSpan>? ParseLinks(string text, out string[] parsedUrls)
    {
        parsedUrls = NoUrls;
        if (!HasLinkCandidate(text))
        {
            return null;
        }

        List<RichSpan>? spans = null;
        List<string>? urls = null;
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

            spans ??= new List<RichSpan>();
            urls ??= new List<string>();
            var raw = text.Substring(linkStart, linkEnd - linkStart);
            spans.Add(new RichSpan(linkStart, linkEnd - linkStart, RichTextRunKind.Link, urls.Count));
            urls.Add(isBare ? "https://" + raw : raw);
        }

        parsedUrls = urls is null ? NoUrls : urls.ToArray();
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

    private static RichTextLayout BuildLayout(string text, List<RichSpan> spans, string[] urls, string[] emojiFiles,
        ReadOnlySpan<MentionSpan> mentions, float wrapWidth, float fontSize, int fontGeneration)
    {
        var runs = new List<RichTextRun>();
        var lineHeight = emojiFiles.Length > 0
            ? MathF.Max(ImGui.GetTextLineHeight(), EmojiRender.LineHeight(fontSize))
            : ImGui.GetTextLineHeight();
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
            var spanIndex = SpanIndexAt(spans, runStart);
            var kind = spanIndex < 0 ? RichTextRunKind.Plain : spans[spanIndex].Kind;
            var target = spanIndex < 0 ? -1 : spans[spanIndex].TargetIndex;
            runs.Add(new RichTextRun(runText, new Vector2(runX, y), x - runX, kind, target));
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

            var spanIndex = SpanIndexAt(spans, position);
            if (position > runStart && spanIndex != SpanIndexAt(spans, runStart))
            {
                Flush(position);
                runStart = position;
                runX = x;
            }

            var isEmoji = spanIndex >= 0 && spans[spanIndex].Kind == RichTextRunKind.Emoji;
            var isSpace = character is ' ' or '\t';
            var atomEnd = position + 1;
            if (!isSpace)
            {
                while (atomEnd < length)
                {
                    var next = text[atomEnd];
                    if (next is ' ' or '\t' or '\n' or '\r' || SpanIndexAt(spans, atomEnd) != spanIndex)
                    {
                        break;
                    }

                    atomEnd++;
                }
            }

            var atomWidth = isEmoji ? EmojiRender.Advance(fontSize) : MeasureWidth(text, position, atomEnd);
            if (!isSpace && x > 0f && x + atomWidth > wrapWidth)
            {
                Flush(position);
                StartLine(position);
            }

            if (!isSpace && !isEmoji && atomWidth > wrapWidth)
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
        var mentionCopy = mentions.Length == 0 ? NoMentions : mentions.ToArray();
        return new RichTextLayout(runs.ToArray(), urls, emojiFiles, mentionCopy, new Vector2(maxWidth, height),
            wrapWidth, fontSize, fontGeneration);
    }

    private static int SpanIndexAt(List<RichSpan> spans, int position)
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
