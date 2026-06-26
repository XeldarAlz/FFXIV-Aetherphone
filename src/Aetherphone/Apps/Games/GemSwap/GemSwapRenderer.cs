using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.GemSwap;

internal readonly struct GemAnim
{
    public readonly GemPhase Phase;

    public readonly int SwapA;

    public readonly int SwapB;

    public readonly float SwapProgress;

    public readonly float ClearProgress;

    public readonly float FallProgress;

    public readonly int SelectedIndex;

    public readonly int HintA;

    public readonly int HintB;

    public readonly float HintClock;

    public GemAnim(GemPhase phase, int swapA, int swapB, float swapProgress, float clearProgress, float fallProgress, int selectedIndex, int hintA, int hintB, float hintClock)
    {
        Phase = phase;
        SwapA = swapA;
        SwapB = swapB;
        SwapProgress = swapProgress;
        ClearProgress = clearProgress;
        FallProgress = fallProgress;
        SelectedIndex = selectedIndex;
        HintA = hintA;
        HintB = hintB;
        HintClock = hintClock;
    }
}

internal sealed class GemSwapRenderer
{
    private static readonly Vector4[] GemColors =
    {
        new(0.95f, 0.83f, 0.26f, 1f),
        new(0.30f, 0.62f, 0.96f, 1f),
        new(0.93f, 0.42f, 0.50f, 1f),
        new(1.00f, 0.55f, 0.22f, 1f),
        new(0.36f, 0.84f, 0.66f, 1f),
        new(0.72f, 0.46f, 0.96f, 1f),
    };

    private static readonly string[] GemSymbols =
    {
        "★", "◆", "♥", "▲", "●", "■",
    };

    public void Draw(GemSwapBoard board, GameGrid grid, in GemAnim anim, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 6f * scale;
        var half = (grid.Pitch - grid.Gap) * 0.5f;

        if (anim.Phase == GemPhase.Clearing)
        {
            DrawClearGlow(drawList, board, grid, anim, half);
        }

        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            var color = board.Color(index);
            if (color < 0)
            {
                continue;
            }

            DrawGem(drawList, board, grid, anim, index, color, rounding, half, scale, theme);
        }
    }

    private void DrawGem(ImDrawListPtr drawList, GemSwapBoard board, GameGrid grid, in GemAnim anim, int index, int color, float rounding, float half, float scale, PhoneTheme theme)
    {
        var column = index % GemSwapBoard.Columns;
        var row = index / GemSwapBoard.Columns;
        var center = grid.CellCenter(column, row);

        var drawScale = 1f;
        var alpha = 1f;

        var swapping = anim.Phase == GemPhase.Swapping || anim.Phase == GemPhase.SwapBack;
        if (swapping && (index == anim.SwapA || index == anim.SwapB))
        {
            var other = index == anim.SwapA ? anim.SwapB : anim.SwapA;
            var otherCenter = grid.CellCenter(other % GemSwapBoard.Columns, other / GemSwapBoard.Columns);
            center = Vector2.Lerp(center, otherCenter, Easing.EaseOutBack(anim.SwapProgress));
        }

        if (anim.Phase == GemPhase.Falling && board.FallFrom(index) != GemSwapBoard.NoFall)
        {
            var distanceRows = row - board.FallFrom(index);
            var remaining = (1f - Easing.EaseOutCubic(anim.FallProgress)) * distanceRows * grid.Pitch;
            center.Y -= remaining;
        }

        if (anim.Phase == GemPhase.Clearing && board.Matched(index))
        {
            drawScale = 1f + 0.35f * anim.ClearProgress;
            alpha = 1f - anim.ClearProgress * anim.ClearProgress;
        }

        if ((index == anim.HintA || index == anim.HintB) && anim.Phase == GemPhase.Idle)
        {
            center.X += MathF.Sin(anim.HintClock * 14f) * 3f * scale;
            center.Y += MathF.Cos(anim.HintClock * 18f) * 3f * scale;
        }

        if (alpha < 0.02f)
        {
            return;
        }

        var gemColor = GemColors[color];
        var gemHalf = half * drawScale;
        var min = new Vector2(center.X - gemHalf, center.Y - gemHalf);
        var max = new Vector2(center.X + gemHalf, center.Y + gemHalf);

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(gemColor with { W = gemColor.W * alpha }));
        Squircle.Fill(drawList, min, new Vector2(max.X, center.Y), rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f * alpha)));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.16f * alpha)), 1f * scale);

        var special = board.Special(index);
        if (special != GemSpecial.None)
        {
            DrawSpecial(drawList, center, gemHalf, special, alpha, scale);
        }
        else
        {
            var ink = GamePalette.InkOn(gemColor);
            Typography.DrawCentered(center, GemSymbols[color], ink with { W = ink.W * alpha }, gemHalf / (20f * scale), FontWeight.SemiBold);
        }

        if (index == anim.SelectedIndex)
        {
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(theme.Accent), 2.5f * scale);
        }
    }

    private void DrawSpecial(ImDrawListPtr drawList, Vector2 center, float gemHalf, GemSpecial special, float alpha, float scale)
    {
        var white = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.92f * alpha));
        var thickness = MathF.Max(1.5f, gemHalf * 0.16f);

        switch (special)
        {
            case GemSpecial.LineHorizontal:
                drawList.AddLine(new Vector2(center.X - gemHalf * 0.7f, center.Y - gemHalf * 0.32f), new Vector2(center.X + gemHalf * 0.7f, center.Y - gemHalf * 0.32f), white, thickness);
                drawList.AddLine(new Vector2(center.X - gemHalf * 0.7f, center.Y + gemHalf * 0.32f), new Vector2(center.X + gemHalf * 0.7f, center.Y + gemHalf * 0.32f), white, thickness);
                break;
            case GemSpecial.LineVertical:
                drawList.AddLine(new Vector2(center.X - gemHalf * 0.32f, center.Y - gemHalf * 0.7f), new Vector2(center.X - gemHalf * 0.32f, center.Y + gemHalf * 0.7f), white, thickness);
                drawList.AddLine(new Vector2(center.X + gemHalf * 0.32f, center.Y - gemHalf * 0.7f), new Vector2(center.X + gemHalf * 0.32f, center.Y + gemHalf * 0.7f), white, thickness);
                break;
            case GemSpecial.Bomb:
                drawList.AddCircle(center, gemHalf * 0.62f, white, 0, thickness);
                drawList.AddCircleFilled(center, gemHalf * 0.28f, white);
                break;
        }
    }

    private void DrawClearGlow(ImDrawListPtr drawList, GemSwapBoard board, GameGrid grid, in GemAnim anim, float half)
    {
        var fade = 1f - anim.ClearProgress;
        if (fade <= 0.02f)
        {
            return;
        }

        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            if (!board.Matched(index) || board.Color(index) < 0)
            {
                continue;
            }

            var center = grid.CellCenter(index % GemSwapBoard.Columns, index / GemSwapBoard.Columns);
            ProgressRing.Glow(center, half * 1.1f, GemColors[board.Color(index)], fade * 0.9f);
        }
    }

    public static Vector4 ColorOf(int color) => GemColors[color % GemColors.Length];
}
