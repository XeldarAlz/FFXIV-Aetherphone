using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismType
{
    private const float FallbackDisplayScale = 1.90f;
    private const float FallbackLabelScale = 1.00f;
    private const float LabelLeading = 1.45f;
    private const float HeadlineLeading = 1.02f;
    private const int CacheLimit = 256;
    private const float FitStep = 0.92f;

    private static readonly Dictionary<string, string> TitleCache = new(StringComparer.Ordinal);
    private static string titleLanguage = string.Empty;
    private static readonly Dictionary<WrapKey, string[]> WrapCache = new();
    private static readonly List<string> Scratch = new();

    private readonly record struct WrapKey(string Text, EditorialFace Face, int Pixels, int Width, bool Native);

    public static string Title(string text)
    {
        if (!string.Equals(titleLanguage, Loc.Current.Code, StringComparison.Ordinal))
        {
            TitleCache.Clear();
            titleLanguage = Loc.Current.Code;
        }

        if (TitleCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        if (TitleCache.Count >= CacheLimit)
        {
            TitleCache.Clear();
        }

        var title = Loc.Culture.TextInfo.ToTitleCase(text);
        TitleCache[text] = title;
        return title;
    }

    public static float Snap(EditorialFace face, float pixels)
    {
        var scale = UiScale.Current;
        return FontService.EditorialUnits(face, FontService.EditorialStep(face, pixels / scale)) * scale *
               Plugin.Fonts.Zoom;
    }

    public static Vector2 Measure(string text, EditorialFace face, float pixels)
    {
        var native = Native(text, face);
        using (Push(text, face, pixels, native))
        {
            var size = ImGui.CalcTextSize(text);
            return native ? size : size * (Snap(face, pixels) / ImGui.GetFontSize());
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, EditorialFace face,
        float pixels)
    {
        var native = Native(text, face);
        using (Push(text, face, pixels, native))
        {
            drawList.AddText(ImGui.GetFont(), native ? ImGui.GetFontSize() : Snap(face, pixels), position,
                ImGui.GetColorU32(color), text);
        }
    }

    public static float DrawRight(ImDrawListPtr drawList, Vector2 topRight, string text, Vector4 color,
        EditorialFace face, float pixels)
    {
        var width = Measure(text, face, pixels).X;
        Draw(drawList, new Vector2(topRight.X - width, topRight.Y), text, color, face, pixels);
        return width;
    }

    public static float LineHeight(EditorialFace face, float pixels) => Snap(face, pixels) * (face == EditorialFace.Label ? LabelLeading : HeadlineLeading);

    public static string[] Wrap(string text, EditorialFace face, float pixels, float width)
    {
        var key = new WrapKey(text, face, (int)MathF.Round(Snap(face, pixels)), (int)MathF.Round(width), Native(text, face));
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
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == ' ')
            {
                index++;
                continue;
            }

            var end = index + 1;
            if (!Typography.IsCjk(text[index]))
            {
                while (end < text.Length && text[end] != ' ' && !Typography.IsCjk(text[end]))
                {
                    end++;
                }
            }

            var word = text[index..end];
            var spaced = index > 0 && text[index - 1] == ' ';
            var candidate = line.Length == 0 ? word : spaced ? line + " " + word : line + word;
            if (line.Length > 0 && Measure(candidate, face, pixels).X > width)
            {
                Scratch.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }

            index = end;
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
        var pixels = Snap(face, maxPixels);
        var floor = Snap(face, minPixels);
        while (true)
        {
            lines = Wrap(text, face, pixels, width);
            if (pixels <= floor + 0.01f || (lines.Length <= maxLines && Widest(lines, face, pixels) <= width))
            {
                return pixels;
            }

            pixels = Snap(face, pixels * FitStep);
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

    private static FontToken Push(string text, EditorialFace face, float pixels, bool native)
    {
        if (native)
        {
            return Plugin.Fonts.PushEditorial(face, FontService.EditorialStep(face, pixels / UiScale.Current));
        }

        Plugin.Fonts.NoticeText(text);
        return face == EditorialFace.Label
            ? Plugin.Fonts.Push(FallbackLabelScale, FontWeight.Medium)
            : Plugin.Fonts.Push(FallbackDisplayScale, FontWeight.Bold);
    }
}
