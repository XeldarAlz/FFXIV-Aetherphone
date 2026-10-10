using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class NightWordmark
{
    private const float HeightRatio = 1.3f;
    private const int LatinLimit = 0x024F;
    private const int RunCacheLimit = 512;

    private readonly record struct Run(string Text, bool Gothic);

    private static readonly Dictionary<string, Run[]> RunCache = new(StringComparer.Ordinal);

    public static bool Usable(string text)
    {
        if (!SeasonalTheme.Halloween || !Plugin.Fonts.DisplayReady)
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] > LatinLimit)
            {
                return false;
            }
        }

        return true;
    }

    public static bool Fits(string text, float maxWidth, float lineHeight, out Vector2 size)
    {
        size = default;
        if (!Usable(text))
        {
            return false;
        }

        size = Measure(text, lineHeight);
        return size.X <= maxWidth;
    }

    public static Vector2 Measure(string text, float lineHeight)
    {
        var pixels = lineHeight * HeightRatio;
        using (Plugin.Fonts.PushDisplay())
        {
            return ImGui.CalcTextSize(text) * (pixels / ImGui.GetFontSize());
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float lineHeight) =>
        Draw(drawList, position, text, color, lineHeight, default);

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float lineHeight,
        in TextEffect effect)
    {
        var pixels = lineHeight * HeightRatio;
        using (Plugin.Fonts.PushDisplay())
        {
            var size = ImGui.CalcTextSize(text) * (pixels / ImGui.GetFontSize());
            Typography.DrawEffect(drawList, ImGui.GetFont(), pixels, position, text, size, color, effect);
        }
    }

    public static bool FitsMixed(string text, float maxWidth, float lineHeight, in TextStyle fallback, out float width)
    {
        width = 0f;
        if (!SeasonalTheme.Halloween || !Plugin.Fonts.DisplayReady)
        {
            return false;
        }

        var runs = RunsOf(text);
        if (runs.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < runs.Length; index++)
        {
            var run = runs[index];
            width += run.Gothic ? Measure(run.Text, lineHeight).X : Typography.Measure(run.Text, fallback).X;
        }

        return width <= maxWidth;
    }

    public static void DrawMixed(ImDrawListPtr drawList, Vector2 lineTop, string text, Vector4 color, float lineHeight,
        in TextStyle fallback, in TextEffect effect)
    {
        var runs = RunsOf(text);
        var cursor = lineTop.X;
        for (var index = 0; index < runs.Length; index++)
        {
            var run = runs[index];
            var size = run.Gothic ? Measure(run.Text, lineHeight) : Typography.Measure(run.Text, fallback);
            var position = new Vector2(cursor, lineTop.Y + (lineHeight - size.Y) * 0.5f);
            if (run.Gothic)
            {
                Draw(drawList, position, run.Text, color, lineHeight, effect);
            }
            else
            {
                Typography.Draw(drawList, position, run.Text, color, fallback, effect);
            }

            cursor += size.X;
        }
    }

    private static Run[] RunsOf(string text)
    {
        if (RunCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        if (RunCache.Count >= RunCacheLimit)
        {
            RunCache.Clear();
        }

        var runs = Split(text);
        RunCache[text] = runs;
        return runs;
    }

    private static Run[] Split(string text)
    {
        var hasLetter = false;
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var gothic = text[index] <= LatinLimit;
            hasLetter |= gothic && char.IsLetterOrDigit(text[index]);
            if (index == 0 || gothic != (text[index - 1] <= LatinLimit))
            {
                count++;
            }
        }

        if (!hasLetter)
        {
            return Array.Empty<Run>();
        }

        var runs = new Run[count];
        var start = 0;
        var slot = 0;
        for (var index = 1; index <= text.Length; index++)
        {
            if (index < text.Length && (text[index] <= LatinLimit) == (text[start] <= LatinLimit))
            {
                continue;
            }

            runs[slot++] = new Run(text.Substring(start, index - start), text[start] <= LatinLimit);
            start = index;
        }

        return runs;
    }
}
