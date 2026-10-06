using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Flow;

internal sealed class FlowRenderer
{
    public const float CellGap = 0.06f;
    public const float HeaderHeight = 36f;
    public const float ProgressHeight = 14f;
    public const int StripSlots = 10;
    private const int StripLead = 6;
    private const int ShapeCount = 9;
    private const float StripRadius = 10f;
    private const float StripStroke = 2f;
    private const float LiquidSpeed = 3.2f;
    private const float LiquidTail = 1.6f;
    private const int LiquidSamples = 4;
    private const float LiquidSampleSpacing = 0.4f;
    private const float PathThickness = 0.36f;
    private const float EndpointRadius = 0.30f;
    private const float ShapeSize = 0.46f;
    private const float ShapeAlpha = 0.78f;
    private const float OwnedFillAlpha = 0.26f;
    private const float RadiusFraction = 0.14f;
    private const float GlowWidth = 1.7f;
    private const float GlowAlpha = 0.40f;
    private const float BarHeight = 5f;
    private const float TextGap = 10f;
    private const float CaptionGap = 4f;
    private const uint ConnectedHaloMask = 0x33FFFFFF;

    private static readonly Vector4[] Colors =
    {
        new(0.95f, 0.42f, 0.50f, 1f), new(0.40f, 0.68f, 0.98f, 1f), new(0.46f, 0.86f, 0.66f, 1f),
        new(0.95f, 0.74f, 0.34f, 1f), new(0.72f, 0.46f, 0.96f, 1f), new(0.36f, 0.82f, 0.82f, 1f),
        new(0.95f, 0.55f, 0.78f, 1f), new(0.62f, 0.66f, 0.74f, 1f), new(0.80f, 0.56f, 0.36f, 1f),
    };

    private static readonly Vector4[] Confetti = { Colors[0], Colors[1], Colors[2], Colors[3] };
    private static readonly Vector4 EmptyFill = new(1f, 1f, 1f, 0.55f);
    private static readonly TextStyle CaptionStyle = TextStyles.Caption2;

    public static Vector4 ColorOf(int color) => Colors[color % Colors.Length];

    public static ReadOnlySpan<Vector4> ConfettiPalette => Confetti;

    public void Draw(ImDrawListPtr drawList, FlowBoard board, in GameGrid grid, Vector4 accent, StageInk ink,
        float emptyHighlight, float liquidTime, float entrance, Vector2 shake, float scale)
    {
        var plate = BoardPlate.Around(grid.Bounds, scale).Translate(shake);
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, accent, ink);
        var rounding = grid.Pitch * RadiusFraction;
        var cellCount = board.CellCount;
        var emptyGlow = emptyHighlight * (0.35f + 0.65f * Pulse.Wave(Pulse.Fast));
        var emptyStroke = ImGui.GetColorU32(accent with { W = 0.9f * emptyGlow });
        for (var index = 0; index < cellCount; index++)
        {
            var phase = GameJuice.Stagger(entrance, index, cellCount);
            if (phase <= 0f)
            {
                continue;
            }

            var lift = StageCell.Lift(phase) * scale;
            var rect = grid.Cell(index % board.Columns, index / board.Columns).Translate(shake + new Vector2(0f, -lift));
            var occupant = board.Owner(index);
            var fill = occupant >= 0
                ? ColorOf(occupant) with { W = OwnedFillAlpha * phase }
                : EmptyFill with { W = EmptyFill.W * phase };
            StageCell.Draw(drawList, rect, fill, occupant >= 0 ? CellDepth.Flat : CellDepth.Sunken, rounding, scale);
            if (occupant < 0 && emptyGlow > 0f)
            {
                Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, emptyStroke, 2f * scale);
            }
        }

        var thickness = grid.Pitch * PathThickness;
        for (var color = 0; color < board.ColorCount; color++)
        {
            DrawPath(drawList, board, grid, color, thickness, shake);
        }

        for (var color = 0; color < board.ColorCount; color++)
        {
            if (board.IsConnected(color))
            {
                DrawLiquid(drawList, board, grid, color, thickness, shake, liquidTime);
            }
        }

        var pulse = 0.4f + 0.6f * Pulse.Wave(Pulse.Fast);
        for (var color = 0; color < board.ColorCount; color++)
        {
            DrawEndpoints(drawList, board, grid, color, shake, pulse, entrance);
        }

        DrawActiveHead(drawList, board, grid, thickness, shake, pulse);
    }

    public void DrawStrip(ImDrawListPtr drawList, Rect rect, int level, int best, Vector4 accent, Vector4 ink,
        Vector4 muted, float entrance, float scale)
    {
        var first = Math.Max(1, level - StripLead);
        var slotWidth = rect.Width / StripSlots;
        var radius = MathF.Min(StripRadius * scale, slotWidth * 0.42f);
        var cleared = ImGui.GetColorU32(accent);
        var current = ImGui.GetColorU32(accent with { W = 0.18f });
        var future = ImGui.GetColorU32(ink with { W = 0.07f });
        var starInk = GamePalette.InkOn(accent);
        for (var slot = 0; slot < StripSlots; slot++)
        {
            var phase = GameJuice.Stagger(entrance, slot, StripSlots);
            if (phase <= 0f)
            {
                continue;
            }

            var number = first + slot;
            var center = new Vector2(rect.Min.X + (slot + 0.5f) * slotWidth, rect.Center.Y);
            var popRadius = radius * GameJuice.PopIn(phase);
            if (number <= best)
            {
                drawList.AddCircleFilled(center, popRadius, cleared, 24);
                ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Star, starInk, popRadius * 1.1f);
                continue;
            }

            if (number == level)
            {
                drawList.AddCircleFilled(center, popRadius, current, 24);
                drawList.AddCircle(center, popRadius, cleared, 24, StripStroke * scale);
                Typography.DrawCentered(drawList, center, GameNumber.Label(number), ink, CaptionStyle);
                continue;
            }

            drawList.AddCircleFilled(center, popRadius, future, 24);
            Typography.DrawCentered(drawList, center, GameNumber.Label(number), muted, CaptionStyle);
        }
    }

    public static bool HintButton(ImDrawListPtr drawList, Vector2 center, float radius, bool enabled, PhoneTheme theme,
        Vector4 accent, float scale)
    {
        var corner = new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(center - corner, center + corner);
        Material.Frosted(drawList, center - corner, center + corner, radius, scale, enabled ? hovered ? 1f : 0.9f : 0.55f);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent with { W = 0.16f }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = !enabled ? theme.TextMuted with { W = 0.45f } : hovered ? theme.TextStrong : accent;
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Lightbulb, ink, radius * 0.95f);
        return enabled && UiInteract.HoverClickCircle(center, radius);
    }

    public void DrawProgress(ImDrawListPtr drawList, Rect row, Vector4 ink, Vector4 muted, Vector4 accent, float scale,
        string flows, string filled, int filledCount, int total, bool warn)
    {
        if (total <= 0)
        {
            return;
        }

        var flowsCaption = Loc.Upper(Loc.T(L.Games.Flows));
        var filledCaption = Loc.Upper(Loc.T(L.Games.Filled));
        var gap = CaptionGap * scale;
        var textY = row.Center.Y - Typography.LineHeight(CaptionStyle) * 0.5f;
        var x = row.Min.X;
        Typography.Draw(drawList, new Vector2(x, textY), flowsCaption, muted, CaptionStyle);
        x += Typography.Measure(flowsCaption, CaptionStyle).X + gap;
        Typography.Draw(drawList, new Vector2(x, textY), flows, ink, CaptionStyle);
        var trackLeft = x + Typography.Measure(flows, CaptionStyle).X + TextGap * scale;
        var rightX = row.Max.X - Typography.Measure(filled, CaptionStyle).X;
        Typography.Draw(drawList, new Vector2(rightX, textY), filled, warn ? accent : ink, CaptionStyle);
        rightX -= gap + Typography.Measure(filledCaption, CaptionStyle).X;
        Typography.Draw(drawList, new Vector2(rightX, textY), filledCaption, muted, CaptionStyle);
        var trackRight = rightX - TextGap * scale;
        var trackWidth = trackRight - trackLeft;
        if (trackWidth <= 0f)
        {
            return;
        }

        var height = BarHeight * scale;
        var radius = height * 0.5f;
        var min = new Vector2(trackLeft, row.Center.Y - radius);
        var max = new Vector2(trackRight, min.Y + height);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ink with { W = 0.10f }));
        var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Fast);
        var barColor = warn ? accent with { W = pulse } : accent;
        var fillWidth = MathF.Max(height, trackWidth * filledCount / total);
        Squircle.Fill(drawList, min, new Vector2(min.X + fillWidth, max.Y), radius, ImGui.GetColorU32(barColor));
    }

    private static void DrawPath(ImDrawListPtr drawList, FlowBoard board, in GameGrid grid, int color, float thickness,
        Vector2 shake)
    {
        var length = board.PathLength(color);
        if (length < 1)
        {
            return;
        }

        var tint = ColorOf(color);
        if (board.IsConnected(color))
        {
            DrawPolyline(drawList, board, grid, color, length, shake,
                ImGui.GetColorU32(GamePalette.Lighten(tint, 0.3f) with { W = GlowAlpha }), thickness * GlowWidth);
        }

        DrawPolyline(drawList, board, grid, color, length, shake, ImGui.GetColorU32(tint), thickness);
    }

    private static void DrawPolyline(ImDrawListPtr drawList, FlowBoard board, in GameGrid grid, int color, int length,
        Vector2 shake, uint packed, float width)
    {
        var joint = width * 0.5f;
        var previous = CellCenter(grid, board, board.PathCell(color, 0)) + shake;
        drawList.AddCircleFilled(previous, joint, packed, 20);
        for (var index = 1; index < length; index++)
        {
            var current = CellCenter(grid, board, board.PathCell(color, index)) + shake;
            drawList.AddLine(previous, current, packed, width);
            drawList.AddCircleFilled(current, joint, packed, 20);
            previous = current;
        }
    }

    private static void DrawLiquid(ImDrawListPtr drawList, FlowBoard board, in GameGrid grid, int color,
        float thickness, Vector2 shake, float liquidTime)
    {
        var length = board.PathLength(color);
        if (length < 2)
        {
            return;
        }

        var span = length - 1;
        var head = Wrap(liquidTime * LiquidSpeed + color * 2.3f, span + LiquidTail);
        var tint = GamePalette.Lighten(ColorOf(color), 0.45f);
        for (var sample = 0; sample < LiquidSamples; sample++)
        {
            var position = head - sample * LiquidSampleSpacing;
            if (position < 0f || position > span)
            {
                continue;
            }

            var segment = Math.Min(span - 1, (int)position);
            var fraction = position - segment;
            var from = CellCenter(grid, board, board.PathCell(color, segment)) + shake;
            var to = CellCenter(grid, board, board.PathCell(color, segment + 1)) + shake;
            var point = Vector2.Lerp(from, to, fraction);
            var fade = 1f - sample / (float)LiquidSamples;
            var radius = thickness * 0.5f * (0.55f + 0.45f * fade);
            if (sample == 0)
            {
                drawList.AddCircleFilled(point, radius * 1.8f, ImGui.GetColorU32(tint with { W = 0.25f }), 20);
            }

            drawList.AddCircleFilled(point, radius, ImGui.GetColorU32(tint with { W = 0.95f * fade }), 16);
        }
    }

    private static void DrawEndpoints(ImDrawListPtr drawList, FlowBoard board, in GameGrid grid, int color,
        Vector2 shake, float pulse, float entrance)
    {
        var radius = grid.Pitch * EndpointRadius;
        var tint = ColorOf(color);
        var packed = ImGui.GetColorU32(tint);
        var shapeInk = ImGui.GetColorU32(GamePalette.InkOn(tint) with { W = ShapeAlpha });
        var connected = board.IsConnected(color);
        DrawDot(drawList, board, grid, board.EndpointA(color), radius, packed, shapeInk, color, connected, pulse, shake,
            entrance);
        DrawDot(drawList, board, grid, board.EndpointB(color), radius, packed, shapeInk, color, connected, pulse, shake,
            entrance);
    }

    private static void DrawDot(ImDrawListPtr drawList, FlowBoard board, in GameGrid grid, int cell, float radius,
        uint packed, uint shapeInk, int color, bool connected, float pulse, Vector2 shake, float entrance)
    {
        var phase = GameJuice.Stagger(entrance, cell, board.CellCount);
        if (phase <= 0f)
        {
            return;
        }

        var center = CellCenter(grid, board, cell) + shake;
        var popRadius = radius * GameJuice.PopIn(phase);
        if (connected)
        {
            drawList.AddCircleFilled(center, popRadius * (1.2f + 0.12f * pulse), packed & ConnectedHaloMask, 24);
        }

        drawList.AddCircleFilled(center, popRadius, packed, 24);
        DrawShape(drawList, center, popRadius * ShapeSize, color % ShapeCount, shapeInk);
    }

    private static void DrawShape(ImDrawListPtr drawList, Vector2 center, float size, int shape, uint color)
    {
        switch (shape)
        {
            case 1:
                drawList.AddRectFilled(center - new Vector2(size * 0.85f, size * 0.85f),
                    center + new Vector2(size * 0.85f, size * 0.85f), color, size * 0.2f);
                return;
            case 2:
                drawList.AddQuadFilled(center + new Vector2(0f, -size * 1.05f), center + new Vector2(size * 1.05f, 0f),
                    center + new Vector2(0f, size * 1.05f), center + new Vector2(-size * 1.05f, 0f), color);
                return;
            case 3:
                drawList.AddTriangleFilled(center + new Vector2(0f, -size * 1.05f),
                    center + new Vector2(size * 0.95f, size * 0.7f), center + new Vector2(-size * 0.95f, size * 0.7f),
                    color);
                return;
            case 4:
                drawList.AddRectFilled(center - new Vector2(size, size * 0.3f), center + new Vector2(size, size * 0.3f),
                    color);
                drawList.AddRectFilled(center - new Vector2(size * 0.3f, size), center + new Vector2(size * 0.3f, size),
                    color);
                return;
            case 5:
                DrawStar(drawList, center, size, color);
                return;
            case 6:
                for (var corner = 0; corner < 6; corner++)
                {
                    var angle = corner * MathF.PI / 3f;
                    drawList.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size);
                }

                drawList.PathFillConvex(color);
                return;
            case 7:
                drawList.AddCircle(center, size * 0.85f, color, 24, size * 0.45f);
                return;
            case 8:
                drawList.AddCircleFilled(center + new Vector2(0f, -size * 0.3f), size * 0.7f, color, 20);
                drawList.AddTriangleFilled(center + new Vector2(-size * 0.62f, -size * 0.05f),
                    center + new Vector2(size * 0.62f, -size * 0.05f), center + new Vector2(0f, size * 1.05f), color);
                return;
            default:
                drawList.AddCircleFilled(center, size * 0.9f, color, 20);
                return;
        }
    }

    private static void DrawStar(ImDrawListPtr drawList, Vector2 center, float size, uint color)
    {
        const int points = 10;
        Span<Vector2> ring = stackalloc Vector2[points];
        for (var index = 0; index < points; index++)
        {
            var angle = -MathF.PI * 0.5f + index * MathF.PI / 5f;
            var radius = index % 2 == 0 ? size * 1.15f : size * 0.5f;
            ring[index] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        for (var index = 0; index < points; index++)
        {
            drawList.AddTriangleFilled(center, ring[index], ring[(index + 1) % points], color);
        }
    }

    private static void DrawActiveHead(ImDrawListPtr drawList, FlowBoard board, in GameGrid grid, float thickness,
        Vector2 shake, float pulse)
    {
        var active = board.ActiveColor;
        if (active < 0 || board.PathLength(active) == 0)
        {
            return;
        }

        var head = board.PathCell(active, board.PathLength(active) - 1);
        if (board.IsEndpoint(head))
        {
            return;
        }

        var center = CellCenter(grid, board, head) + shake;
        var color = ColorOf(active);
        ProgressRing.Glow(center, thickness * 0.9f, color, 0.6f + 0.4f * pulse);
        drawList.AddCircleFilled(center, thickness * 0.62f, ImGui.GetColorU32(GamePalette.Lighten(color, 0.3f)), 24);
    }

    private static Vector2 CellCenter(in GameGrid grid, FlowBoard board, int cell) =>
        grid.CellCenter(cell % board.Columns, cell / board.Columns);

    private static float Wrap(float value, float size) => value - MathF.Floor(value / size) * size;
}
