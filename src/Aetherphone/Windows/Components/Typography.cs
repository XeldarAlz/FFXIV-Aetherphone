using System.Text;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
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

    private static readonly Dictionary<(string Text, float Width, float FontSize), string> FitCache = new();

    private readonly struct WrapEntry
    {
        public readonly float Width;
        public readonly float FontSize;
        public readonly string[] Lines;

        public WrapEntry(float width, float fontSize, string[] lines)
        {
            Width = width;
            FontSize = fontSize;
            Lines = lines;
        }
    }

    private static readonly Dictionary<string, WrapEntry> WrapCache = new();

    private static int cacheGeneration;

    private static void InvalidateCachesOnFontChange()
    {
        var current = Plugin.Fonts.Generation;
        if (current == cacheGeneration)
        {
            return;
        }

        cacheGeneration = current;
        FitCache.Clear();
        WrapCache.Clear();
        FitScaleCache.Clear();
    }

    public static void Plain(string text)
    {
        Plugin.Fonts.NoticeText(text);
        ImGui.TextUnformatted(text);
    }

    public static void Wrapped(string text)
    {
        Plugin.Fonts.NoticeText(text);
        ImGui.TextWrapped(text);
    }

    public static Vector2 Measure(string text, float scale = 1f) => Measure(text, scale, FontWeight.Regular);
    public static Vector2 Measure(string text, in TextStyle style) => Measure(text, style.Scale, style.Weight);

    public static Vector2 Measure(string text, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            return ImGui.CalcTextSize(text);
        }
    }

    public static float MeasureWrapped(string text, float wrapWidth, float fontScale,
        FontWeight weight = FontWeight.Regular)
    {
        using (Plugin.Fonts.Push(fontScale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            return ImGui.CalcTextSize(text, false, wrapWidth).Y;
        }
    }

    public static float LineHeight(in TextStyle style)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            return ImGui.GetTextLineHeightWithSpacing();
        }
    }

    public static Vector2 MeasureWrappedBlock(string text, in TextStyle style, float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var lines = WrapLines(text, maxWidth);
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var width = 0f;
            for (var index = 0; index < lines.Length; index++)
            {
                width = MathF.Max(width, ImGui.CalcTextSize(lines[index]).X);
            }

            return new Vector2(width, lines.Length * lineHeight);
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
            Plugin.Fonts.NoticeText(text);
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
            Plugin.Fonts.NoticeText(text);
            return Fit(text, maxWidth);
        }
    }

    public static string FitText(string text, float maxWidth, in TextStyle style) =>
        FitText(text, maxWidth, style.Scale, style.Weight);

    private static readonly Dictionary<(string Text, float MaxWidth, float MaxScale, float MinScale, FontWeight Weight,
        float FontSize), float> FitScaleCache = new();

    public static float FitScale(string text, float maxWidth, float maxScale, float minScale, FontWeight weight)
    {
        if (maxWidth <= 0f)
        {
            return minScale;
        }

        InvalidateCachesOnFontChange();
        var fontSize = ImGui.GetFontSize();
        var key = (text, maxWidth, maxScale, minScale, weight, fontSize);
        if (FitScaleCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = minScale;
        for (var scale = maxScale; scale > minScale; scale -= 0.05f)
        {
            using (Plugin.Fonts.Push(scale, weight))
            {
                Plugin.Fonts.NoticeText(text);
                if (ImGui.CalcTextSize(text).X <= maxWidth)
                {
                    result = scale;
                    break;
                }
            }
        }

        FitScaleCache[key] = result;
        return result;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale,
        FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
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
            Plugin.Fonts.NoticeText(text);
            var size = ImGui.CalcTextSize(text);
            var wrapWidth = AutoWrapWidth(center.X);
            if (wrapWidth > 0f && size.X > wrapWidth)
            {
                DrawWrappedBlock(drawList, center, text, color, wrapWidth);
                return;
            }

            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), center - size * 0.5f, ImGui.GetColorU32(color),
                text);
        }
    }

    private static float AutoWrapWidth(float centerX)
    {
        var windowLeft = ImGui.GetWindowPos().X;
        var margin = 8f * ImGuiHelpers.GlobalScale;
        var left = windowLeft + ImGui.GetWindowContentRegionMin().X + margin;
        var right = windowLeft + ImGui.GetWindowContentRegionMax().X - margin;
        var half = MathF.Min(centerX - left, right - centerX);
        return half * 2f;
    }

    private static void DrawWrappedBlock(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float wrapWidth)
    {
        var lines = WrapLines(text, wrapWidth);
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

    public static float DrawWrappedCentered(ImDrawListPtr drawList, string text, in TextStyle style, Vector4 color,
        Vector2 topCenter, float maxWidth, float lineSpacing = 1.25f)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(color);
            var lineHeight = ImGui.CalcTextSize("Ay").Y * lineSpacing;
            var buffer = new List<string>();
            var y = topCenter.Y;
            var segments = text.Split('\n');
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                var segment = segments[segmentIndex].TrimEnd('\r');
                if (segment.Length == 0)
                {
                    y += lineHeight;
                    continue;
                }

                buffer.Clear();
                WrapSegment(segment, maxWidth, buffer);
                for (var lineIndex = 0; lineIndex < buffer.Count; lineIndex++)
                {
                    var line = buffer[lineIndex];
                    var size = ImGui.CalcTextSize(line);
                    drawList.AddText(font, fontSize,
                        new Vector2(topCenter.X - size.X * 0.5f, y + (lineHeight - size.Y) * 0.5f), packed, line);
                    y += lineHeight;
                }
            }

            return y;
        }
    }

    public static int CountWrappedLines(string text, in TextStyle style, float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var buffer = new List<string>();
            var lines = 0;
            var segments = text.Split('\n');
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                var segment = segments[segmentIndex].TrimEnd('\r');
                if (segment.Length == 0)
                {
                    lines++;
                    continue;
                }

                buffer.Clear();
                WrapSegment(segment, maxWidth, buffer);
                lines += buffer.Count;
            }

            return lines;
        }
    }

    public static void DrawCenteredHalo(Vector2 center, string text, Vector4 color, Vector4 halo, float haloRadius,
        float maxWidth, in TextStyle style)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
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
            Plugin.Fonts.NoticeText(text);
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
            Plugin.Fonts.NoticeText(text);
            DrawWrappedBlock(drawList, center, text, color, maxWidth);
        }
    }

    public static float DrawWrappedLeft(Vector2 topLeft, string text, Vector4 color, in TextStyle style,
        float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var lines = WrapLines(text, maxWidth);
            var drawList = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var packed = ImGui.GetColorU32(color);
            for (var index = 0; index < lines.Length; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(topLeft.X, topLeft.Y + index * lineHeight), packed,
                    lines[index]);
            }

            return lines.Length * lineHeight;
        }
    }

    public static string[] WrapText(string text, in TextStyle style, float maxWidth) =>
        WrapText(text, style.Scale, style.Weight, maxWidth);

    public static string[] WrapText(string text, float scale, FontWeight weight, float maxWidth)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            return WrapLines(text, maxWidth);
        }
    }

    public static string[] WrapCurrent(string text, float maxWidth) => WrapLines(text, maxWidth);

    private static string[] WrapLines(string text, float maxWidth)
    {
        InvalidateCachesOnFontChange();
        var fontSize = ImGui.GetFontSize();
        if (WrapCache.TryGetValue(text, out var cached) && cached.Width == maxWidth && cached.FontSize == fontSize)
        {
            return cached.Lines;
        }

        var lines = Wrap(text, maxWidth);
        WrapCache[text] = new WrapEntry(maxWidth, fontSize, lines);
        return lines;
    }

    private static string[] Wrap(string text, float maxWidth)
    {
        if (maxWidth <= 0f || ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return new[] { text };
        }

        var lines = new List<string>();
        var segments = text.Split('\n');
        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            WrapSegment(segments[segmentIndex].TrimEnd('\r'), maxWidth, lines);
        }

        return lines.ToArray();
    }

    private static bool IsCjk(char value)
    {
        return value >= '⺀' && value <= '鿿'
            || value >= '豈' && value <= '﫿'
            || value >= '＀' && value <= '￯';
    }

    private static void WrapSegment(string segment, float maxWidth, List<string> lines)
    {
        if (segment.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        var builder = new StringBuilder();
        var length = segment.Length;
        var index = 0;
        var pendingSpace = false;
        while (index < length)
        {
            if (segment[index] == ' ')
            {
                pendingSpace = true;
                index++;
                continue;
            }

            string token;
            if (IsCjk(segment[index]))
            {
                token = segment[index].ToString();
                index++;
            }
            else
            {
                var start = index;
                while (index < length && segment[index] != ' ' && !IsCjk(segment[index]))
                {
                    index++;
                }

                token = segment.Substring(start, index - start);
            }

            if (builder.Length == 0)
            {
                builder.Append(token);
                pendingSpace = false;
                continue;
            }

            var separatorLength = pendingSpace ? 1 : 0;
            if (pendingSpace)
            {
                builder.Append(' ');
            }

            builder.Append(token);
            pendingSpace = false;
            if (ImGui.CalcTextSize(builder.ToString()).X > maxWidth)
            {
                builder.Length -= token.Length + separatorLength;
                lines.Add(builder.ToString());
                builder.Clear();
                builder.Append(token);
            }
        }

        if (builder.Length > 0)
        {
            lines.Add(builder.ToString());
        }
    }

    private static string Fit(string text, float maxWidth)
    {
        if (maxWidth <= 0f)
        {
            return text;
        }

        InvalidateCachesOnFontChange();
        var fontSize = ImGui.GetFontSize();
        var key = (text, maxWidth, fontSize);
        if (FitCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = Shorten(text, maxWidth);
        FitCache[key] = result;
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
            Plugin.Fonts.NoticeText(text);
            var size = ImGui.CalcTextSize(text);
            var wrapWidth = AutoWrapWidth(center.X);
            if (wrapWidth > 0f && size.X > wrapWidth)
            {
                DrawWrappedBlock(ImGui.GetWindowDrawList(), center, text, color, wrapWidth);
                return;
            }

            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }
}
