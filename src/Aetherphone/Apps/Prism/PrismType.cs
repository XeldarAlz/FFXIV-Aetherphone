using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismType
{
    private const float FallbackDisplayScale = 1.90f;
    private const float FallbackMonoScale = 1.00f;
    private const int CacheLimit = 256;
    private const float FitStep = 0.92f;

    private static readonly Dictionary<string, string> UpperCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<WrapKey, string[]> WrapCache = new();
    private static readonly List<string> Scratch = new();

    private readonly record struct WrapKey(string Text, EditorialFace Face, int Pixels, int Width, bool Native);

    public static string Upper(string text)
    {
        if (UpperCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        if (UpperCache.Count >= CacheLimit)
        {
            UpperCache.Clear();
        }

        var upper = Loc.Culture.TextInfo.ToUpper(text);
        UpperCache[text] = upper;
        return upper;
    }

    public static Vector2 Measure(string text, EditorialFace face, float pixels)
    {
        using (Push(text, face))
        {
            return ImGui.CalcTextSize(text) * (pixels / ImGui.GetFontSize());
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, EditorialFace face,
        float pixels)
    {
        using (Push(text, face))
        {
            drawList.AddText(ImGui.GetFont(), pixels, position, ImGui.GetColorU32(color), text);
        }
    }

    public static float DrawRight(ImDrawListPtr drawList, Vector2 topRight, string text, Vector4 color,
        EditorialFace face, float pixels)
    {
        var width = Measure(text, face, pixels).X;
        Draw(drawList, new Vector2(topRight.X - width, topRight.Y), text, color, face, pixels);
        return width;
    }

    public static float LineHeight(EditorialFace face, float pixels) => pixels * (face == EditorialFace.Mono ? 1.35f : 1.0f);

    public static string[] Wrap(string text, EditorialFace face, float pixels, float width)
    {
        var key = new WrapKey(text, face, (int)MathF.Round(pixels), (int)MathF.Round(width), Native(text, face));
        if (WrapCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (WrapCache.Count >= CacheLimit)
        {
            WrapCache.Clear();
        }

        Scratch.Clear();
        var line = string.Empty;
        var start = 0;
        for (var index = 0; index <= text.Length; index++)
        {
            if (index < text.Length && text[index] != ' ')
            {
                continue;
            }

            var word = text[start..index];
            start = index + 1;
            if (word.Length == 0)
            {
                continue;
            }

            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(candidate, face, pixels).X > width)
            {
                Scratch.Add(line);
                line = word;
                continue;
            }

            line = candidate;
        }

        if (line.Length > 0)
        {
            Scratch.Add(line);
        }

        var lines = Scratch.ToArray();
        WrapCache[key] = lines;
        return lines;
    }

    public static float Fit(string text, EditorialFace face, float width, float maxPixels, float minPixels,
        int maxLines, out string[] lines)
    {
        var pixels = maxPixels;
        while (true)
        {
            lines = Wrap(text, face, pixels, width);
            if (pixels <= minPixels || (lines.Length <= maxLines && Widest(lines, face, pixels) <= width))
            {
                return pixels;
            }

            pixels = MathF.Max(minPixels, pixels * FitStep);
        }
    }

    public static float DrawLines(ImDrawListPtr drawList, Vector2 topLeft, string[] lines, Vector4 color,
        EditorialFace face, float pixels)
    {
        var lineHeight = LineHeight(face, pixels);
        for (var index = 0; index < lines.Length; index++)
        {
            Draw(drawList, new Vector2(topLeft.X, topLeft.Y + index * lineHeight), lines[index], color, face, pixels);
        }

        return lines.Length * lineHeight;
    }

    private static float Widest(string[] lines, EditorialFace face, float pixels)
    {
        var widest = 0f;
        for (var index = 0; index < lines.Length; index++)
        {
            widest = MathF.Max(widest, Measure(lines[index], face, pixels).X);
        }

        return widest;
    }

    private static bool Native(string text, EditorialFace face) =>
        Plugin.Fonts.EditorialReady(face) && FontService.EditorialCovers(face, text);

    private static FontToken Push(string text, EditorialFace face)
    {
        if (Native(text, face))
        {
            return Plugin.Fonts.PushEditorial(face);
        }

        Plugin.Fonts.NoticeText(text);
        return face == EditorialFace.Mono
            ? Plugin.Fonts.Push(FallbackMonoScale, FontWeight.Medium)
            : Plugin.Fonts.Push(FallbackDisplayScale, FontWeight.Bold);
    }
}
