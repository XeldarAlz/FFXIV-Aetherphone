using Aetherphone.Core.Emoji;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class EmojiText
{
    public static float MeasureWidth(string text, float fontSize)
    {
        if (!EmojiCatalog.Ready)
        {
            return ImGui.CalcTextSize(text).X;
        }

        var width = 0f;
        var runStart = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (EmojiCatalog.TryMatch(text, index, out var matched, out _))
            {
                if (index > runStart)
                {
                    width += ImGui.CalcTextSize(text.AsSpan(runStart, index - runStart)).X;
                }

                width += EmojiRender.Advance(fontSize);
                index += matched;
                runStart = index;
                continue;
            }

            index++;
        }

        if (text.Length > runStart)
        {
            width += ImGui.CalcTextSize(text.AsSpan(runStart, text.Length - runStart)).X;
        }

        return width;
    }

    public static float DrawLine(ImDrawListPtr drawList, string text, Vector2 origin, float fontSize, uint color,
        float scrollX)
    {
        var font = ImGui.GetFont();
        var x = origin.X - scrollX;
        if (!EmojiCatalog.Ready)
        {
            drawList.AddText(font, fontSize, new Vector2(x, origin.Y), color, text);
            return x + ImGui.CalcTextSize(text).X;
        }

        var runStart = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (EmojiCatalog.TryMatch(text, index, out var matched, out var file))
            {
                x = FlushRun(drawList, text, runStart, index, font, fontSize, color, x, origin.Y);
                EmojiRender.Draw(drawList, file, new Vector2(x, origin.Y), fontSize, 1f);
                x += EmojiRender.Advance(fontSize);
                index += matched;
                runStart = index;
                continue;
            }

            index++;
        }

        return FlushRun(drawList, text, runStart, text.Length, font, fontSize, color, x, origin.Y);
    }

    private static float FlushRun(ImDrawListPtr drawList, string text, int start, int end, ImFontPtr font,
        float fontSize, uint color, float x, float y)
    {
        if (end <= start)
        {
            return x;
        }

        var run = text.Substring(start, end - start);
        drawList.AddText(font, fontSize, new Vector2(x, y), color, run);
        return x + ImGui.CalcTextSize(run).X;
    }
}
