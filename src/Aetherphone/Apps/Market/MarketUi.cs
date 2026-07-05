using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Market;

internal sealed class MarketUi
{
    public static readonly Vector4 Accent = new(0.92f, 0.62f, 0.18f, 1f);
    public static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);
    public static readonly Vector4 TitleInk = new(0.99f, 0.97f, 0.93f, 1f);
    public static readonly Vector4 BodyInk = new(0.95f, 0.90f, 0.84f, 0.96f);
    public static readonly Vector4 MutedInk = new(0.78f, 0.72f, 0.62f, 0.85f);
    public static readonly Vector4 HeaderInk = new(0.99f, 0.88f, 0.65f, 0.95f);

    private static readonly Vector4 BackdropTop = new(0.10f, 0.09f, 0.18f, 1f);
    private static readonly Vector4 BackdropBottom = new(0.04f, 0.03f, 0.08f, 1f);
    private static readonly Vector4 BloomTop = new(0.85f, 0.55f, 0.15f, 0.24f);
    private static readonly Vector4 BloomBottom = new(0.40f, 0.25f, 0.08f, 0f);

    public static readonly Vector4 Surface = new(1f, 1f, 1f, 0.05f);
    public static readonly Vector4 SurfaceStroke = new(1f, 1f, 1f, 0.06f);
    public static readonly Vector4 FieldSurface = new(1f, 1f, 1f, 0.10f);

    private const float SegmentSmoothTime = 0.13f;
    private static readonly Dictionary<string, Spring> SegmentSprings = new(StringComparer.Ordinal);

    public PhoneTheme Theme { get; set; } = PhoneTheme.Default;

    public void Backdrop(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        PaintGradient(ImGui.GetWindowDrawList(), screen, screen, Theme.ScreenRounding * scale);
    }

    public void Body(Rect area)
    {
        var frame = SceneChrome.ScreenFrom(area, Theme, ImGuiHelpers.GlobalScale);
        PaintGradient(ImGui.GetWindowDrawList(), area, frame, 0f);
    }

    public static void PaintGradient(ImDrawListPtr drawList, Rect target, Rect frame, float rounding)
    {
        var topFraction = frame.Height <= 0f ? 0f : (target.Min.Y - frame.Min.Y) / frame.Height;
        var bottomFraction = frame.Height <= 0f ? 1f : (target.Max.Y - frame.Min.Y) / frame.Height;
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(BackdropTop, BackdropBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(BackdropTop, BackdropBottom, bottomFraction)));
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(BloomTop, BloomBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(BloomTop, BloomBottom, bottomFraction)));
    }

    public void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding)
    {
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Surface));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(SurfaceStroke), 1f);
    }

    public bool PillButton(Rect rect, string label, bool filled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;

        var fill = filled
            ? (hovered ? Palette.Mix(Accent, Theme.TextStrong, 0.12f) : Accent)
            : (hovered ? new Vector4(1f, 1f, 1f, 0.16f) : FieldSurface);
        var ink = filled ? new Vector4(1f, 1f, 1f, 1f) : TitleInk;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));

        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static void SectionLabel(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        var topPad = 14f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + topPad));

        var barWidth = 3f * scale;
        var barHeight = 14f * scale;
        Squircle.Fill(drawList, new Vector2(origin.X, origin.Y + topPad + 2f * scale), new Vector2(origin.X + barWidth, origin.Y + topPad + 2f * scale + barHeight), barWidth * 0.5f, ImGui.GetColorU32(Accent));
        Typography.Draw(new Vector2(origin.X + barWidth + 9f * scale, origin.Y + topPad), label, new Vector4(0.99f, 0.92f, 0.78f, 1f), 0.95f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, topPad + barHeight + 8f * scale));
    }

    public int DrawScopeTabs(string id, Rect row, IReadOnlyList<string> options, int selected)
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
        drawList.AddRectFilled(thumbMin + shadow, thumbMax + shadow, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)), thumbRadius);
        Squircle.Fill(drawList, thumbMin, thumbMax, thumbRadius, ImGui.GetColorU32(Accent));

        var gloss = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f));
        drawList.AddLine(new Vector2(thumbMin.X + thumbRadius, thumbMin.Y + 1f * scale), new Vector2(thumbMax.X - thumbRadius, thumbMin.Y + 1f * scale), gloss, 1f * scale);

        var result = selected;
        for (var index = 0; index < options.Count; index++)
        {
            var segmentMin = new Vector2(trackMin.X + index * segmentWidth, trackMin.Y);
            var segmentMax = new Vector2(trackMin.X + (index + 1) * segmentWidth, trackMax.Y);

            var center = new Vector2(trackMin.X + (index + 0.5f) * segmentWidth, row.Center.Y);
            var proximity = 1f - Math.Clamp(MathF.Abs(position - index), 0f, 1f);
            var color = Vector4.Lerp(MutedInk, new Vector4(1f, 1f, 1f, 1f), proximity);
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

    public static bool HoverClick(Vector2 min, Vector2 max)
    {
        if (!ImGui.IsMouseHoveringRect(min, max))
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
