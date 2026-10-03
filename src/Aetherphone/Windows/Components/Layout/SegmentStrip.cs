using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SegmentStrip
{
    private const float TrackHeight = 30f;
    private static readonly Vector4 FrostTrack = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<string, Spring> Thumbs = new(StringComparer.Ordinal);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, PhoneTheme theme) =>
        Draw(id, row, options, selected, theme, out _);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, PhoneTheme theme,
        out bool pressed) =>
        Draw(id, row, options, selected, theme.ToggleOff, theme.Accent, theme.TextMuted, theme.TextStrong,
            out pressed);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, in AppPalette palette,
        float trackHeight = TrackHeight, float textScale = 0.82f) =>
        Draw(id, row, options, selected, FrostTrack, palette.Accent, palette.MutedInk, White, out _, trackHeight,
            textScale);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, Vector4 track,
        Vector4 accent, Vector4 mutedInk, Vector4 activeInk, float trackHeight = TrackHeight, float textScale = 0.82f) =>
        Draw(id, row, options, selected, track, accent, mutedInk, activeInk, out _, trackHeight, textScale);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, Vector4 track,
        Vector4 accent, Vector4 mutedInk, Vector4 activeInk, out bool pressed, float trackHeight = TrackHeight,
        float textScale = 0.82f)
    {
        pressed = false;
        if (options.Count == 0)
        {
            return selected;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var height = trackHeight * scale;
        var trackMin = new Vector2(row.Min.X, row.Center.Y - height * 0.5f);
        var trackMax = new Vector2(row.Max.X, row.Center.Y + height * 0.5f);
        var radius = height * 0.5f;
        drawList.AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(track), radius);
        var segmentWidth = (trackMax.X - trackMin.X) / options.Count;
        var result = selected;
        for (var index = 0; index < options.Count; index++)
        {
            var segmentMin = new Vector2(trackMin.X + index * segmentWidth, trackMin.Y);
            var segmentMax = new Vector2(trackMin.X + (index + 1) * segmentWidth, trackMax.Y);
            var segmentHovered = UiInteract.Hover(segmentMin, segmentMax);
            if (segmentHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(segmentMin, segmentMax, segmentHovered))
            {
                result = index;
                pressed = true;
            }
        }

        var position = AnimateThumb(id, selected);
        var inset = 2f * scale;
        var thumbCenterX = trackMin.X + (position + 0.5f) * segmentWidth;
        var thumbHalf = segmentWidth * 0.5f - inset;
        var thumbMin = new Vector2(thumbCenterX - thumbHalf, trackMin.Y + inset);
        var thumbMax = new Vector2(thumbCenterX + thumbHalf, trackMax.Y - inset);
        var thumbRadius = radius - inset;
        var shadow = new Vector2(0f, 1.5f * scale);
        drawList.AddRectFilled(thumbMin + shadow, thumbMax + shadow, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)),
            thumbRadius);
        Squircle.Fill(drawList, thumbMin, thumbMax, thumbRadius, ImGui.GetColorU32(accent));
        var gloss = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f));
        drawList.AddLine(new Vector2(thumbMin.X + thumbRadius, thumbMin.Y + 1f * scale),
            new Vector2(thumbMax.X - thumbRadius, thumbMin.Y + 1f * scale), gloss, 1f * scale);
        var labelMaxWidth = MathF.Max(1f, segmentWidth - 8f * scale);
        for (var index = 0; index < options.Count; index++)
        {
            var segmentMin = new Vector2(trackMin.X + index * segmentWidth, trackMin.Y);
            var segmentMax = new Vector2(trackMin.X + (index + 1) * segmentWidth, trackMax.Y);
            var center = new Vector2(trackMin.X + (index + 0.5f) * segmentWidth, row.Center.Y);
            var proximity = 1f - Math.Clamp(MathF.Abs(position - index), 0f, 1f);
            var color = Vector4.Lerp(mutedInk, activeInk, proximity);
            var hovering = UiInteract.Hover(segmentMin, segmentMax);
            var labelHeight = Typography.Measure(options[index], textScale, FontWeight.SemiBold).Y;
            Marquee.DrawCentered(id + "." + index, options[index], center.X, center.Y - labelHeight * 0.5f,
                labelMaxWidth, new TextStyle(textScale, FontWeight.SemiBold), color, hovering);
        }

        return result;
    }

    public static Rect SegmentRect(Rect row, int index, int count, float trackHeight = TrackHeight)
    {
        var height = trackHeight * UiScale.Current;
        var segmentWidth = row.Width / Math.Max(1, count);
        var left = row.Min.X + index * segmentWidth;
        return new Rect(new Vector2(left, row.Center.Y - height * 0.5f),
            new Vector2(left + segmentWidth, row.Center.Y + height * 0.5f));
    }

    private static float AnimateThumb(string id, int selected)
    {
        if (!Thumbs.TryGetValue(id, out var spring))
        {
            spring = new Spring(selected);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var position = spring.Step(selected, Motion.Release, deltaSeconds);
        Thumbs[id] = spring;
        return position;
    }
}
