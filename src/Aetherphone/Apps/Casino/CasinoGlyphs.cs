using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal static class CasinoGlyphs
{
    public static void Draw(ImDrawListPtr drawList, string gameId, Vector2 center, float extent, uint ink, uint hole)
    {
        switch (gameId)
        {
            case CasinoGames.Blackjack:
                DrawBlackjack(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Holdem:
                DrawHoldem(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.DealerHoldem:
                DrawDealerHoldem(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Slots:
                DrawSlots(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.SlotsBird:
                DrawBird(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.SlotsCascade:
                DrawCrystal(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.SlotsMoogle:
                DrawPom(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Scratch:
                DrawScratch(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Barkeep:
                DrawBarkeep(drawList, center, extent, ink);
                break;
            case CasinoGames.Bingo:
                DrawBingo(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Wheel:
                DrawWheel(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.DailySpin:
                DrawDailySpin(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Mines:
                DrawMines(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Dice:
                DrawDice(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Limbo:
                DrawLimbo(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Keno:
                DrawKeno(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.HiLo:
                DrawHiLo(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Race:
                DrawRace(drawList, center, extent, ink, hole);
                break;
            case CasinoGames.Plinko:
                DrawPlinko(drawList, center, extent, ink, hole);
                break;
            default:
                DrawChip(drawList, center, extent, ink, hole);
                break;
        }
    }

    private static void DrawBlackjack(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var rounding = extent * 0.16f;
        var backMin = At(center, extent, -0.18f, -0.82f);
        var backMax = At(center, extent, 0.82f, 0.5f);
        drawList.AddRectFilled(backMin, backMax, ink, rounding);
        var gap = extent * 0.08f;
        var frontMin = At(center, extent, -0.82f, -0.5f);
        var frontMax = At(center, extent, 0.18f, 0.82f);
        drawList.AddRectFilled(frontMin - new Vector2(gap, gap), frontMax + new Vector2(gap, gap), hole, rounding);
        drawList.AddRectFilled(frontMin, frontMax, ink, rounding);
        DrawSpade(drawList, (frontMin + frontMax) * 0.5f, extent * 0.26f, hole);
    }

    private static void DrawHoldem(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var rounding = extent * 0.14f;
        var gap = extent * 0.08f;
        var leftMin = At(center, extent, -0.86f, -0.78f);
        var leftMax = At(center, extent, 0.06f, 0.56f);
        drawList.AddRectFilled(leftMin, leftMax, ink, rounding);
        var rightMin = At(center, extent, -0.22f, -0.62f);
        var rightMax = At(center, extent, 0.70f, 0.72f);
        drawList.AddRectFilled(rightMin - new Vector2(gap, gap), rightMax + new Vector2(gap, gap), hole, rounding);
        drawList.AddRectFilled(rightMin, rightMax, ink, rounding);
        DrawSpade(drawList, (rightMin + rightMax) * 0.5f, extent * 0.24f, hole);
        var chip = At(center, extent, 0.62f, 0.66f);
        drawList.AddCircleFilled(chip, extent * 0.36f, hole, 24);
        drawList.AddCircleFilled(chip, extent * 0.28f, ink, 24);
        drawList.AddCircle(chip, extent * 0.16f, hole, 20, extent * 0.06f);
    }

    private static void DrawDealerHoldem(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var rounding = extent * 0.14f;
        var gap = extent * 0.08f;
        var leftMin = At(center, extent, -0.80f, -0.30f);
        var leftMax = At(center, extent, 0.04f, 0.86f);
        drawList.AddRectFilled(leftMin, leftMax, ink, rounding);
        var rightMin = At(center, extent, -0.10f, -0.18f);
        var rightMax = At(center, extent, 0.74f, 0.98f);
        drawList.AddRectFilled(rightMin - new Vector2(gap, gap), rightMax + new Vector2(gap, gap), hole, rounding);
        drawList.AddRectFilled(rightMin, rightMax, ink, rounding);
        DrawSpade(drawList, (rightMin + rightMax) * 0.5f, extent * 0.22f, hole);
        var button = At(center, extent, 0f, -0.70f);
        drawList.AddCircleFilled(button, extent * 0.34f, hole, 24);
        drawList.AddCircleFilled(button, extent * 0.26f, ink, 24);
        drawList.AddCircle(button, extent * 0.15f, hole, 20, extent * 0.06f);
    }

    private static void DrawSlots(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<float> reelColumns = stackalloc float[3] { -0.68f, 0f, 0.68f };
        Span<float> symbolOffsets = stackalloc float[3] { -0.28f, 0.14f, -0.06f };
        var halfWidth = extent * 0.26f;
        var rounding = extent * 0.14f;
        for (var reel = 0; reel < reelColumns.Length; reel++)
        {
            var reelCenterX = At(center, extent, reelColumns[reel], 0f).X;
            var min = new Vector2(reelCenterX - halfWidth, At(center, extent, 0f, -0.8f).Y);
            var max = new Vector2(reelCenterX + halfWidth, At(center, extent, 0f, 0.8f).Y);
            drawList.AddRectFilled(min, max, ink, rounding);
            drawList.AddCircleFilled(At(center, extent, reelColumns[reel], symbolOffsets[reel]), extent * 0.12f,
                hole, 16);
        }
    }

    private static void DrawScratch(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var rounding = extent * 0.16f;
        drawList.AddRectFilled(At(center, extent, -0.85f, -0.65f), At(center, extent, 0.85f, 0.65f), ink, rounding);
        drawList.AddLine(At(center, extent, -0.9f, 0.8f), At(center, extent, 0.5f, -0.85f), hole, extent * 0.34f);
        var pipCenter = At(center, extent, 0.42f, 0.3f);
        var pipRadius = extent * 0.26f;
        Span<Vector2> diamond = stackalloc Vector2[4]
        {
            new(pipCenter.X, pipCenter.Y - pipRadius), new(pipCenter.X + pipRadius * 0.72f, pipCenter.Y),
            new(pipCenter.X, pipCenter.Y + pipRadius), new(pipCenter.X - pipRadius * 0.72f, pipCenter.Y),
        };
        FillConvex(drawList, hole, diamond);
    }

    private static void DrawBarkeep(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        drawList.AddRectFilled(At(center, extent, -0.55f, -0.55f), At(center, extent, 0.35f, 0.85f), ink,
            extent * 0.14f);
        drawList.AddCircle(At(center, extent, 0.55f, 0.15f), extent * 0.3f, ink, 24, extent * 0.14f);
        drawList.AddCircleFilled(At(center, extent, -0.38f, -0.62f), extent * 0.2f, ink, 16);
        drawList.AddCircleFilled(At(center, extent, -0.1f, -0.74f), extent * 0.2f, ink, 16);
        drawList.AddCircleFilled(At(center, extent, 0.18f, -0.62f), extent * 0.2f, ink, 16);
    }

    private static void DrawBingo(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<float> tracks = stackalloc float[3] { -0.6f, 0f, 0.6f };
        var radius = extent * 0.24f;
        for (var row = 0; row < tracks.Length; row++)
        {
            for (var column = 0; column < tracks.Length; column++)
            {
                drawList.AddCircleFilled(At(center, extent, tracks[column], tracks[row]), radius, ink, 16);
            }
        }

        drawList.AddCircleFilled(center, radius * 0.45f, hole, 12);
    }

    private static void DrawWheel(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddCircleFilled(center, extent * 0.85f, ink, 40);
        var spokeThickness = extent * 0.1f;
        for (var spoke = 0; spoke < 3; spoke++)
        {
            var angle = spoke * (MathF.PI / 3f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * extent * 0.85f;
            drawList.AddLine(center - direction, center + direction, hole, spokeThickness);
        }

        drawList.AddCircle(center, extent * 0.5f, hole, 32, extent * 0.08f);
        drawList.AddCircleFilled(center, extent * 0.18f, hole, 16);
    }

    private static void DrawDailySpin(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddCircleFilled(center, extent * 0.85f, ink, 40);
        var spokeThickness = extent * 0.09f;
        for (var spoke = 0; spoke < 4; spoke++)
        {
            var angle = spoke * (MathF.PI / 4f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * extent * 0.85f;
            drawList.AddLine(center - direction, center + direction, hole, spokeThickness);
        }

        drawList.AddCircleFilled(center, extent * 0.3f, hole, 20);
        drawList.AddCircleFilled(center, extent * 0.16f, ink, 16);
    }

    private static void DrawMines(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var spikeThickness = extent * 0.16f;
        for (var spoke = 0; spoke < 4; spoke++)
        {
            var angle = spoke * (MathF.PI * 0.25f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * extent * 0.9f;
            drawList.AddLine(center - direction, center + direction, ink, spikeThickness);
        }

        drawList.AddCircleFilled(center, extent * 0.62f, ink, 32);
        drawList.AddCircleFilled(At(center, extent, -0.2f, -0.2f), extent * 0.16f, hole, 12);
    }

    private static void DrawDice(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddRectFilled(At(center, extent, -0.8f, -0.8f), At(center, extent, 0.8f, 0.8f), ink, extent * 0.26f);
        Span<float> pips = stackalloc float[3] { -0.42f, 0f, 0.42f };
        var radius = extent * 0.14f;
        for (var pip = 0; pip < pips.Length; pip++)
        {
            drawList.AddCircleFilled(At(center, extent, pips[pip], pips[pip]), radius, hole, 12);
        }
    }

    private static void DrawLimbo(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.PathClear();
        drawList.PathArcTo(center, extent * 0.78f, MathF.PI * 0.8f, MathF.PI * 2.2f, 32);
        drawList.PathStroke(ink, ImDrawFlags.None, extent * 0.22f);
        var tip = At(center, extent, 0.42f, -0.5f);
        drawList.AddLine(center, tip, ink, extent * 0.18f);
        drawList.AddCircleFilled(center, extent * 0.2f, ink, 16);
        drawList.AddCircleFilled(center, extent * 0.08f, hole, 10);
    }

    private static void DrawKeno(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<float> tracks = stackalloc float[3] { -0.6f, 0f, 0.6f };
        var radius = extent * 0.22f;
        for (var row = 0; row < tracks.Length; row++)
        {
            for (var column = 0; column < tracks.Length; column++)
            {
                var spot = At(center, extent, tracks[column], tracks[row]);
                if ((row + column) % 2 == 0)
                {
                    drawList.AddCircleFilled(spot, radius, ink, 16);
                    continue;
                }

                drawList.AddCircle(spot, radius * 0.85f, ink, 16, extent * 0.08f);
            }
        }

        drawList.AddCircleFilled(center, radius * 0.4f, hole, 10);
    }

    private static void DrawHiLo(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var cardMin = At(center, extent, -0.5f, -0.8f);
        var cardMax = At(center, extent, 0.5f, 0.8f);
        drawList.AddRectFilled(cardMin, cardMax, ink, extent * 0.16f);
        Span<Vector2> up = stackalloc Vector2[3]
        {
            At(center, extent, 0f, -0.55f), At(center, extent, 0.26f, -0.12f), At(center, extent, -0.26f, -0.12f),
        };
        FillConvex(drawList, hole, up);
        Span<Vector2> down = stackalloc Vector2[3]
        {
            At(center, extent, -0.26f, 0.12f), At(center, extent, 0.26f, 0.12f), At(center, extent, 0f, 0.55f),
        };
        FillConvex(drawList, hole, down);
    }

    private static void DrawRace(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddCircleFilled(At(center, extent, -0.12f, 0.12f), extent * 0.5f, ink, 28);
        drawList.AddLine(At(center, extent, 0.12f, -0.1f), At(center, extent, 0.42f, -0.62f), ink, extent * 0.3f);
        drawList.AddCircleFilled(At(center, extent, 0.46f, -0.66f), extent * 0.22f, ink, 18);
        Span<Vector2> beak = stackalloc Vector2[3]
        {
            At(center, extent, 0.62f, -0.76f), At(center, extent, 0.95f, -0.62f), At(center, extent, 0.62f, -0.54f),
        };
        FillConvex(drawList, ink, beak);
        Span<Vector2> crest = stackalloc Vector2[3]
        {
            At(center, extent, 0.36f, -0.82f), At(center, extent, 0.16f, -1f), At(center, extent, 0.5f, -0.86f),
        };
        FillConvex(drawList, ink, crest);
        drawList.AddCircleFilled(At(center, extent, 0.5f, -0.7f), extent * 0.06f, hole, 8);
        drawList.AddLine(At(center, extent, -0.2f, 0.5f), At(center, extent, -0.42f, 0.95f), ink, extent * 0.12f);
        drawList.AddLine(At(center, extent, 0.04f, 0.5f), At(center, extent, 0.28f, 0.95f), ink, extent * 0.12f);
        drawList.AddCircleFilled(At(center, extent, -0.18f, 0.1f), extent * 0.2f, hole, 16);
    }

    private static void DrawPlinko(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var pegRadius = extent * 0.1f;
        for (var row = 0; row < 3; row++)
        {
            var y = -0.42f + row * 0.36f;
            for (var column = 0; column <= row + 1; column++)
            {
                var x = (column - (row + 1) * 0.5f) * 0.42f;
                drawList.AddCircleFilled(At(center, extent, x, y), pegRadius, ink, 10);
            }
        }

        drawList.AddRectFilled(At(center, extent, -0.86f, 0.6f), At(center, extent, 0.86f, 0.86f), ink,
            extent * 0.1f);
        var ball = At(center, extent, 0.21f, -0.24f);
        drawList.AddCircleFilled(ball, extent * 0.2f, hole, 14);
        drawList.AddCircleFilled(ball, extent * 0.14f, ink, 14);
    }

    private static void DrawBird(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddCircleFilled(At(center, extent, 0f, 0.15f), extent * 0.42f, ink, 28);
        drawList.AddCircleFilled(At(center, extent, 0.32f, -0.38f), extent * 0.24f, ink, 20);
        Span<Vector2> wing = stackalloc Vector2[3]
        {
            At(center, extent, -0.2f, 0f),
            At(center, extent, -0.95f, -0.75f),
            At(center, extent, -0.5f, 0.35f),
        };
        FillConvex(drawList, ink, wing);
        Span<Vector2> beak = stackalloc Vector2[3]
        {
            At(center, extent, 0.5f, -0.46f),
            At(center, extent, 0.85f, -0.36f),
            At(center, extent, 0.5f, -0.26f),
        };
        FillConvex(drawList, ink, beak);
        drawList.AddCircleFilled(At(center, extent, 0.36f, -0.42f), extent * 0.06f, hole, 10);
    }

    private static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<Vector2> gem = stackalloc Vector2[5]
        {
            At(center, extent, 0f, -0.9f),
            At(center, extent, 0.62f, -0.3f),
            At(center, extent, 0.38f, 0.85f),
            At(center, extent, -0.38f, 0.85f),
            At(center, extent, -0.62f, -0.3f),
        };
        FillConvex(drawList, ink, gem);
        var thickness = MathF.Max(1f, extent * 0.1f);
        drawList.AddLine(gem[0], At(center, extent, 0f, 0.85f), hole, thickness);
        drawList.AddLine(gem[4], gem[1], hole, thickness);
    }

    private static void DrawPom(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddCircleFilled(At(center, extent, 0f, 0.2f), extent * 0.66f, ink, 32);
        drawList.AddCircle(At(center, extent, 0f, 0.2f), extent * 0.44f, hole, 28, extent * 0.1f);
        drawList.AddLine(At(center, extent, 0f, -0.46f), At(center, extent, 0f, -0.72f), ink, extent * 0.1f);
        drawList.AddCircleFilled(At(center, extent, 0f, -0.82f), extent * 0.2f, ink, 16);
    }

    private static void DrawChip(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddCircleFilled(center, extent * 0.85f, ink, 40);
        var notchThickness = extent * 0.22f;
        for (var notch = 0; notch < 4; notch++)
        {
            var angle = MathF.PI * 0.25f + notch * (MathF.PI * 0.5f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + direction * extent * 0.55f, center + direction * extent * 0.9f, hole,
                notchThickness);
        }

        drawList.AddCircle(center, extent * 0.5f, hole, 32, extent * 0.08f);
    }

    private static void DrawSpade(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        var lobe = radius * 0.5f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, center.Y + radius * 0.2f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, center.Y + radius * 0.2f), lobe, packed, 20);
        Span<Vector2> triangle = stackalloc Vector2[3]
        {
            new(center.X - radius * 0.98f, center.Y + radius * 0.08f),
            new(center.X + radius * 0.98f, center.Y + radius * 0.08f), new(center.X, center.Y - radius),
        };
        FillConvex(drawList, packed, triangle);
        Span<Vector2> stem = stackalloc Vector2[4]
        {
            new(center.X - radius * 0.12f, center.Y + radius * 0.18f),
            new(center.X + radius * 0.12f, center.Y + radius * 0.18f),
            new(center.X + radius * 0.26f, center.Y + radius * 0.98f),
            new(center.X - radius * 0.26f, center.Y + radius * 0.98f),
        };
        FillConvex(drawList, packed, stem);
    }

    private static Vector2 At(Vector2 center, float extent, float unitX, float unitY)
    {
        return new Vector2(center.X + unitX * extent, center.Y + unitY * extent);
    }

    private static void FillConvex(ImDrawListPtr drawList, uint color, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathFillConvex(color);
    }
}
