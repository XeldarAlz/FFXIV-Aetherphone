using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class RunTextLayout
{
    public readonly List<RunPiece> Pieces = new(8);
    public Vector2 Size;
    public float WrapWidth;
    public float FontSize;
    public int FontGeneration;
    public int RunCount;
}

internal readonly struct RunPiece
{
    public readonly string Text;
    public readonly Vector2 Offset;
    public readonly float Width;
    public readonly int Run;

    public RunPiece(string text, Vector2 offset, float width, int run)
    {
        Text = text;
        Offset = offset;
        Width = width;
        Run = run;
    }
}

internal static class RunText
{
    private const int CacheLimit = 192;
    private const float UnderlineInset = 1f;

    private static readonly Dictionary<string, RunTextLayout> Cache = new(StringComparer.Ordinal);

    public static void Reset() => Cache.Clear();

    public static RunTextLayout Layout(string key, ReadOnlySpan<TextRun> runs, float wrapWidth)
    {
        var fontSize = ImGui.GetFontSize();
        var generation = Plugin.Fonts.Generation;
        if (Cache.TryGetValue(key, out var cached) && cached.RunCount == runs.Length &&
            Math.Abs(cached.WrapWidth - wrapWidth) < 0.5f && Math.Abs(cached.FontSize - fontSize) < 0.01f &&
            cached.FontGeneration == generation)
        {
            return cached;
        }

        if (Cache.Count > CacheLimit)
        {
            Cache.Clear();
            cached = null;
        }

        cached ??= new RunTextLayout();
        Build(cached, runs, wrapWidth, fontSize, generation);
        Cache[key] = cached;
        return cached;
    }

    public static int Draw(ImDrawListPtr drawList, RunTextLayout layout, ReadOnlySpan<TextRun> runs, Vector2 origin,
        Vector4 bodyInk, float alpha, bool interactive)
    {
        var font = ImGui.GetFont();
        var fontSize = layout.FontSize;
        var scale = UiScale.Current;
        var hovered = -1;
        if (interactive)
        {
            for (var index = 0; index < layout.Pieces.Count; index++)
            {
                var piece = layout.Pieces[index];
                if (piece.Run < 0 || piece.Run >= runs.Length || !runs[piece.Run].Interactive)
                {
                    continue;
                }

                var min = origin + piece.Offset;
                var max = min + new Vector2(piece.Width, fontSize);
                if (UiInteract.Hover(min, max))
                {
                    hovered = piece.Run;
                    break;
                }
            }
        }

        var thickness = MathF.Max(1f, scale);
        for (var index = 0; index < layout.Pieces.Count; index++)
        {
            var piece = layout.Pieces[index];
            var run = piece.Run >= 0 && piece.Run < runs.Length ? runs[piece.Run] : TextRun.Plain(piece.Text);
            var ink = run.Interactive ? run.Tint : bodyInk;
            if (run.Interactive && piece.Run == hovered)
            {
                ink = Palette.WithAlpha(ink, MathF.Min(1f, ink.W * 1.25f));
            }

            var position = origin + piece.Offset;
            if (run.IsEmoji)
            {
                EmojiRender.Draw(drawList, run.EmojiFile, position, fontSize, alpha);
                continue;
            }

            drawList.AddText(font, fontSize, position,
                ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * alpha)), piece.Text);
            if (!run.Interactive)
            {
                continue;
            }

            var underlineY = position.Y + fontSize - UnderlineInset * scale;
            drawList.AddLine(new Vector2(position.X, underlineY), new Vector2(position.X + piece.Width, underlineY),
                ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * alpha * 0.75f)), thickness);
        }

        if (hovered < 0)
        {
            return -1;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left) ? runs[hovered].Target : -1;
    }

    private static void Build(RunTextLayout layout, ReadOnlySpan<TextRun> runs, float wrapWidth, float fontSize,
        int generation)
    {
        layout.Pieces.Clear();
        layout.WrapWidth = wrapWidth;
        layout.FontSize = fontSize;
        layout.FontGeneration = generation;
        layout.RunCount = runs.Length;
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        var cursorX = 0f;
        var cursorY = 0f;
        var widest = 0f;
        for (var index = 0; index < runs.Length; index++)
        {
            if (runs[index].IsEmoji)
            {
                var advance = EmojiRender.Advance(fontSize);
                if (cursorX > 0f && cursorX + advance > wrapWidth)
                {
                    cursorX = 0f;
                    cursorY += lineHeight;
                }

                layout.Pieces.Add(new RunPiece(string.Empty, new Vector2(cursorX, cursorY), advance, index));
                cursorX += advance;
                widest = MathF.Max(widest, cursorX);
                continue;
            }

            var text = runs[index].Text;
            if (text.Length == 0)
            {
                continue;
            }

            var start = 0;
            while (start < text.Length)
            {
                var wordEnd = WordEnd(text, start);
                var word = text[start..wordEnd];
                var width = ImGui.CalcTextSize(word).X;
                if (cursorX > 0f && cursorX + width > wrapWidth)
                {
                    cursorX = 0f;
                    cursorY += lineHeight;
                    if (word[0] == ' ')
                    {
                        start = wordEnd;
                        continue;
                    }
                }

                if (width > wrapWidth && cursorX <= 0f)
                {
                    wordEnd = BreakLongWord(text, start, wrapWidth, out width);
                    word = text[start..wordEnd];
                }

                layout.Pieces.Add(new RunPiece(word, new Vector2(cursorX, cursorY), width, index));
                cursorX += width;
                widest = MathF.Max(widest, cursorX);
                start = wordEnd;
            }
        }

        layout.Size = new Vector2(MathF.Min(widest, wrapWidth), cursorY + lineHeight);
    }

    private static int WordEnd(string text, int start)
    {
        if (text[start] == ' ')
        {
            var spaces = start;
            while (spaces < text.Length && text[spaces] == ' ')
            {
                spaces++;
            }

            return spaces;
        }

        var index = start;
        while (index < text.Length && text[index] != ' ')
        {
            index++;
        }

        return index;
    }

    private static int BreakLongWord(string text, int start, float wrapWidth, out float width)
    {
        var end = start + 1;
        width = ImGui.CalcTextSize(text[start..end]).X;
        while (end < text.Length && text[end] != ' ')
        {
            var candidate = ImGui.CalcTextSize(text[start..(end + 1)]).X;
            if (candidate > wrapWidth)
            {
                break;
            }

            width = candidate;
            end++;
        }

        return end;
    }
}
