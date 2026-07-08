using System.Numerics;
using System.Text;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class Typography
{
    private const string Ellipsis = "…";

    private static readonly Vector2[] HaloOffsets =
    {
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, 1f),
        new(0f, -1f),
        new(0.7071f, 0.7071f),
        new(-0.7071f, 0.7071f),
        new(0.7071f, -0.7071f),
        new(-0.7071f, -0.7071f),
    };

    private readonly struct FitEntry
    {
        public readonly float Width;
        public readonly string Text;

        public FitEntry(float width, string text)
        {
            Width = width;
            Text = text;
        }
    }

    private static readonly Dictionary<string, FitEntry> FitCache = new();

    private readonly struct WrapEntry
    {
        public readonly float Width;
        public readonly string[] Lines;

        public WrapEntry(float width, string[] lines)
        {
            Width = width;
            Lines = lines;
        }
    }

    private static readonly Dictionary<string, WrapEntry> WrapCache = new();

    public static Vector2 Measure(string text, float scale = 1f) => Measure(text, scale, FontWeight.Regular);
    public static Vector2 Measure(string text, in TextStyle style) => Measure(text, style.Scale, style.Weight);

    public static Vector2 Measure(string text, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            return ImGui.CalcTextSize(text);
        }
    }

    public static float MeasureWrapped(string text, float wrapWidth, float fontScale,
        FontWeight weight = FontWeight.Regular)
    {
        using (Plugin.Fonts.Push(fontScale, weight))
        {
            return ImGui.CalcTextSize(text, false, wrapWidth).Y;
        }
    }

    public static void Draw(Vector2 position, string text, Vector4 color, float scale = 1f) =>
        Draw(position, text, color, scale, FontWeight.Regular);

    public static void Draw(Vector2 position, string text, Vector4 color, in TextStyle style) =>
        Draw(position, text, color, style.Scale, style.Weight);

    public static void Draw(Vector2 position, string text, Vector4 color, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            ImGui.SetCursorScreenPos(position);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale = 1f) =>
        Draw(drawList, position, text, color, scale, FontWeight.Regular);

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color,
        in TextStyle style) =>
        Draw(drawList, position, text, color, style.Scale, style.Weight);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        in TextStyle style) =>
        DrawCentered(drawList, center, text, color, style.Scale, style.Weight);

    public static string FitText(string text, float maxWidth, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            return Fit(text, maxWidth);
        }
    }

    public static string FitText(string text, float maxWidth, in TextStyle style) =>
        FitText(text, maxWidth, style.Scale, style.Weight);

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale,
        FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), position, ImGui.GetColorU32(color), text);
        }
    }

    public static void DrawCentered(Vector2 center, string text, Vector4 color, float scale = 1f) =>
        DrawCentered(center, text, color, scale, FontWeight.Regular);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float scale = 1f) =>
        DrawCentered(drawList, center, text, color, scale, FontWeight.Regular);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color, float scale,
        FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            var size = ImGui.CalcTextSize(text);
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), center - size * 0.5f, ImGui.GetColorU32(color),
                text);
        }
    }

    public static void DrawCenteredHalo(Vector2 center, string text, Vector4 color, Vector4 halo, float haloRadius,
        float maxWidth, in TextStyle style)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            var display = Fit(text, maxWidth);
            var drawList = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var origin = center - ImGui.CalcTextSize(display) * 0.5f;
            var haloColor = ImGui.GetColorU32(halo);
            for (var offsetIndex = 0; offsetIndex < HaloOffsets.Length; offsetIndex++)
            {
                drawList.AddText(font, fontSize, origin + HaloOffsets[offsetIndex] * haloRadius, haloColor, display);
            }

            drawList.AddText(font, fontSize, origin, ImGui.GetColorU32(color), display);
        }
    }

    public static float DrawWrappedCentered(Vector2 topCenter, string text, Vector4 color, in TextStyle style,
        float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            var lines = WrapLines(text, maxWidth);
            var drawList = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var packed = ImGui.GetColorU32(color);
            for (var index = 0; index < lines.Length; index++)
            {
                var size = ImGui.CalcTextSize(lines[index]);
                drawList.AddText(font, fontSize, new Vector2(topCenter.X - size.X * 0.5f, topCenter.Y + index * lineHeight),
                    packed, lines[index]);
            }

            return lines.Length * lineHeight;
        }
    }

    public static void DrawWrappedCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        in TextStyle style, float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            var lines = WrapLines(text, maxWidth);
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var packed = ImGui.GetColorU32(color);
            var top = center.Y - lines.Length * lineHeight * 0.5f;
            for (var index = 0; index < lines.Length; index++)
            {
                var size = ImGui.CalcTextSize(lines[index]);
                drawList.AddText(font, fontSize, new Vector2(center.X - size.X * 0.5f, top + index * lineHeight), packed,
                    lines[index]);
            }
        }
    }

    private static string[] WrapLines(string text, float maxWidth)
    {
        if (WrapCache.TryGetValue(text, out var cached) && cached.Width == maxWidth)
        {
            return cached.Lines;
        }

        var lines = Wrap(text, maxWidth);
        WrapCache[text] = new WrapEntry(maxWidth, lines);
        return lines;
    }

    private static string[] Wrap(string text, float maxWidth)
    {
        if (maxWidth <= 0f || ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return new[] { text };
        }

        var lines = new List<string>();
        var builder = new StringBuilder();
        var words = text.Split(' ');
        for (var index = 0; index < words.Length; index++)
        {
            var word = words[index];
            if (builder.Length == 0)
            {
                builder.Append(word);
                continue;
            }

            builder.Append(' ').Append(word);
            if (ImGui.CalcTextSize(builder.ToString()).X > maxWidth)
            {
                builder.Length -= word.Length + 1;
                lines.Add(builder.ToString());
                builder.Clear();
                builder.Append(word);
            }
        }

        if (builder.Length > 0)
        {
            lines.Add(builder.ToString());
        }

        return lines.ToArray();
    }

    private static string Fit(string text, float maxWidth)
    {
        if (maxWidth <= 0f)
        {
            return text;
        }

        if (FitCache.TryGetValue(text, out var cached) && cached.Width == maxWidth)
        {
            return cached.Text;
        }

        var result = Shorten(text, maxWidth);
        FitCache[text] = new FitEntry(maxWidth, result);
        return result;
    }

    private static string Shorten(string text, float maxWidth)
    {
        if (ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return text;
        }

        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = string.Concat(text.AsSpan(0, length).TrimEnd(), Ellipsis.AsSpan());
            if (ImGui.CalcTextSize(candidate).X <= maxWidth)
            {
                return candidate;
            }
        }

        return Ellipsis;
    }

    public static void DrawCentered(Vector2 center, string text, Vector4 color, in TextStyle style) =>
        DrawCentered(center, text, color, style.Scale, style.Weight);

    public static void DrawCentered(Vector2 center, string text, Vector4 color, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            var size = ImGui.CalcTextSize(text);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }
}
