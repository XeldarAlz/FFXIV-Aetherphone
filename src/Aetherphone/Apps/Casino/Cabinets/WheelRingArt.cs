using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class WheelRingArt
{
    public static readonly Vector4[] SpotColors =
    {
        new(0.169f, 0.298f, 0.337f, 1f),
        new(0.212f, 0.392f, 0.639f, 1f),
        new(0.451f, 0.310f, 0.663f, 1f),
        new(0.808f, 0.451f, 0.208f, 1f),
        new(0.925f, 0.745f, 0.318f, 1f),
    };

    private static readonly Vector4 FeltDark = new(0.035f, 0.070f, 0.059f, 1f);
    private static readonly Vector4 HubFill = new(0.062f, 0.105f, 0.094f, 1f);
    private static readonly Vector4 LightInk = new(0.96f, 0.97f, 0.98f, 1f);
    private static readonly Vector4 DarkInk = new(0.09f, 0.10f, 0.08f, 1f);
    private static readonly Vector4 Brass = new(0.85f, 0.72f, 0.42f, 1f);
    private static readonly Vector4[] SpotInks =
    {
        InkOn(SpotColors[0]), InkOn(SpotColors[1]), InkOn(SpotColors[2]), InkOn(SpotColors[3]), InkOn(SpotColors[4]),
    };

    public static readonly Vector4[] SpotTextInks =
    {
        ReadableOnNight(SpotColors[0]), ReadableOnNight(SpotColors[1]), ReadableOnNight(SpotColors[2]),
        ReadableOnNight(SpotColors[3]), ReadableOnNight(SpotColors[4]),
    };

    private const int WedgeSegments = 6;
    private const float HubRadiusFactor = 0.34f;
    private const float LabelPadding = 3f;
    private const float LabelRimInset = 7f;
    private const float LabelChordFraction = 0.95f;
    private const float ChipTintAlpha = 0.32f;
    private const int LightenSteps = 10;

    private static readonly TextStyle LabelStyle = TextStyles.Title3;
    private static readonly float MinimumLabelScale = TextStyles.Caption2.Scale;
    private static readonly string[] SpotLabels = new string[WheelRules.SpotCount];

    private static LanguageInfo? spotLabelLanguage;

    public static Vector4 InkOn(Vector4 fill) =>
        StageContrast.Ratio(DarkInk, fill) > StageContrast.Ratio(LightInk, fill) ? DarkInk : LightInk;

    private static Vector4 ReadableOnNight(Vector4 color)
    {
        var ground = StageContrast.Over(color with { W = ChipTintAlpha }, HubFill);
        var ink = color;
        for (var step = 1; step <= LightenSteps && StageContrast.Ratio(ink, ground) < StageContrast.Readable; step++)
        {
            ink = Vector4.Lerp(color, LightInk, step / (float)LightenSteps);
        }

        return ink with { W = 1f };
    }

    public static Vector2 Direction(float angle)
    {
        return new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
    }

    private static float ImGuiAngle(float angle)
    {
        return angle - MathF.PI * 0.5f;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        drawList.AddCircleFilled(center, radius + 7f * scale, ImGui.GetColorU32(FeltDark), 64);
        drawList.AddCircle(center, radius + 7f * scale, ImGui.GetColorU32(Palette.WithAlpha(Brass, 0.55f)), 64,
            1.5f * scale);

        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var spot = WheelRules.Segments[segment];
            var fill = SpotColors[spot];
            if (segment == highlightSegment && highlightGlow > 0f)
            {
                fill = Vector4.Lerp(fill, LightInk, 0.42f * highlightGlow);
            }

            var centreAngle = rotation + segment * WheelChoreography.SegmentSpan;
            var from = ImGuiAngle(centreAngle - WheelChoreography.SegmentSpan * 0.5f);
            var to = ImGuiAngle(centreAngle + WheelChoreography.SegmentSpan * 0.5f);
            drawList.PathClear();
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, from, to, WedgeSegments);
            drawList.PathFillConvex(ImGui.GetColorU32(fill));
        }

        DrawTicks(drawList, center, radius, rotation, scale);
        DrawLabels(drawList, center, radius, rotation, highlightSegment, highlightGlow, scale);
        drawList.AddCircleFilled(center, radius * HubRadiusFactor, ImGui.GetColorU32(HubFill), 48);
        drawList.AddCircle(center, radius * HubRadiusFactor, ImGui.GetColorU32(Palette.WithAlpha(Brass, 0.45f)), 48,
            1.2f * scale);
    }

    private static void DrawTicks(ImDrawListPtr drawList, Vector2 center, float radius, float rotation, float scale)
    {
        var tint = ImGui.GetColorU32(Palette.WithAlpha(FeltDark, 0.75f));
        var inner = radius * 0.36f;
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var boundary = rotation + (segment - 0.5f) * WheelChoreography.SegmentSpan;
            var direction = Direction(boundary);
            drawList.AddLine(center + direction * inner, center + direction * radius, tint, 1f * scale);
        }
    }

    private static void RefreshSpotLabels()
    {
        if (ReferenceEquals(spotLabelLanguage, Loc.Current))
        {
            return;
        }

        spotLabelLanguage = Loc.Current;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            SpotLabels[spot] = Loc.T(L.Casino.WheelMultiplier, GameNumber.Label(WheelRules.Multipliers[spot]));
        }
    }

    private static void DrawLabels(ImDrawListPtr drawList, Vector2 center, float radius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        RefreshSpotLabels();
        var hubEdge = radius * HubRadiusFactor + LabelPadding * scale;
        var rimEdge = radius - LabelRimInset * scale;
        var chordPerRadius = 2f * MathF.Sin(WheelChoreography.SegmentSpan * 0.5f) * LabelChordFraction;
        var weight = LabelStyle.Weight;
        var textScale = LabelStyle.Scale;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            var unit = Typography.Measure(SpotLabels[spot], 1f, weight);
            if (unit.X <= 0f || unit.Y <= 0f)
            {
                continue;
            }

            textScale = MathF.Min(textScale, (rimEdge - hubEdge) / unit.X);
            textScale = MathF.Min(textScale, chordPerRadius * rimEdge / (unit.Y + chordPerRadius * unit.X));
        }

        if (textScale < MinimumLabelScale)
        {
            return;
        }

        Span<Vector2> sizes = stackalloc Vector2[WheelRules.SpotCount];
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            sizes[spot] = Typography.Measure(SpotLabels[spot], textScale, weight);
        }

        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var spot = WheelRules.Segments[segment];
            var ink = segment == highlightSegment && highlightGlow > 0f
                ? InkOn(Vector4.Lerp(SpotColors[spot], LightInk, 0.42f * highlightGlow))
                : SpotInks[spot];
            var centreAngle = rotation + segment * WheelChoreography.SegmentSpan;
            var pivot = center + Direction(centreAngle) * (rimEdge - sizes[spot].X * 0.5f);
            DrawRadialLabel(drawList, pivot, centreAngle - MathF.PI * 0.5f, SpotLabels[spot], sizes[spot],
                ink, textScale, weight);
        }
    }

    private static void DrawRadialLabel(ImDrawListPtr drawList, Vector2 pivot, float angle, string label,
        Vector2 size, Vector4 ink, float textScale, FontWeight weight)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        Typography.Draw(drawList, pivot - size * 0.5f, label, ink, textScale, weight);
        var sine = MathF.Sin(angle);
        var cosine = MathF.Cos(angle);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = firstVertex; vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            var offset = vertex.Pos - pivot;
            vertex.Pos = new Vector2(pivot.X + offset.X * cosine - offset.Y * sine,
                pivot.Y + offset.X * sine + offset.Y * cosine);
        }
    }

    public static void DrawPointer(ImDrawListPtr drawList, Vector2 center, float radius, float scale,
        float deflection = 0f)
    {
        var back = new Vector2(center.X, center.Y - radius - 13f * scale);
        var length = 18f * scale;
        var width = 7f * scale;
        var along = new Vector2(MathF.Sin(deflection), MathF.Cos(deflection));
        var across = new Vector2(along.Y, -along.X);
        var tip = back + along * length;
        drawList.AddTriangleFilled(tip, back - across * width, back + across * width, ImGui.GetColorU32(Brass));
        drawList.AddCircleFilled(back + along * 2f * scale, 4f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(LightInk, 0.85f)), 16);
    }

    public static void DrawCountdown(ImDrawListPtr drawList, Vector2 center, float radius, float fraction,
        Vector4 color, float scale)
    {
        var track = ImGui.GetColorU32(Palette.WithAlpha(color, 0.16f));
        drawList.AddCircle(center, radius, track, 96, 3f * scale);
        if (fraction <= 0f)
        {
            return;
        }

        if (fraction > 1f)
        {
            fraction = 1f;
        }

        var steps = Math.Max(4, (int)MathF.Ceiling(fraction * 96f));
        var tint = ImGui.GetColorU32(color);
        var previous = center + Direction(0f) * radius;
        for (var step = 1; step <= steps; step++)
        {
            var angle = fraction * WheelChoreography.Tau * (step / (float)steps);
            var next = center + Direction(angle) * radius;
            drawList.AddLine(previous, next, tint, 3f * scale);
            previous = next;
        }
    }
}
