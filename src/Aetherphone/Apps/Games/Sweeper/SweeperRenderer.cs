using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Sweeper;

internal readonly struct SweeperView
{
    public readonly float Entrance;
    public readonly float WaveProgress;
    public readonly int Hovered;
    public readonly int Pressed;
    public readonly float HoldFraction;
    public readonly float[] FlagPop;

    public SweeperView(float entrance, float waveProgress, int hovered, int pressed, float holdFraction,
        float[] flagPop)
    {
        Entrance = entrance;
        WaveProgress = waveProgress;
        Hovered = hovered;
        Pressed = pressed;
        HoldFraction = holdFraction;
        FlagPop = flagPop;
    }
}

internal static class SweeperRenderer
{
    public const float GapFraction = 0.10f;
    private const float CellRadiusFraction = 0.18f;
    private const float WaveOverlap = 0.5f;
    private const float AccentTint = 0.12f;
    private const float MinNumberPop = 0.3f;
    private static readonly Vector4[] NumberInks =
    {
        new(0.22f, 0.48f, 0.95f, 1f), new(0.18f, 0.62f, 0.38f, 1f), new(0.90f, 0.28f, 0.34f, 1f),
        new(0.52f, 0.34f, 0.86f, 1f), new(0.86f, 0.50f, 0.18f, 1f), new(0.16f, 0.64f, 0.70f, 1f),
        new(0.24f, 0.24f, 0.30f, 1f), new(0.50f, 0.50f, 0.56f, 1f),
    };

    private static readonly Vector4 Covered = new(0.84f, 0.81f, 0.76f, 1f);
    private static readonly Vector4 Revealed = new(0.96f, 0.95f, 0.92f, 1f);
    private static readonly Vector4 MineBed = new(0.90f, 0.86f, 0.82f, 1f);
    private static readonly Vector4 Detonated = new(0.95f, 0.36f, 0.34f, 1f);
    private static readonly Vector4 MineInk = new(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Vector4 Pole = new(0.40f, 0.38f, 0.36f, 1f);
    private static readonly Vector4 WrongFlag = new(0.90f, 0.25f, 0.28f, 1f);
    private static readonly Vector4 Shine = new(1f, 1f, 1f, 0.55f);

    public static void DrawBoard(ImDrawListPtr drawList, SweeperBoard board, in GameGrid grid, in SweeperView view,
        float scale, Vector4 accent, StageInk ink)
    {
        BoardPlate.Draw(drawList, BoardPlate.Around(grid.Bounds, scale), BoardPlate.Radius * scale, scale, accent, ink);
        var count = board.CellCount;
        var radius = grid.Pitch * CellRadiusFraction;
        var coveredFill = Vector4.Lerp(Covered, accent, AccentTint);
        var numberScale = Math.Clamp(grid.Pitch / (30f * scale), 0.7f, 1.35f);
        var lost = board.State == SweeperState.Lost;
        for (var index = 0; index < count; index++)
        {
            var lift = StageCell.Lift(GameJuice.Stagger(view.Entrance, index, count)) * scale;
            var rect = grid.Cell(index % board.Columns, index / board.Columns).Translate(new Vector2(0f, -lift));
            if (board.IsRevealed(index))
            {
                var local = board.RevealWave(index) == board.WaveId
                    ? GameJuice.Stagger(view.WaveProgress, board.RevealDistance(index), board.WaveMaxDistance + 1,
                        WaveOverlap)
                    : 1f;
                if (local > 0f)
                {
                    DrawRevealed(drawList, board, index, rect, local, radius, numberScale, scale, accent);
                    continue;
                }

                StageCell.Draw(drawList, rect, coveredFill, CellDepth.Raised, radius, scale);
                continue;
            }

            var hovered = index == view.Hovered;
            var pressed = index == view.Pressed;
            var wrong = lost && board.IsFlagged(index) && !board.IsMine(index);
            DrawCovered(drawList, rect, coveredFill, hovered, pressed, board.IsFlagged(index), view.FlagPop[index],
                wrong, radius, scale, accent);
            if (pressed && view.HoldFraction > 0f)
            {
                ProgressRing.Fill(drawList, rect.Center, rect.Width * 0.36f, 2f * scale, view.HoldFraction, accent);
            }
        }
    }

    private static void DrawRevealed(ImDrawListPtr drawList, SweeperBoard board, int index, Rect rect, float local,
        float radius, float numberScale, float scale, Vector4 accent)
    {
        var pop = MathF.Max(MinNumberPop, GameJuice.PopIn(local));
        if (board.IsMine(index))
        {
            var detonated = index == board.ClickedBomb;
            var fill = detonated ? Detonated : MineBed;
            StageCell.Draw(drawList, rect, fill, CellDepth.Sunken, radius, scale);
            DrawMine(drawList, rect.Center, rect.Width * 0.22f * pop, detonated ? GamePalette.InkOn(fill) : MineInk);
            return;
        }

        StageCell.Draw(drawList, rect, Revealed, CellDepth.Sunken, radius, scale);
        if (local < 1f)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(accent with { W = 0.35f * (1f - local) }));
        }

        var adjacent = board.Adjacent(index);
        if (adjacent > 0)
        {
            Typography.DrawCentered(drawList, rect.Center, GameNumber.Label(adjacent), NumberInks[adjacent - 1],
                numberScale * pop, FontWeight.Bold);
        }
    }

    private static void DrawCovered(ImDrawListPtr drawList, Rect rect, Vector4 coveredFill, bool hovered,
        bool pressed, bool flagged, float flagPop, bool wrong, float radius, float scale, Vector4 accent)
    {
        var fill = hovered ? GamePalette.Lighten(coveredFill, 0.08f) : coveredFill;
        StageCell.Draw(drawList, rect, fill, pressed ? CellDepth.Pressed : CellDepth.Raised, radius, scale);
        if (!flagged)
        {
            return;
        }

        DrawFlag(drawList, rect.Center, rect.Width * 0.26f * (1f + flagPop * 0.25f), accent, scale);
        if (wrong)
        {
            DrawCross(drawList, rect.Center, rect.Width * 0.3f, WrongFlag, scale);
        }
    }

    private static void DrawMine(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        for (var spoke = 0; spoke < 4; spoke++)
        {
            var angle = spoke * MathF.PI / 4f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 1.5f;
            drawList.AddLine(center - direction, center + direction, packed, radius * 0.35f);
        }

        drawList.AddCircleFilled(center, radius, packed);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.32f,
            ImGui.GetColorU32(Shine));
    }

    private static void DrawFlag(ImDrawListPtr drawList, Vector2 center, float size, Vector4 accent, float scale)
    {
        var poleTop = new Vector2(center.X - size * 0.35f, center.Y - size);
        var poleBottom = new Vector2(center.X - size * 0.35f, center.Y + size);
        drawList.AddLine(poleTop, poleBottom, ImGui.GetColorU32(Pole), MathF.Max(1.5f * scale, size * 0.18f));
        var flagTip = new Vector2(center.X + size * 0.75f, center.Y - size * 0.45f);
        drawList.AddTriangleFilled(poleTop, new Vector2(poleTop.X, center.Y), flagTip, ImGui.GetColorU32(accent));
    }

    private static void DrawCross(ImDrawListPtr drawList, Vector2 center, float reach, Vector4 color, float scale)
    {
        var packed = ImGui.GetColorU32(color);
        var thickness = 2f * scale;
        drawList.AddLine(center - new Vector2(reach, reach), center + new Vector2(reach, reach), packed, thickness);
        drawList.AddLine(center - new Vector2(reach, -reach), center + new Vector2(reach, -reach), packed, thickness);
    }
}
