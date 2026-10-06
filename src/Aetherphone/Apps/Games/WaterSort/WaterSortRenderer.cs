using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.WaterSort;

internal struct WaterSortPour
{
    public bool Active;
    public int FromTube;
    public int ToTube;
    public int Color;
    public int Count;
    public float Progress;
    public bool Splashed;
}

internal sealed class WaterSortRenderer
{
    public const float StreamArrive = 0.34f;
    private const float StreamRetract = 0.82f;
    private const float StreamEnd = 0.97f;
    private const float StreamLead = 0.04f;
    private const int StreamSamples = 14;
    private const float RiseDistance = 36f;
    private const float RiseOverlap = 0.6f;
    private const float PourNudge = 14f;
    private const float InnerTopFraction = 0.08f;
    private const float InnerShadowFraction = 0.2f;
    private static readonly Vector4 Glass = new(0.06f, 0.05f, 0.07f, 0.55f);
    private static readonly Vector4 GlassShadow = new(0f, 0f, 0f, 0.28f);
    private static readonly Vector4 InnerShadow = new(0f, 0f, 0f, 0.22f);
    private static readonly Vector4 Highlight = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 Rim = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 Mouth = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 StreamCore = new(1f, 1f, 1f, 0.45f);

    // Okabe-Ito colorblind-safe palette plus white and maroon; every entry also differs in lightness
    private static readonly Vector4[] LiquidColors =
    {
        new(0.10f, 0.48f, 0.82f, 1f),
        new(0.96f, 0.88f, 0.25f, 1f),
        new(0.86f, 0.24f, 0.22f, 1f),
        new(0.00f, 0.66f, 0.47f, 1f),
        new(0.94f, 0.62f, 0.05f, 1f),
        new(0.36f, 0.74f, 0.94f, 1f),
        new(0.87f, 0.48f, 0.70f, 1f),
        new(0.85f, 0.87f, 0.92f, 1f),
        new(0.47f, 0.16f, 0.22f, 1f),
    };

    public static Vector4 ColorOf(int color) => LiquidColors[color % LiquidColors.Length];

    public static Rect TubeRect(Rect area, int index, int tubeCount, float scale)
    {
        var rowCount = tubeCount <= 5 ? 1 : 2;
        var firstRow = (tubeCount + rowCount - 1) / rowCount;
        var row = index < firstRow ? 0 : 1;
        var indexInRow = row == 0 ? index : index - firstRow;
        var tubesInRow = row == 0 ? firstRow : tubeCount - firstRow;
        var slotWidth = area.Width / tubesInRow;
        var rowHeight = area.Height / rowCount;
        var centerX = area.Min.X + slotWidth * (indexInRow + 0.5f);
        var centerY = area.Min.Y + rowHeight * (row + 0.5f);
        var tubeWidth = MathF.Min(slotWidth * 0.6f, rowHeight * 0.3f);
        var tubeHeight = rowHeight * 0.76f;
        var min = new Vector2(centerX - tubeWidth * 0.5f, centerY - tubeHeight * 0.5f);
        var max = new Vector2(centerX + tubeWidth * 0.5f, centerY + tubeHeight * 0.5f);
        return new Rect(min, max);
    }

    public static Vector2 Spout(Rect tube) => new(tube.Center.X, tube.Min.Y + tube.Height * InnerTopFraction);

    public static Vector2 Surface(Rect tube, int filled)
    {
        var wall = Wall(tube);
        var innerTop = tube.Min.Y + tube.Height * InnerTopFraction;
        var innerBottom = tube.Max.Y - wall;
        var segmentHeight = (innerBottom - innerTop) / WaterSortBoard.Capacity;
        return new Vector2(tube.Center.X, innerBottom - filled * segmentHeight);
    }

    public void Draw(WaterSortBoard board, Rect area, float scale, PhoneTheme theme, float selectedLift,
        float entrance, in WaterSortPour pour, ReadOnlySpan<float> sortedGlow)
    {
        var drawList = ImGui.GetWindowDrawList();
        var tubeCount = board.TubeCount;
        var pourTarget = pour.Active ? TubeRect(area, pour.ToTube, tubeCount, scale) : default;
        for (var tube = 0; tube < tubeCount; tube++)
        {
            var rect = TubeRect(area, tube, tubeCount, scale);
            var rise = (1f - Easing.EaseOutCubic(GameJuice.Stagger(entrance, tube, tubeCount, RiseOverlap))) *
                       RiseDistance * scale;
            var offset = new Vector2(0f, rise);
            var pouring = pour.Active && tube == pour.FromTube;
            var lifted = tube == board.Selected || pouring;
            if (lifted)
            {
                offset.Y -= selectedLift;
            }

            if (pouring)
            {
                var toward = MathF.Sign(pourTarget.Center.X - rect.Center.X);
                offset.X += toward * MathF.Sin(pour.Progress * MathF.PI) * PourNudge * scale;
            }

            rect = rect.Translate(offset);
            DrawTube(drawList, board, tube, rect, scale, theme, lifted, sortedGlow[tube], in pour);
        }

        if (pour.Active)
        {
            DrawStream(drawList, board, area, scale, selectedLift, in pour);
        }
    }

    private static float Wall(Rect rect) => MathF.Max(1.5f, rect.Width * 0.06f);

    private static void DrawTube(ImDrawListPtr drawList, WaterSortBoard board, int tube, Rect rect, float scale,
        PhoneTheme theme, bool lifted, float sortedGlow, in WaterSortPour pour)
    {
        var rounding = rect.Width * 0.5f;
        var wall = Wall(rect);
        var count = board.Count(tube);
        var topColor = count > 0 ? ColorOf(board.TopColor(tube)) : theme.Accent;
        if (sortedGlow > 0.01f)
        {
            ProgressRing.Glow(rect.Center, rect.Width * 1.1f, topColor, 0.8f * sortedGlow);
        }

        if (lifted)
        {
            ProgressRing.Glow(new Vector2(rect.Center.X, rect.Max.Y - rect.Height * 0.3f), rect.Width * 0.9f,
                theme.Accent, 0.6f);
        }

        var shadowOffset = new Vector2(0f, 2f * scale);
        drawList.AddRectFilled(rect.Min + shadowOffset, rect.Max + shadowOffset, ImGui.GetColorU32(GlassShadow), rounding,
            ImDrawFlags.RoundCornersBottom);
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Glass), rounding, ImDrawFlags.RoundCornersBottom);
        var innerTop = rect.Min.Y + rect.Height * InnerTopFraction;
        var innerBottom = rect.Max.Y - wall;
        var segmentHeight = (innerBottom - innerTop) / WaterSortBoard.Capacity;
        var left = rect.Min.X + wall;
        var right = rect.Max.X - wall;
        var pouringFrom = pour.Active && tube == pour.FromTube;
        var pouringTo = pour.Active && tube == pour.ToTube;
        var fill = pour.Active ? Easing.Segment(pour.Progress, StreamArrive, 1f) : 1f;
        var settledCount = pouringTo ? count - pour.Count : count;
        for (var level = 0; level < count; level++)
        {
            var color = board.Segment(tube, level);
            if (color < 0)
            {
                continue;
            }

            var bandBottom = innerBottom - level * segmentHeight;
            var height = segmentHeight;
            if (pouringTo && level >= settledCount)
            {
                height *= fill;
            }

            if (height <= 0.5f)
            {
                continue;
            }

            var bandMin = new Vector2(left, bandBottom - height);
            var bandMax = new Vector2(right, bandBottom);
            var flags = level == 0 ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone;
            drawList.AddRectFilled(bandMin, bandMax, ImGui.GetColorU32(ColorOf(color)), level == 0 ? rounding - wall : 0f,
                flags);
        }

        if (pouringFrom)
        {
            var draining = pour.Count * segmentHeight * (1f - fill);
            if (draining > 0.5f)
            {
                var bandBottom = innerBottom - count * segmentHeight;
                var flags = count == 0 ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone;
                drawList.AddRectFilled(new Vector2(left, bandBottom - draining), new Vector2(right, bandBottom),
                    ImGui.GetColorU32(ColorOf(pour.Color)), count == 0 ? rounding - wall : 0f, flags);
            }
        }

        var shadowBottom = innerTop + (innerBottom - innerTop) * InnerShadowFraction;
        var shadowTop = ImGui.GetColorU32(InnerShadow);
        var shadowClear = ImGui.GetColorU32(InnerShadow with { W = 0f });
        drawList.AddRectFilledMultiColor(new Vector2(left, innerTop), new Vector2(right, shadowBottom), shadowTop,
            shadowTop, shadowClear, shadowClear);
        drawList.AddRectFilled(new Vector2(left, innerTop), new Vector2(left + wall * 0.9f, innerBottom),
            ImGui.GetColorU32(Highlight), 0f);
        var rim = lifted ? theme.Accent : Vector4.Lerp(Rim, topColor, sortedGlow);
        drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(rim), rounding, ImDrawFlags.RoundCornersBottom,
            (lifted || sortedGlow > 0.5f ? 2.2f : 1.4f) * scale);
        drawList.AddLine(new Vector2(rect.Min.X, rect.Min.Y), new Vector2(rect.Max.X, rect.Min.Y),
            ImGui.GetColorU32(Mouth), 2f * scale);
    }

    private static void DrawStream(ImDrawListPtr drawList, WaterSortBoard board, Rect area, float scale,
        float selectedLift, in WaterSortPour pour)
    {
        var progress = pour.Progress;
        if (progress <= StreamLead || progress >= StreamEnd)
        {
            return;
        }

        var tubeCount = board.TubeCount;
        var from = TubeRect(area, pour.FromTube, tubeCount, scale);
        var to = TubeRect(area, pour.ToTube, tubeCount, scale);
        var toward = MathF.Sign(to.Center.X - from.Center.X);
        var spout = Spout(from) + new Vector2(toward * (from.Width * 0.5f + MathF.Sin(progress * MathF.PI) * PourNudge * scale),
            -selectedLift);
        var surface = Surface(to, board.Count(pour.ToTube) - pour.Count);
        var head = Easing.Segment(progress, StreamLead, StreamArrive);
        var tail = Easing.Segment(progress, StreamRetract, StreamEnd);
        if (tail >= head)
        {
            return;
        }

        var control = new Vector2((spout.X + surface.X) * 0.5f, MathF.Min(spout.Y, surface.Y) - from.Height * 0.12f);
        var color = ColorOf(pour.Color);
        var outer = ImGui.GetColorU32(color with { W = 0.9f });
        var core = ImGui.GetColorU32(StreamCore);
        var previous = Bezier(spout, control, surface, tail);
        for (var sample = 1; sample <= StreamSamples; sample++)
        {
            var at = tail + (head - tail) * (sample / (float)StreamSamples);
            var point = Bezier(spout, control, surface, at);
            drawList.AddLine(previous, point, outer, 3.6f * scale);
            drawList.AddLine(previous, point, core, 1.2f * scale);
            previous = point;
        }

        drawList.AddCircleFilled(previous, 3.2f * scale, ImGui.GetColorU32(color));
    }

    private static Vector2 Bezier(Vector2 start, Vector2 control, Vector2 end, float at)
    {
        var inverse = 1f - at;
        return start * (inverse * inverse) + control * (2f * inverse * at) + end * (at * at);
    }
}
