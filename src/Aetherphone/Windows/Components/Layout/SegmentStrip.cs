using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SegmentStrip
{
    private const float TrackHeight = 30f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<string, Spring> Thumbs = new(StringComparer.Ordinal);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, PhoneTheme theme) =>
        Draw(id, row, options, selected, theme, out _);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, PhoneTheme theme,
        out bool pressed) =>
        Draw(id, row, options, selected, Surfaces.Fill(theme.TextStrong, FillLevel.Tertiary), theme.Accent,
            theme.TextMuted, White, out pressed);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, in AppPalette palette,
        float trackHeight = TrackHeight, float textScale = 0.82f, bool overlay = false) =>
        Draw(id, row, options, selected, Surfaces.Fill(palette.TitleInk, FillLevel.Tertiary), palette.Accent,
            palette.MutedInk, White, out _, trackHeight, textScale, overlay);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, Vector4 track,
        Vector4 accent, Vector4 mutedInk, Vector4 activeInk, float trackHeight = TrackHeight, float textScale = 0.82f,
        bool overlay = false) =>
        Draw(id, row, options, selected, track, accent, mutedInk, activeInk, out _, trackHeight, textScale, overlay);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, Vector4 track,
        Vector4 accent, Vector4 mutedInk, Vector4 activeInk, out bool pressed, float trackHeight = TrackHeight,
        float textScale = 0.82f, bool overlay = false)
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
        Squircle.Fill(drawList, trackMin, trackMax, radius, ImGui.GetColorU32(track));
        var segmentWidth = (trackMax.X - trackMin.X) / options.Count;
        var result = selected;
        for (var index = 0; index < options.Count; index++)
        {
            var segmentMin = new Vector2(trackMin.X + index * segmentWidth, trackMin.Y);
            var segmentMax = new Vector2(trackMin.X + (index + 1) * segmentWidth, trackMax.Y);
            var segmentHovered = overlay
                ? UiInteract.HoverWindowOnly(segmentMin, segmentMax)
                : UiInteract.Hover(segmentMin, segmentMax);
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
        Squircle.Fill(drawList, thumbMin, thumbMax, thumbRadius, ImGui.GetColorU32(accent with { W = 1f }));
        var labelMaxWidth = MathF.Max(1f, segmentWidth - Metrics.Space.Sm * scale);
        var labelStyle = new TextStyle(textScale, FontWeight.SemiBold);
        for (var index = 0; index < options.Count; index++)
        {
            var segmentMin = new Vector2(trackMin.X + index * segmentWidth, trackMin.Y);
            var segmentMax = new Vector2(trackMin.X + (index + 1) * segmentWidth, trackMax.Y);
            var center = new Vector2(trackMin.X + (index + 0.5f) * segmentWidth, row.Center.Y);
            var proximity = 1f - Math.Clamp(MathF.Abs(position - index), 0f, 1f);
            var color = Vector4.Lerp(mutedInk, activeInk, proximity);
            var hovering = UiInteract.Hover(segmentMin, segmentMax);
            var labelHeight = Typography.Measure(options[index], labelStyle).Y;
            Marquee.DrawCentered(new MarqueeId(id, index), options[index], center.X, center.Y - labelHeight * 0.5f,
                labelMaxWidth, labelStyle, color, hovering);
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
