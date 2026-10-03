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

    public GemAnim(GemPhase phase, int swapA, int swapB, float swapProgress, float clearProgress, float fallProgress,
        int selectedIndex, int hintA, int hintB, float hintClock)
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
    public static readonly Vector4 FrostTint = new(0.62f, 0.86f, 1f, 1f);
    private static readonly Vector4 PrismInk = new(0.96f, 0.94f, 1f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private static readonly Vector4[] GemColors =
    {
        new(0.95f, 0.83f, 0.26f, 1f), new(0.30f, 0.62f, 0.96f, 1f), new(0.93f, 0.42f, 0.50f, 1f),
        new(1.00f, 0.55f, 0.22f, 1f), new(0.36f, 0.84f, 0.66f, 1f), new(0.72f, 0.46f, 0.96f, 1f),
    };

    private static readonly string[] GemSymbols = { "★", "◆", "♥", "▲", "●", "■", };

    public void Draw(GemSwapBoard board, GameGrid grid, in GemAnim anim, PhoneTheme theme, float scale, Vector4 accent,
        float entrance, float frost)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 6f * scale;
        var half = (grid.Pitch - grid.Gap) * 0.5f;
        var boardPad = grid.Gap + 4f * scale;
        var boardMin = grid.Origin - new Vector2(boardPad, boardPad);
        var boardMax = grid.Origin + new Vector2(grid.Width, grid.Height) + new Vector2(boardPad, boardPad);
        var time = (float)ImGui.GetTime();
        GameScene.Arena(drawList, new Rect(boardMin, boardMax), rounding + boardPad, scale, accent);
        for (var row = 0; row < GemSwapBoard.Rows; row++)
        {
            for (var column = 0; column < GemSwapBoard.Columns; column++)
            {
                var cell = grid.Cell(column, row);
                Squircle.Fill(drawList, cell.Min, cell.Max, rounding,
                    ImGui.GetColorU32(GamePalette.CellSunken with { W = 0.55f }));
            }
        }

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

            DrawGem(drawList, board, grid, anim, index, color, rounding, half, scale, theme, entrance, time);
        }

        if (frost > 0.01f)
        {
            DrawFrost(drawList, boardMin, boardMax, rounding + boardPad, frost, scale, time);
        }
    }

    private void DrawGem(ImDrawListPtr drawList, GemSwapBoard board, GameGrid grid, in GemAnim anim, int index,
        int color, float rounding, float half, float scale, PhoneTheme theme, float entrance, float time)
    {
        var column = index % GemSwapBoard.Columns;
        var row = index / GemSwapBoard.Columns;
        var center = grid.CellCenter(column, row);
        var drawScale = GameJuice.PopIn(GameJuice.Stagger(entrance, index, GemSwapBoard.CellCount));
        if (drawScale <= 0.01f)
        {
            return;
        }

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
            drawScale *= 1f + 0.35f * anim.ClearProgress;
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

        var gemHalf = half * drawScale;
        var min = new Vector2(center.X - gemHalf, center.Y - gemHalf);
        var max = new Vector2(center.X + gemHalf, center.Y + gemHalf);
        var special = board.Special(index);
        if (special == GemSpecial.Prism || color == GemSwapBoard.PrismColor)
        {
            DrawPrism(drawList, center, gemHalf, alpha, scale, time);
            DrawSelection(drawList, anim, index, center, min, max, gemHalf, rounding, scale, theme);
            return;
        }

        var gemColor = GemColors[color];
        if (special == GemSpecial.Burst)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 6f + index);
            ProgressRing.Glow(center, gemHalf * (1.15f + 0.18f * pulse), GamePalette.Lighten(gemColor, 0.3f),
                (0.6f + 0.5f * pulse) * alpha);
        }

        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(gemColor, 0.14f) with { W = gemColor.W * alpha }),
            ImGui.GetColorU32(GamePalette.Darken(gemColor, 0.16f) with { W = gemColor.W * alpha }));
        Squircle.Fill(drawList, min, new Vector2(max.X, center.Y), rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f * alpha)));
        drawList.AddCircleFilled(new Vector2(center.X - gemHalf * 0.4f, center.Y - gemHalf * 0.45f), gemHalf * 0.18f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.45f * alpha)), 12);
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.16f * alpha)),
            1f * scale);
        switch (special)
        {
            case GemSpecial.LineHorizontal:
            case GemSpecial.LineVertical:
                DrawLineGem(drawList, center, min, max, gemHalf, special == GemSpecial.LineHorizontal, alpha, time,
                    rounding);
                break;
            case GemSpecial.Burst:
                DrawBurstGem(drawList, center, gemHalf, alpha, time);
                break;
            default:
            {
                var ink = GamePalette.InkOn(gemColor);
                Typography.DrawCentered(center, GemSymbols[color], ink with { W = ink.W * alpha },
                    gemHalf / (20f * scale), FontWeight.SemiBold);
                break;
            }
        }

        DrawSelection(drawList, anim, index, center, min, max, gemHalf, rounding, scale, theme);
    }

    private static void DrawSelection(ImDrawListPtr drawList, in GemAnim anim, int index, Vector2 center, Vector2 min,
        Vector2 max, float gemHalf, float rounding, float scale, PhoneTheme theme)
    {
        if (index != anim.SelectedIndex)
        {
            return;
        }

        ProgressRing.Glow(center, gemHalf * 1.05f, theme.Accent, 0.35f + 0.3f * Pulse.Wave(Pulse.Fast));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(theme.Accent), 2.5f * scale);
    }

    private static void DrawLineGem(ImDrawListPtr drawList, Vector2 center, Vector2 min, Vector2 max, float gemHalf,
        bool horizontal, float alpha, float time, float rounding)
    {
        var inset = rounding * 0.35f;
        drawList.PushClipRect(min + new Vector2(inset, inset), max - new Vector2(inset, inset), true);
        var stripe = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.26f * alpha));
        var spacing = gemHalf * 0.55f;
        var slant = gemHalf * 0.4f;
        var phase = time * 1.8f - MathF.Floor(time * 1.8f);
        var stripeThickness = MathF.Max(1.5f, gemHalf * 0.16f);
        for (var stripeIndex = -4; stripeIndex <= 4; stripeIndex++)
        {
            var offset = (stripeIndex + phase) * spacing;
            if (horizontal)
            {
                var x = center.X + offset;
                drawList.AddLine(new Vector2(x - slant, max.Y), new Vector2(x + slant, min.Y), stripe, stripeThickness);
            }
            else
            {
                var y = center.Y + offset;
                drawList.AddLine(new Vector2(min.X, y + slant), new Vector2(max.X, y - slant), stripe, stripeThickness);
            }
        }

        drawList.PopClipRect();
        var rail = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f * alpha));
        var railGlow = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f * alpha));
        var thickness = MathF.Max(1.5f, gemHalf * 0.15f);
        var reach = gemHalf * 0.72f;
        var gap = gemHalf * 0.3f;
        for (var side = -1; side <= 1; side += 2)
        {
            Vector2 from;
            Vector2 to;
            if (horizontal)
            {
                from = new Vector2(center.X - reach, center.Y + side * gap);
                to = new Vector2(center.X + reach, center.Y + side * gap);
            }
            else
            {
                from = new Vector2(center.X + side * gap, center.Y - reach);
                to = new Vector2(center.X + side * gap, center.Y + reach);
            }

            drawList.AddLine(from, to, railGlow, thickness * 2.6f);
            drawList.AddLine(from, to, rail, thickness);
        }
    }

    private static void DrawBurstGem(ImDrawListPtr drawList, Vector2 center, float gemHalf, float alpha, float time)
    {
        var white = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.92f * alpha));
        var thickness = MathF.Max(1.5f, gemHalf * 0.14f);
        var beat = 1f + 0.08f * MathF.Sin(time * 6f);
        drawList.AddCircle(center, gemHalf * 0.58f * beat, white, 0, thickness);
        drawList.AddCircleFilled(center, gemHalf * 0.24f * beat, white);
        var spin = time * 2.2f;
        for (var spark = 0; spark < 4; spark++)
        {
            var angle = spin + spark * MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + direction * gemHalf * 0.66f, center + direction * gemHalf * 0.86f, white,
                thickness * 0.8f);
        }
    }

    private static void DrawPrism(ImDrawListPtr drawList, Vector2 center, float gemHalf, float alpha, float scale,
        float time)
    {
        var radius = gemHalf * 1.04f;
        var hue = (int)(time * 2f) % GemColors.Length;
        ProgressRing.Glow(center, radius * 1.15f, GemColors[hue], 0.7f * alpha);
        var rotation = time * 1.3f;
        const float facetAngle = MathF.PI / 3f;
        for (var facet = 0; facet < 6; facet++)
        {
            var from = rotation + facet * facetAngle;
            var outerA = center + new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius;
            var outerB = center + new Vector2(MathF.Cos(from + facetAngle), MathF.Sin(from + facetAngle)) * radius;
            var facetColor = GamePalette.Lighten(GemColors[facet], 0.08f) with { W = alpha };
            drawList.AddTriangleFilled(center, outerA, outerB, ImGui.GetColorU32(facetColor));
        }

        var innerRotation = -rotation * 0.7f;
        var innerRadius = radius * 0.5f;
        for (var facet = 0; facet < 6; facet++)
        {
            var from = innerRotation + facet * facetAngle;
            var innerA = center + new Vector2(MathF.Cos(from), MathF.Sin(from)) * innerRadius;
            var innerB = center + new Vector2(MathF.Cos(from + facetAngle), MathF.Sin(from + facetAngle)) * innerRadius;
            var facetColor = GamePalette.Lighten(GemColors[(facet + 3) % GemColors.Length], 0.45f) with
            {
                W = 0.85f * alpha,
            };
            drawList.AddTriangleFilled(center, innerA, innerB, ImGui.GetColorU32(facetColor));
        }

        var edge = ImGui.GetColorU32(White with { W = 0.75f * alpha });
        for (var facet = 0; facet < 6; facet++)
        {
            var from = rotation + facet * facetAngle;
            var outerA = center + new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius;
            var outerB = center + new Vector2(MathF.Cos(from + facetAngle), MathF.Sin(from + facetAngle)) * radius;
            drawList.AddLine(outerA, outerB, edge, 1.4f * scale);
            drawList.AddLine(center, outerA, ImGui.GetColorU32(White with { W = 0.25f * alpha }), 1f * scale);
        }

        var twinkle = 0.5f + 0.5f * MathF.Sin(time * 5f);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.32f, -radius * 0.38f), radius * (0.1f + 0.05f * twinkle),
            ImGui.GetColorU32(White with { W = (0.6f + 0.4f * twinkle) * alpha }), 12);
    }

    private static void DrawFrost(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float frost,
        float scale, float time)
    {
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(FrostTint with { W = 0.16f * frost }));
        var shimmer = 0.6f + 0.4f * MathF.Sin(time * 3f);
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(FrostTint with { W = 0.75f * frost * shimmer }),
            2.5f * scale);
        var crystal = ImGui.GetColorU32(White with { W = 0.55f * frost });
        var size = 16f * scale * frost;
        DrawCrystal(drawList, new Vector2(min.X + rounding, min.Y + rounding), size, crystal, scale);
        DrawCrystal(drawList, new Vector2(max.X - rounding, min.Y + rounding), size, crystal, scale);
        DrawCrystal(drawList, new Vector2(min.X + rounding, max.Y - rounding), size, crystal, scale);
        DrawCrystal(drawList, new Vector2(max.X - rounding, max.Y - rounding), size, crystal, scale);
    }

    public static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float size, uint color, float scale)
    {
        for (var arm = 0; arm < 3; arm++)
        {
            var angle = arm * MathF.PI / 3f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
            drawList.AddLine(center - direction, center + direction, color, 1.4f * scale);
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
            ProgressRing.Glow(center, half * 1.1f, ColorOf(board.Color(index)), fade * 0.9f);
        }
    }

    public static Vector4 ColorOf(int color) => color >= 0 && color < GemColors.Length ? GemColors[color] : PrismInk;
}
