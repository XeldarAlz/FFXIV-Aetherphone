using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Reversi;

internal readonly struct ReversiRenderState
{
    public readonly sbyte[] FlipDistance;
    public readonly sbyte[] FlipFrom;
    public readonly float FlipWave;
    public readonly int FlipSpan;
    public readonly int PlacedCell;
    public readonly float PlaceProgress;
    public readonly ulong Hints;
    public readonly float Entrance;

    public ReversiRenderState(sbyte[] flipDistance, sbyte[] flipFrom, float flipWave, int flipSpan, int placedCell,
        float placeProgress, ulong hints, float entrance)
    {
        FlipDistance = flipDistance;
        FlipFrom = flipFrom;
        FlipWave = flipWave;
        FlipSpan = flipSpan;
        PlacedCell = placedCell;
        PlaceProgress = placeProgress;
        Hints = hints;
        Entrance = entrance;
    }
}

internal sealed class ReversiRenderer
{
    private const float DiscRadiusFraction = 0.40f;
    private const float BoardRadius = 6f;
    private const float FlipOverlap = 0.55f;
    private const float FlipLift = 5f;
    private const float ShadowOffset = 2f;
    private const float HintFillAlpha = 0.16f;
    private const float CapsulePadX = 10f;
    private const float CapsuleDisc = 12f;
    private const float CapsuleGap = 5f;
    private const float CapsuleSideGap = 12f;
    private const float DotRadius = 1.8f;
    private const float DotStep = 5.5f;
    private const int DotCount = 3;
    private const float DotSpeed = 7f;
    private static readonly Vector4 Felt = new(0.16f, 0.42f, 0.30f, 1f);
    private static readonly Vector4 DarkDisc = new(0.13f, 0.14f, 0.19f, 1f);
    private static readonly Vector4 LightDisc = new(0.94f, 0.95f, 0.97f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.30f);
    private static readonly Vector4 GridLine = new(0f, 0f, 0f, 0.22f);
    private static readonly TextStyle CountStyle = TextStyles.FootnoteEmphasized;
    private const string WidestCount = "00";

    public void Draw(ImDrawListPtr drawList, ReversiBoard board, GameGrid grid, in ReversiRenderState state,
        Vector4 accent, float scale)
    {
        var bounds = grid.Bounds;
        Squircle.FillVerticalGradient(drawList, bounds.Min, bounds.Max, BoardRadius * scale,
            ImGui.GetColorU32(GamePalette.Lighten(Felt, 0.06f)), ImGui.GetColorU32(GamePalette.Darken(Felt, 0.10f)));
        DrawGridLines(drawList, grid, scale);
        var radius = grid.Pitch * DiscRadiusFraction;
        var hintPulse = 0.35f + 0.35f * Pulse.Wave(Pulse.Calm);
        for (var row = 0; row < ReversiBoard.Size; row++)
        {
            for (var column = 0; column < ReversiBoard.Size; column++)
            {
                DrawCell(drawList, board, row * ReversiBoard.Size + column, grid.CellCenter(column, row), radius,
                    state, accent, hintPulse, scale);
            }
        }
    }

    public static float CountsWidth(float scale)
    {
        var countWidth = Typography.Measure(WidestCount, CountStyle).X / scale;
        return CapsulePadX * 2f + (CapsuleDisc + CapsuleGap + countWidth) * 2f + CapsuleSideGap;
    }

    public const float ThinkingWidth = CapsulePadX * 2f + (DotCount - 1) * DotStep + DotRadius * 2f;

    public void DrawCounts(ImDrawListPtr drawList, Rect rect, int dark, int light, int current, Vector4 accent,
        PhoneTheme theme, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var countWidth = Typography.Measure(WidestCount, CountStyle).X;
        var disc = CapsuleDisc * scale;
        var gap = CapsuleGap * scale;
        var centerY = rect.Center.Y;
        var textY = centerY - Typography.LineHeight(CountStyle) * 0.5f;
        var penX = rect.Min.X + CapsulePadX * scale;
        DrawCountDisc(drawList, new Vector2(penX + disc * 0.5f, centerY), disc * 0.5f, ReversiBoard.Dark,
            current == ReversiBoard.Dark, accent, scale);
        penX += disc + gap;
        Typography.Draw(drawList, new Vector2(penX, textY), GameNumber.Label(dark), StageInks.Strong, CountStyle);
        penX += countWidth + CapsuleSideGap * scale;
        DrawCountDisc(drawList, new Vector2(penX + disc * 0.5f, centerY), disc * 0.5f, ReversiBoard.Light,
            current == ReversiBoard.Light, accent, scale);
        penX += disc + gap;
        Typography.Draw(drawList, new Vector2(penX, textY), GameNumber.Label(light), StageInks.Strong, CountStyle);
    }

    public static void DrawThinking(ImDrawListPtr drawList, Rect rect, float dotPhase, Vector4 accent, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var centerY = rect.Center.Y;
        var dotX = rect.Center.X - (DotCount - 1) * DotStep * scale * 0.5f;
        for (var dot = 0; dot < DotCount; dot++)
        {
            var bounce = MathF.Sin(dotPhase * DotSpeed - dot * 0.9f);
            var alpha = 0.45f + 0.55f * MathF.Max(0f, bounce);
            var lift = MathF.Max(0f, bounce) * 2f * scale;
            drawList.AddCircleFilled(new Vector2(dotX + dot * DotStep * scale, centerY - lift), DotRadius * scale,
                ImGui.GetColorU32(accent with { W = alpha }), 10);
        }
    }

    private static void DrawCountDisc(ImDrawListPtr drawList, Vector2 center, float radius, int color, bool active,
        Vector4 accent, float scale)
    {
        if (active)
        {
            drawList.AddCircleFilled(center, radius + 2.5f * scale, ImGui.GetColorU32(accent with { W = 0.55f }), 20);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color == ReversiBoard.Dark ? DarkDisc : LightDisc),
            20);
    }

    private void DrawCell(ImDrawListPtr drawList, ReversiBoard board, int index, Vector2 center, float radius,
        in ReversiRenderState state, Vector4 accent, float hintPulse, float scale)
    {
        var color = board.Cell(index);
        var distance = state.FlipDistance[index];
        if (distance >= 0)
        {
            var progress = GameJuice.Stagger(state.FlipWave, distance, state.FlipSpan, FlipOverlap);
            if (progress <= 0f)
            {
                DrawDisc(drawList, center, radius, 1f, 1f, state.FlipFrom[index], 0f, scale);
                return;
            }

            if (progress >= 1f)
            {
                DrawDisc(drawList, center, radius, 1f, 1f, color, 0f, scale);
                return;
            }

            var squash = MathF.Abs(MathF.Cos(progress * MathF.PI));
            var shown = progress < 0.5f ? state.FlipFrom[index] : color;
            var lift = MathF.Sin(progress * MathF.PI) * FlipLift * scale;
            DrawDisc(drawList, center, radius, squash, 1f, shown, lift, scale);
            return;
        }

        if (index == state.PlacedCell && state.PlaceProgress < 1f)
        {
            DrawDisc(drawList, center, radius, 1f, GameJuice.PopIn(state.PlaceProgress), color, 0f, scale);
            return;
        }

        if (color != 0)
        {
            var pop = state.Entrance < 1f
                ? GameJuice.PopIn(GameJuice.Stagger(state.Entrance, index, ReversiBoard.CellCount))
                : 1f;
            DrawDisc(drawList, center, radius, 1f, pop, color, 0f, scale);
            return;
        }

        if ((state.Hints & (1UL << index)) != 0)
        {
            DrawHint(drawList, center, radius, accent, hintPulse, scale);
        }
    }

    private static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, float squashX, float popScale,
        int colorIndex, float lift, float scale)
    {
        var effective = radius * MathF.Max(0f, popScale);
        if (effective <= 0.5f)
        {
            return;
        }

        var halfWidth = MathF.Max(0.5f, effective * squashX);
        var halfHeight = effective;
        var min = new Vector2(center.X - halfWidth, center.Y - halfHeight - lift);
        var max = new Vector2(center.X + halfWidth, center.Y + halfHeight - lift);
        var corner = MathF.Min(halfWidth, halfHeight);
        var body = colorIndex == ReversiBoard.Dark ? DarkDisc : LightDisc;
        var shadowDrop = new Vector2(0f, ShadowOffset * scale + lift * 0.6f);
        Squircle.Fill(drawList, min + shadowDrop, max + shadowDrop, corner, ImGui.GetColorU32(Shadow));
        Squircle.Fill(drawList, min, max, corner, ImGui.GetColorU32(body));
        Squircle.Fill(drawList, min, new Vector2(max.X, center.Y - lift), corner,
            ImGui.GetColorU32(GamePalette.Lighten(body, 0.22f) with { W = 0.5f }));
        Squircle.Stroke(drawList, min, max, corner, ImGui.GetColorU32(GamePalette.Darken(body, 0.3f) with { W = 0.6f }),
            1f * scale);
    }

    private static void DrawHint(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float pulse,
        float scale)
    {
        var half = new Vector2(radius, radius);
        Squircle.Fill(drawList, center - half, center + half, radius, ImGui.GetColorU32(DarkDisc with { W = HintFillAlpha }));
        Squircle.Stroke(drawList, center - half, center + half, radius, ImGui.GetColorU32(accent with { W = pulse }),
            1.2f * scale);
    }

    private static void DrawGridLines(ImDrawListPtr drawList, GameGrid grid, float scale)
    {
        var color = ImGui.GetColorU32(GridLine);
        for (var line = 0; line <= ReversiBoard.Size; line++)
        {
            var x = grid.Origin.X + line * grid.Pitch;
            drawList.AddLine(new Vector2(x, grid.Origin.Y), new Vector2(x, grid.Origin.Y + grid.Height), color,
                1f * scale);
            var y = grid.Origin.Y + line * grid.Pitch;
            drawList.AddLine(new Vector2(grid.Origin.X, y), new Vector2(grid.Origin.X + grid.Width, y), color,
                1f * scale);
        }
    }
}
