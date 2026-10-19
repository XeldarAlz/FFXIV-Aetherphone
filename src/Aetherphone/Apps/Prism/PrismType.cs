using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal enum PrismFace : byte
{
    Headline,
    Numerals,
    Label,
}

internal static class PrismType
{
    private const float LabelLeading = 1.45f;
    private const float HeadlineLeading = 1.02f;
    private const int CacheLimit = 256;
    private const float FitStep = 0.92f;
    private const float BalanceFloor = 0.5f;
    private const int BalanceSteps = 10;

    private static readonly Dictionary<WrapKey, string[]> WrapCache = new();
    private static readonly List<string> Scratch = new();
    private static readonly Dictionary<BalanceKey, float> BalanceCache = new();

    private readonly record struct WrapKey(string Text, PrismFace Face, int Pixels, int Width);

    private readonly record struct BalanceKey(string Text, float Scale, FontWeight Weight, int Width);

    public static float Snap(PrismFace face, float pixels) =>
        Tier(face, pixels) * UiBuilder.DefaultFontSizePx * UiScale.Current * Plugin.Fonts.Zoom;

    public static Vector2 Measure(string text, PrismFace face, float pixels)
    {
        using (Push(text, face, pixels))
        {
            return ImGui.CalcTextSize(text) * (Snap(face, pixels) / ImGui.GetFontSize());
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, PrismFace face,
        float pixels)
    {
        using (Push(text, face, pixels))
        {
            drawList.AddText(ImGui.GetFont(), Snap(face, pixels), position, ImGui.GetColorU32(color), text);
        }
    }

    public static float DrawRight(ImDrawListPtr drawList, Vector2 topRight, string text, Vector4 color,
        PrismFace face, float pixels)
    {
        var width = Measure(text, face, pixels).X;
        Draw(drawList, new Vector2(topRight.X - width, topRight.Y), text, color, face, pixels);
        return width;
    }

    public static float LineHeight(PrismFace face, float pixels) =>
        Snap(face, pixels) * (face == PrismFace.Label ? LabelLeading : HeadlineLeading);

    public static string[] Wrap(string text, PrismFace face, float pixels, float width)
    {
        var key = new WrapKey(text, face, (int)MathF.Round(Snap(face, pixels)), (int)MathF.Round(width));
        if (WrapCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (WrapCache.Count >= CacheLimit)
        {
            WrapCache.Clear();
        }

        var lines = Break(text, face, pixels, width);
        if (face == PrismFace.Headline && lines.Length > 1)
        {
            var low = width * BalanceFloor;
            var high = width;
            for (var step = 0; step < BalanceSteps; step++)
            {
                var middle = (low + high) * 0.5f;
                if (Break(text, face, pixels, middle).Length <= lines.Length)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }

            lines = Break(text, face, pixels, high);
        }

        WrapCache[key] = lines;
        return lines;
    }

    public static float BalancedWidth(string text, in TextStyle style, float width)
    {
        var key = new BalanceKey(text, style.Scale, style.Weight, (int)MathF.Round(width));
        if (BalanceCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (BalanceCache.Count >= CacheLimit)
        {
            BalanceCache.Clear();
        }

        var count = Typography.WrapText(text, style, width).Length;
        var high = width;
        if (count > 1)
        {
            var low = width * BalanceFloor;
            for (var step = 0; step < BalanceSteps; step++)
            {
                var middle = (low + high) * 0.5f;
                if (Typography.WrapText(text, style, middle).Length <= count)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }
        }

        BalanceCache[key] = high;
        return high;
    }

    private static string[] Break(string text, PrismFace face, float pixels, float width)
    {
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

        return Scratch.ToArray();
    }

    public static float Fit(string text, PrismFace face, float width, float maxPixels, float minPixels,
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
        PrismFace face, float pixels)
    {
        var lineHeight = LineHeight(face, pixels);
        for (var index = 0; index < lines.Length; index++)
        {
            Draw(drawList, new Vector2(topLeft.X, topLeft.Y + index * lineHeight), lines[index], color, face, pixels);
        }

        return lines.Length * lineHeight;
    }

    private static float Widest(string[] lines, PrismFace face, float pixels)
    {
        var widest = 0f;
        for (var index = 0; index < lines.Length; index++)
        {
            widest = MathF.Max(widest, Measure(lines[index], face, pixels).X);
        }

        return widest;
    }

    private static float Tier(PrismFace face, float pixels)
    {
        var scale = pixels / (UiBuilder.DefaultFontSizePx * UiScale.Current);
        var tier = face switch
        {
            PrismFace.Headline => FontService.TitleScaleAtMost(scale),
            PrismFace.Numerals => FontService.NumeralScaleAtMost(scale),
            _ => FontService.TextScaleAtMost(scale),
        };
        return tier > 0f ? tier : FontService.SmallestTextScale;
    }

    private static FontToken Push(string text, PrismFace face, float pixels)
    {
        var tier = Tier(face, pixels);
        Plugin.Fonts.NoticeText(text);
        return face switch
        {
            PrismFace.Headline => Plugin.Fonts.PushTitle(tier, text),
            PrismFace.Numerals => Plugin.Fonts.PushNumerals(tier, text),
            _ => Plugin.Fonts.Push(tier, FontWeight.Medium),
        };
    }
}
