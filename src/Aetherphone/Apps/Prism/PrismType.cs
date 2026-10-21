using System.Text;
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
    private const int FitAttempts = 32;
    private const float BalanceFloor = 0.5f;
    private const int BalanceSteps = 10;

    private static readonly Dictionary<WrapKey, string[]> WrapCache = new();
    private static readonly List<string> Scratch = new();
    private static readonly Dictionary<BalanceKey, float> BalanceCache = new();
    private static readonly Dictionary<BalanceKey, string> SentenceCache = new();

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

    public static string FitLabel(string text, float pixels, float width)
    {
        var tier = Tier(PrismFace.Label, pixels);
        float fontSize;
        using (Plugin.Fonts.Push(tier, FontWeight.Medium))
        {
            fontSize = ImGui.GetFontSize();
        }

        return Typography.FitText(text, width * fontSize / Snap(PrismFace.Label, pixels), tier, FontWeight.Medium);
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

    public static string Sentences(string text, in TextStyle style, float width)
    {
        var key = new BalanceKey(text, style.Scale, style.Weight, (int)MathF.Round(width));
        if (SentenceCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (SentenceCache.Count >= CacheLimit)
        {
            SentenceCache.Clear();
        }

        var broken = BreakSentences(text);
        var result = !ReferenceEquals(broken, text) &&
                     Typography.WrapText(broken, style, width).Length <= Typography.WrapText(text, style, width).Length
            ? broken
            : text;
        SentenceCache[key] = result;
        return result;
    }

    private static string BreakSentences(string text)
    {
        var builder = new StringBuilder(text.Length);
        var broke = false;
        for (var index = 0; index < text.Length; index++)
        {
            var glyph = text[index];
            builder.Append(glyph);
            if (index + 1 == text.Length)
            {
                break;
            }

            if (glyph is '。' or '！' or '？')
            {
                builder.Append('\n');
                broke = true;
            }
            else if (glyph is '.' or '!' or '?' && text[index + 1] == ' ')
            {
                builder.Append('\n');
                index++;
                broke = true;
            }
        }

        return broke ? builder.ToString() : text;
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
        var probe = new WrapProbe(text, face, width, maxLines);
        var pixels = FitRequest(ref probe, maxPixels, minPixels);
        lines = Wrap(text, face, pixels, width);
        return pixels;
    }

    internal static float FitRequest<TProbe>(ref TProbe probe, float maxPixels, float minPixels)
        where TProbe : struct, IFitProbe
    {
        var floor = probe.Snap(minPixels);
        var pixels = maxPixels;
        for (var attempt = 0; attempt < FitAttempts; attempt++)
        {
            if (probe.Snap(pixels) <= floor + 0.01f || probe.Fits(pixels))
            {
                return pixels;
            }

            pixels *= FitStep;
        }

        return pixels;
    }

    internal interface IFitProbe
    {
        float Snap(float pixels);

        bool Fits(float pixels);
    }

    private readonly struct WrapProbe : IFitProbe
    {
        private readonly string text;
        private readonly PrismFace face;
        private readonly float width;
        private readonly int maxLines;

        public WrapProbe(string text, PrismFace face, float width, int maxLines)
        {
            this.text = text;
            this.face = face;
            this.width = width;
            this.maxLines = maxLines;
        }

        public float Snap(float pixels) => PrismType.Snap(face, pixels);

        public bool Fits(float pixels)
        {
            var lines = Wrap(text, face, pixels, width);
            return lines.Length <= maxLines && Widest(lines, face, pixels) <= width;
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
