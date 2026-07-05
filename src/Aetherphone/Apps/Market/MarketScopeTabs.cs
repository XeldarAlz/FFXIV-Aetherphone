using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Market;

internal static class MarketScopeTabs
{
    private const float SegmentSmoothTime = 0.13f;

    private static readonly Dictionary<string, Spring> SegmentSprings = new(StringComparer.Ordinal);

    public static int Draw(string id, Rect row, IReadOnlyList<string> options, int selected, in AppPalette palette)
    {
        if (options.Count == 0)
        {
            return selected;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var height = 32f * scale;
        var pad = 4f * scale;
        var trackMin = new Vector2(row.Min.X + pad, row.Center.Y - height * 0.5f);
        var trackMax = new Vector2(row.Max.X - pad, row.Center.Y + height * 0.5f);
        var radius = height * 0.5f;
        Squircle.Fill(drawList, trackMin, trackMax, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
        var segmentWidth = (trackMax.X - trackMin.X) / options.Count;
        var position = AnimateSegment(id, selected);
        var inset = 2f * scale;
        var thumbCenterX = trackMin.X + (position + 0.5f) * segmentWidth;
        var thumbHalf = segmentWidth * 0.5f - inset;
        var thumbMin = new Vector2(thumbCenterX - thumbHalf, trackMin.Y + inset);
        var thumbMax = new Vector2(thumbCenterX + thumbHalf, trackMax.Y - inset);
        var thumbRadius = radius - inset;
        var shadow = new Vector2(0f, 1.5f * scale);
        drawList.AddRectFilled(thumbMin + shadow, thumbMax + shadow, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)),
            thumbRadius);
        Squircle.Fill(drawList, thumbMin, thumbMax, thumbRadius, ImGui.GetColorU32(palette.Accent));
        var gloss = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f));
        drawList.AddLine(new Vector2(thumbMin.X + thumbRadius, thumbMin.Y + 1f * scale),
            new Vector2(thumbMax.X - thumbRadius, thumbMin.Y + 1f * scale), gloss, 1f * scale);
        var result = selected;
        for (var index = 0; index < options.Count; index++)
        {
            var segmentMin = new Vector2(trackMin.X + index * segmentWidth, trackMin.Y);
            var segmentMax = new Vector2(trackMin.X + (index + 1) * segmentWidth, trackMax.Y);
            var center = new Vector2(trackMin.X + (index + 0.5f) * segmentWidth, row.Center.Y);
            var proximity = 1f - Math.Clamp(MathF.Abs(position - index), 0f, 1f);
            var color = Vector4.Lerp(palette.MutedInk, new Vector4(1f, 1f, 1f, 1f), proximity);
            Typography.DrawCentered(center, options[index], color, 0.84f, FontWeight.SemiBold);
            if (ImGui.IsMouseHoveringRect(segmentMin, segmentMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    result = index;
                }
            }
        }

        return result;
    }

    private static float AnimateSegment(string id, int selected)
    {
        if (!SegmentSprings.TryGetValue(id, out var spring))
        {
            spring = new Spring(selected);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var position = spring.Step(selected, SegmentSmoothTime, deltaSeconds);
        SegmentSprings[id] = spring;
        return position;
    }
}
