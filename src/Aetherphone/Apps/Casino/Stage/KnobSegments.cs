using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal static class KnobSegmentsLayout
{
    public const float LabelPad = 14f;
    public const float MinimumSegment = 44f;
    public const float FitSlack = 0.5f;
    public const int MaxSegments = 8;

    public static float Natural(float labelWidth, float scale) =>
        MathF.Max(MinimumSegment * scale, labelWidth + LabelPad * 2f * scale);

    public static float Total(ReadOnlySpan<float> labelWidths, float scale)
    {
        var total = 0f;
        for (var index = 0; index < labelWidths.Length; index++)
        {
            total += Natural(labelWidths[index], scale);
        }

        return total;
    }

    public static bool Fits(ReadOnlySpan<float> labelWidths, float rowWidth, float scale) =>
        Total(labelWidths, scale) <= rowWidth + FitSlack;

    public static bool Distribute(ReadOnlySpan<float> labelWidths, float rowWidth, float scale, Span<float> widths)
    {
        var count = labelWidths.Length;
        if (count == 0)
        {
            return true;
        }

        var total = Total(labelWidths, scale);
        var fits = total <= rowWidth + FitSlack;
        var slack = fits ? MathF.Max(0f, rowWidth - total) / count : 0f;
        var shrink = fits || total <= 0f ? 1f : rowWidth / total;
        for (var index = 0; index < count; index++)
        {
            widths[index] = Natural(labelWidths[index], scale) * shrink + slack;
        }

        return fits;
    }
}

internal static class KnobText
{
    private const float CompactCeiling = 31f;

    public static TextStyle Style(float heightPixels) =>
        heightPixels / MathF.Max(UiScale.Current, 0.0001f) <= CompactCeiling
            ? TextStyles.FootnoteEmphasized
            : TextStyles.SubheadlineEmphasized;
}

internal static class KnobSegments
{
    private const float ThumbInset = 2f;
    private const float LockedAlpha = 0.5f;

    private static readonly Dictionary<string, Spring> Thumbs = new(StringComparer.Ordinal);

    public static TextStyle LabelStyle(float height) => KnobText.Style(height);

    public static float Width(ReadOnlySpan<string> labels, float height, float scale)
    {
        var style = LabelStyle(height);
        var total = 0f;
        for (var index = 0; index < labels.Length; index++)
        {
            total += KnobSegmentsLayout.Natural(Typography.Measure(labels[index], style).X, scale);
        }

        return total;
    }

    public static bool Fits(ReadOnlySpan<string> labels, float width, float height, float scale) =>
        Width(labels, height, scale) <= width + KnobSegmentsLayout.FitSlack;

    public static int Draw(ImDrawListPtr drawList, string id, Rect rect, ReadOnlySpan<string> labels, int selected,
        AppSkin ui, bool enabled)
    {
        var count = Math.Min(labels.Length, KnobSegmentsLayout.MaxSegments);
        if (count == 0 || rect.Width <= 0f || rect.Height <= 0f)
        {
            return selected;
        }

        var scale = UiScale.Current;
        var style = LabelStyle(rect.Height);
        Span<float> labelWidths = stackalloc float[KnobSegmentsLayout.MaxSegments];
        Span<float> widths = stackalloc float[KnobSegmentsLayout.MaxSegments];
        Span<float> lefts = stackalloc float[KnobSegmentsLayout.MaxSegments];
        for (var index = 0; index < count; index++)
        {
            labelWidths[index] = Typography.Measure(labels[index], style).X;
        }

        KnobSegmentsLayout.Distribute(labelWidths[..count], rect.Width, scale, widths[..count]);
        var cursor = rect.Min.X;
        for (var index = 0; index < count; index++)
        {
            lefts[index] = cursor;
            cursor += widths[index];
        }

        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        var result = selected;
        for (var index = 0; index < count; index++)
        {
            var segmentMin = new Vector2(lefts[index], rect.Min.Y);
            var segmentMax = new Vector2(lefts[index] + widths[index], rect.Max.Y);
            var hovered = enabled && UiInteract.Hover(segmentMin, segmentMax);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (enabled && UiInteract.Click(segmentMin, segmentMax, hovered))
            {
                result = index;
            }
        }

        var clamped = Math.Clamp(selected, 0, count - 1);
        var position = Math.Clamp(Animate(id, clamped), 0f, count - 1);
        var lower = (int)MathF.Floor(position);
        var upper = Math.Min(lower + 1, count - 1);
        var fraction = position - lower;
        var inset = ThumbInset * scale;
        var thumbLeft = Lerp(lefts[lower], lefts[upper], fraction) + inset;
        var thumbRight = Lerp(lefts[lower] + widths[lower], lefts[upper] + widths[upper], fraction) - inset;
        var alpha = enabled ? 1f : LockedAlpha;
        Squircle.Fill(drawList, new Vector2(thumbLeft, rect.Min.Y + inset), new Vector2(thumbRight, rect.Max.Y - inset),
            radius - inset, ImGui.GetColorU32(ui.Accent with { W = alpha }));
        var pad = KnobSegmentsLayout.LabelPad * scale;
        for (var index = 0; index < count; index++)
        {
            var proximity = 1f - Math.Clamp(MathF.Abs(position - index), 0f, 1f);
            var ink = Vector4.Lerp(CasinoColors.InkBody, CasinoColors.InkTitle, proximity);
            var label = Typography.FitText(labels[index], MathF.Max(1f, widths[index] - pad), style);
            Typography.DrawCentered(drawList, new Vector2(lefts[index] + widths[index] * 0.5f, rect.Center.Y), label,
                ink with { W = ink.W * alpha }, style);
        }

        return enabled ? result : selected;
    }

    private static float Animate(string id, int selected)
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

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
}

internal static class KnobPicker
{
    public const float Pad = 14f;
    public const float Gap = 6f;
    public const float Chevron = 12f;
    public const float MinimumWidth = 44f;

    public static TextStyle ValueStyle(float height) => KnobText.Style(height);

    public static TextStyle CaptionStyle(float height) => ValueStyle(height) with { Weight = FontWeight.Regular };

    public static float Width(string caption, string value, float height, float scale)
    {
        var captionWidth = caption.Length > 0 ? Typography.Measure(caption, CaptionStyle(height)).X + Gap * scale : 0f;
        var valueWidth = Typography.Measure(value, ValueStyle(height)).X;
        return MathF.Max(MinimumWidth * scale,
            Pad * 2f * scale + captionWidth + valueWidth + Gap * scale + Chevron * scale);
    }

    public static bool Draw(ImDrawListPtr drawList, string id, Rect rect, string caption, string value, AppSkin ui,
        bool enabled, bool open)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return false;
        }

        var scale = UiScale.Current;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink, ButtonStyle.Gray, ButtonRole.Normal, enabled, hovered,
            ImGui.GetID(id));
        var captionStyle = CaptionStyle(rect.Height);
        var valueStyle = ValueStyle(rect.Height);
        var gap = Gap * scale;
        var chevron = Chevron * scale;
        var captionWidth = caption.Length > 0 ? Typography.Measure(caption, captionStyle).X : 0f;
        var room = MathF.Max(1f, face.Face.Width - Pad * 2f * scale - chevron - gap
            - (captionWidth > 0f ? captionWidth + gap : 0f));
        var shown = Typography.FitText(value, room, valueStyle);
        var valueWidth = Typography.Measure(shown, valueStyle).X;
        var content = (captionWidth > 0f ? captionWidth + gap : 0f) + valueWidth + gap + chevron;
        var left = face.Face.Center.X - content * 0.5f;
        var centerY = face.Face.Center.Y;
        if (captionWidth > 0f)
        {
            var captionHeight = Typography.LineHeight(captionStyle);
            Typography.Draw(drawList, new Vector2(left, centerY - captionHeight * 0.5f), caption,
                CasinoColors.InkBody with { W = face.LabelInk.W }, captionStyle);
            left += captionWidth + gap;
        }

        var valueHeight = Typography.LineHeight(valueStyle);
        Typography.Draw(drawList, new Vector2(left, centerY - valueHeight * 0.5f), shown, face.LabelInk, valueStyle);
        left += valueWidth + gap;
        PhoneIcon.Draw(drawList, new Vector2(left + chevron * 0.5f, centerY),
            open ? PhoneIcons.ChevronUp : PhoneIcons.ChevronDown, face.LabelInk, chevron);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
