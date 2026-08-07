using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AppIconArt
{
    public static bool TryDraw(string id, Vector2 center, float size, Vector4 ink, Vector4 hole) =>
        TryDraw(ImGui.GetWindowDrawList(), id, center, size, ink, hole);

    public static bool TryDraw(ImDrawListPtr dl, string id, Vector2 center, float size, Vector4 ink, Vector4 hole)
    {
        if (AppIconTextures.TryDraw(dl, id, center, size, ink))
        {
            return true;
        }

        var extent = size * 0.30f;
        var inkColor = ImGui.GetColorU32(ink);
        var holeColor = ImGui.GetColorU32(hole);
        switch (id)
        {
            case "minesweeper":
                DrawMine(dl, center, extent, inkColor, holeColor);
                return true;
            case "memory":
                DrawMemory(dl, center, extent, inkColor, holeColor);
                return true;
            case "match3":
                DrawGem(dl, center, extent, inkColor, holeColor);
                return true;
            case "2048":
                DrawTiles(dl, center, extent, inkColor);
                return true;
            case "breakout":
                DrawBreakout(dl, center, extent, inkColor);
                return true;
            case "bubbles":
                DrawBubbles(dl, center, extent, inkColor, holeColor);
                return true;
            case "watersort":
                DrawWaterSort(dl, center, extent, inkColor);
                return true;
            case "nonogram":
                DrawNonogram(dl, center, extent, inkColor);
                return true;
            case "flow":
                DrawFlow(dl, center, extent, inkColor, holeColor);
                return true;
            case "solitaire":
                DrawSolitaire(dl, center, extent, inkColor, holeColor);
                return true;
            case "simon":
                DrawSimon(dl, center, extent, inkColor, holeColor);
                return true;
            case "flap":
                DrawFlap(dl, center, extent, inkColor, holeColor);
                return true;
            case "reversi":
                DrawReversi(dl, center, extent, inkColor, holeColor);
                return true;
            case "whack":
                DrawWhack(dl, center, extent, inkColor, holeColor);
                return true;
            case "snake":
                DrawSnake(dl, center, extent, inkColor, holeColor);
                return true;
            case "tetris":
                DrawTetris(dl, center, extent, inkColor);
                return true;
            case "sudoku":
                DrawSudoku(dl, center, extent, inkColor, holeColor);
                return true;
            case "chess":
                DrawChess(dl, center, extent, inkColor, holeColor);
                return true;
            case "stack":
                DrawStack(dl, center, extent, inkColor);
                return true;
            case "crystaldrop":
                DrawCrystalDrop(dl, center, extent, inkColor, holeColor);
                return true;
            case "beat":
                DrawBeat(dl, center, extent, inkColor);
                return true;
            case "blade":
                DrawBladeThrow(dl, center, extent, inkColor, holeColor);
                return true;
            case "trivia":
                DrawTrivia(dl, center, extent, inkColor, holeColor);
                return true;
            default:
                return false;
        }
    }

    private static void DrawMine(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var spikeThickness = extent * 0.16f;
        for (var spike = 0; spike < 8; spike++)
        {
            var angle = spike * (MathF.PI / 4f);
            dl.AddLine(Polar(center, extent, 0.45f, angle), Polar(center, extent, 0.98f, angle), ink, spikeThickness);
        }

        dl.AddCircleFilled(center, extent * 0.62f, ink, 32);
        dl.AddCircleFilled(At(center, extent, -0.22f, -0.22f), extent * 0.16f, hole, 16);
    }

    private static void DrawMemory(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var backMin = At(center, extent, -0.10f, -0.90f);
        var backMax = At(center, extent, 0.85f, 0.45f);
        dl.AddRectFilled(backMin, backMax, ink, extent * 0.16f);
        var outlineMin = At(center, extent, -0.92f, -0.47f);
        var outlineMax = At(center, extent, 0.20f, 0.92f);
        dl.AddRectFilled(outlineMin, outlineMax, hole, extent * 0.20f);
        var frontMin = At(center, extent, -0.80f, -0.35f);
        var frontMax = At(center, extent, 0.08f, 0.80f);
        dl.AddRectFilled(frontMin, frontMax, ink, extent * 0.16f);
        var symbol = At(center, extent, -0.36f, 0.22f);
        Span<Vector2> diamond = stackalloc Vector2[4]
        {
            new(symbol.X, symbol.Y - extent * 0.28f), new(symbol.X + extent * 0.24f, symbol.Y),
            new(symbol.X, symbol.Y + extent * 0.28f), new(symbol.X - extent * 0.24f, symbol.Y),
        };
        FillConvex(dl, hole, diamond);
    }

    private static void DrawGem(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<Vector2> gem = stackalloc Vector2[5]
        {
            At(center, extent, -0.60f, -0.55f), At(center, extent, 0.60f, -0.55f),
            At(center, extent, 0.92f, -0.05f), At(center, extent, 0f, 0.92f), At(center, extent, -0.92f, -0.05f),
        };
        FillConvex(dl, ink, gem);
        var facetThickness = extent * 0.06f;
        dl.AddLine(At(center, extent, -0.92f, -0.05f), At(center, extent, 0.92f, -0.05f), hole, facetThickness);
        dl.AddLine(At(center, extent, 0f, -0.55f), At(center, extent, 0f, -0.05f), hole, facetThickness);
        dl.AddLine(At(center, extent, -0.60f, -0.55f), At(center, extent, 0f, 0.92f), hole, facetThickness);
        dl.AddLine(At(center, extent, 0.60f, -0.55f), At(center, extent, 0f, 0.92f), hole, facetThickness);
    }

    private static void DrawTiles(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        var tileExtent = extent * 0.40f;
        var rounding = extent * 0.14f;
        Span<Vector2> tileCenters = stackalloc Vector2[4]
        {
            At(center, extent, -0.45f, -0.45f), At(center, extent, 0.45f, -0.45f),
            At(center, extent, -0.45f, 0.45f), At(center, extent, 0.45f, 0.45f),
        };
        for (var tile = 0; tile < tileCenters.Length; tile++)
        {
            var tileCenter = tileCenters[tile];
            var tileMin = new Vector2(tileCenter.X - tileExtent, tileCenter.Y - tileExtent);
            var tileMax = new Vector2(tileCenter.X + tileExtent, tileCenter.Y + tileExtent);
            dl.AddRectFilled(tileMin, tileMax, ink, rounding);
        }
    }

    private static void DrawBreakout(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        var brickWidth = extent * 0.30f;
        var brickHeight = extent * 0.16f;
        var rounding = extent * 0.06f;
        Span<float> columns = stackalloc float[3] { -0.62f, 0f, 0.62f };
        for (var column = 0; column < columns.Length; column++)
        {
            var brick = At(center, extent, columns[column], -0.72f);
            dl.AddRectFilled(new Vector2(brick.X - brickWidth, brick.Y - brickHeight),
                new Vector2(brick.X + brickWidth, brick.Y + brickHeight), ink, rounding);
        }

        dl.AddCircleFilled(At(center, extent, 0.18f, 0.05f), extent * 0.15f, ink);
        var paddle = At(center, extent, 0f, 0.78f);
        var paddleWidth = extent * 0.52f;
        var paddleHeight = extent * 0.12f;
        dl.AddRectFilled(new Vector2(paddle.X - paddleWidth, paddle.Y - paddleHeight),
            new Vector2(paddle.X + paddleWidth, paddle.Y + paddleHeight), ink, paddleHeight);
    }

    private static void DrawBubbles(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<Vector2> bubbles = stackalloc Vector2[5]
        {
            At(center, extent, -0.46f, -0.42f), At(center, extent, 0.46f, -0.42f), At(center, extent, 0f, 0.04f),
            At(center, extent, -0.42f, 0.5f), At(center, extent, 0.42f, 0.5f),
        };
        var radius = extent * 0.34f;
        for (var bubble = 0; bubble < bubbles.Length; bubble++)
        {
            dl.AddCircleFilled(bubbles[bubble], radius, ink);
            dl.AddCircleFilled(new Vector2(bubbles[bubble].X - radius * 0.32f, bubbles[bubble].Y - radius * 0.32f),
                radius * 0.3f, hole);
        }
    }

    private static void DrawWaterSort(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        Span<float> tubeColumns = stackalloc float[2] { -0.5f, 0.5f };
        Span<float> fillFractions = stackalloc float[2] { 0.62f, 0.86f };
        var halfWidth = extent * 0.26f;
        var topY = At(center, extent, 0f, -0.82f).Y;
        var bottomY = At(center, extent, 0f, 0.84f).Y;
        var thickness = extent * 0.09f;
        var inset = extent * 0.05f;
        for (var tube = 0; tube < tubeColumns.Length; tube++)
        {
            var centerX = At(center, extent, tubeColumns[tube], 0f).X;
            var min = new Vector2(centerX - halfWidth, topY);
            var max = new Vector2(centerX + halfWidth, bottomY);
            dl.AddRect(min, max, ink, halfWidth, ImDrawFlags.RoundCornersBottom, thickness);
            var fillTopY = bottomY - (bottomY - topY) * fillFractions[tube];
            dl.AddRectFilled(new Vector2(min.X + inset, fillTopY), new Vector2(max.X - inset, max.Y - inset), ink,
                halfWidth - inset, ImDrawFlags.RoundCornersBottom);
        }
    }

    private static void DrawNonogram(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        Span<float> tracks = stackalloc float[3] { -0.6f, 0f, 0.6f };
        var half = extent * 0.26f;
        var rounding = extent * 0.06f;
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var cell = At(center, extent, tracks[column], tracks[row]);
                var min = new Vector2(cell.X - half, cell.Y - half);
                var max = new Vector2(cell.X + half, cell.Y + half);
                if ((row + column) % 2 == 0)
                {
                    dl.AddRectFilled(min, max, ink, rounding);
                }
                else
                {
                    dl.AddRect(min, max, ink, rounding, ImDrawFlags.RoundCornersAll, extent * 0.05f);
                }
            }
        }
    }

    private static void DrawFlow(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var thickness = extent * 0.2f;
        var first = At(center, extent, -0.55f, -0.45f);
        var second = At(center, extent, -0.55f, 0.5f);
        var third = At(center, extent, 0.55f, 0.5f);
        var fourth = At(center, extent, 0.55f, -0.45f);
        dl.AddLine(first, second, ink, thickness);
        dl.AddLine(second, third, ink, thickness);
        dl.AddLine(third, fourth, ink, thickness);
        dl.AddCircleFilled(second, thickness * 0.5f, ink, 16);
        dl.AddCircleFilled(third, thickness * 0.5f, ink, 16);
        var dotRadius = extent * 0.22f;
        dl.AddCircleFilled(first, dotRadius, ink, 24);
        dl.AddCircleFilled(fourth, dotRadius, ink, 24);
        dl.AddCircleFilled(first - new Vector2(dotRadius * 0.3f, dotRadius * 0.3f), dotRadius * 0.34f, hole, 16);
        dl.AddCircleFilled(fourth - new Vector2(dotRadius * 0.3f, dotRadius * 0.3f), dotRadius * 0.34f, hole, 16);
    }

    private static void DrawSolitaire(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var rounding = extent * 0.16f;
        var backMin = At(center, extent, -0.18f, -0.82f);
        var backMax = At(center, extent, 0.82f, 0.5f);
        dl.AddRectFilled(backMin, backMax, ink, rounding);
        var gap = extent * 0.08f;
        var frontMin = At(center, extent, -0.82f, -0.5f);
        var frontMax = At(center, extent, 0.18f, 0.82f);
        dl.AddRectFilled(frontMin - new Vector2(gap, gap), frontMax + new Vector2(gap, gap), hole, rounding);
        dl.AddRectFilled(frontMin, frontMax, ink, rounding);
        var pip = (frontMin + frontMax) * 0.5f;
        var pipRadius = extent * 0.24f;
        Span<Vector2> diamond = stackalloc Vector2[4]
        {
            new(pip.X, pip.Y - pipRadius), new(pip.X + pipRadius * 0.72f, pip.Y), new(pip.X, pip.Y + pipRadius),
            new(pip.X - pipRadius * 0.72f, pip.Y),
        };
        FillConvex(dl, hole, diamond);
    }

    private static void DrawSimon(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<float> tracks = stackalloc float[2] { -0.46f, 0.46f };
        var half = extent * 0.4f;
        var rounding = extent * 0.16f;
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 2; column++)
            {
                var cell = At(center, extent, tracks[column], tracks[row]);
                dl.AddRectFilled(new Vector2(cell.X - half, cell.Y - half), new Vector2(cell.X + half, cell.Y + half),
                    ink, rounding);
            }
        }

        dl.AddCircleFilled(center, extent * 0.3f, hole, 28);
        dl.AddCircleFilled(center, extent * 0.18f, ink, 24);
    }

    private static void DrawFlap(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var topMin = At(center, extent, 0.46f, -1f);
        var topMax = At(center, extent, 0.92f, -0.28f);
        dl.AddRectFilled(topMin, topMax, ink, extent * 0.08f);
        var bottomMin = At(center, extent, 0.46f, 0.32f);
        var bottomMax = At(center, extent, 0.92f, 1f);
        dl.AddRectFilled(bottomMin, bottomMax, ink, extent * 0.08f);
        var bird = At(center, extent, -0.34f, 0.04f);
        var radius = extent * 0.42f;
        dl.AddCircleFilled(bird, radius, ink, 28);
        dl.AddCircleFilled(new Vector2(bird.X + radius * 0.34f, bird.Y - radius * 0.32f), radius * 0.24f, hole, 16);
        Span<Vector2> beak = stackalloc Vector2[3]
        {
            new(bird.X + radius * 0.82f, bird.Y - radius * 0.12f),
            new(bird.X + radius * 1.34f, bird.Y + radius * 0.06f),
            new(bird.X + radius * 0.82f, bird.Y + radius * 0.28f),
        };
        FillConvex(dl, ink, beak);
    }

    private static void DrawReversi(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<float> tracks = stackalloc float[2] { -0.44f, 0.44f };
        var radius = extent * 0.36f;
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 2; column++)
            {
                var cell = At(center, extent, tracks[column], tracks[row]);
                dl.AddCircleFilled(cell, radius, ink, 28);
                if ((row + column) % 2 != 0)
                {
                    dl.AddCircleFilled(cell, radius * 0.6f, hole, 24);
                }
            }
        }
    }

    private static void DrawWhack(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var mole = At(center, extent, 0f, -0.05f);
        var radius = extent * 0.52f;
        dl.AddCircleFilled(mole, radius, ink, 30);
        dl.AddCircleFilled(new Vector2(mole.X - radius * 0.34f, mole.Y - radius * 0.14f), radius * 0.16f, hole, 12);
        dl.AddCircleFilled(new Vector2(mole.X + radius * 0.34f, mole.Y - radius * 0.14f), radius * 0.16f, hole, 12);
        dl.AddCircleFilled(new Vector2(mole.X, mole.Y + radius * 0.16f), radius * 0.14f, hole, 12);
        var lip = At(center, extent, 0f, 0.72f);
        dl.AddRectFilled(new Vector2(lip.X - extent * 0.92f, lip.Y - extent * 0.18f),
            new Vector2(lip.X + extent * 0.92f, lip.Y + extent * 0.32f), hole, extent * 0.2f);
    }

    private static void DrawSnake(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<Vector2> body = stackalloc Vector2[5]
        {
            At(center, extent, -0.72f, 0.4f), At(center, extent, -0.36f, -0.08f), At(center, extent, 0.02f, 0.3f),
            At(center, extent, 0.4f, -0.12f), At(center, extent, 0.66f, 0.12f),
        };
        var radius = extent * 0.24f;
        for (var index = 0; index < body.Length; index++)
        {
            dl.AddCircleFilled(body[index], radius * (0.62f + 0.08f * index), ink, 20);
        }

        var head = At(center, extent, 0.82f, -0.08f);
        dl.AddCircleFilled(head, radius * 1.15f, ink, 24);
        dl.AddCircleFilled(new Vector2(head.X + radius * 0.34f, head.Y - radius * 0.34f), radius * 0.24f, hole, 12);
        dl.AddCircleFilled(At(center, extent, -0.86f, -0.6f), extent * 0.16f, ink, 16);
    }

    private static void DrawTetris(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        var blockExtent = extent * 0.30f;
        var rounding = extent * 0.10f;
        Span<Vector2> blockCenters = stackalloc Vector2[4]
        {
            At(center, extent, -0.66f, -0.33f), At(center, extent, 0f, -0.33f), At(center, extent, 0.66f, -0.33f),
            At(center, extent, 0f, 0.33f),
        };
        for (var block = 0; block < blockCenters.Length; block++)
        {
            var blockCenter = blockCenters[block];
            var blockMin = new Vector2(blockCenter.X - blockExtent, blockCenter.Y - blockExtent);
            var blockMax = new Vector2(blockCenter.X + blockExtent, blockCenter.Y + blockExtent);
            dl.AddRectFilled(blockMin, blockMax, ink, rounding);
        }
    }

    private static void DrawSudoku(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var half = extent * 0.92f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var thickness = extent * 0.15f;
        dl.AddRect(min, max, ink, extent * 0.18f, ImDrawFlags.RoundCornersAll, thickness);
        var step = half * 2f / 3f;
        for (var line = 1; line < 3; line++)
        {
            var offset = line * step;
            dl.AddLine(new Vector2(min.X + offset, min.Y), new Vector2(min.X + offset, max.Y), ink, thickness * 0.7f);
            dl.AddLine(new Vector2(min.X, min.Y + offset), new Vector2(max.X, min.Y + offset), ink, thickness * 0.7f);
        }

        var cell = step * 0.5f;
        Span<Vector2> marks = stackalloc Vector2[3]
        {
            new(min.X + cell, min.Y + cell), new(min.X + step + cell, min.Y + step + cell),
            new(min.X + step * 2f + cell, min.Y + cell),
        };
        for (var mark = 0; mark < marks.Length; mark++)
        {
            dl.AddCircleFilled(marks[mark], extent * 0.16f, ink, 16);
        }

        dl.AddCircleFilled(new Vector2(min.X + cell, min.Y + step * 2f + cell), extent * 0.16f, hole, 16);
    }

    private static void DrawChess(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var headRadius = extent * 0.30f;
        var head = At(center, extent, 0f, -0.52f);
        dl.AddCircleFilled(head, headRadius, ink, 24);
        var collarTop = At(center, extent, 0f, -0.20f);
        var collarHalf = extent * 0.20f;
        dl.AddRectFilled(new Vector2(collarTop.X - collarHalf, collarTop.Y - extent * 0.06f),
            new Vector2(collarTop.X + collarHalf, collarTop.Y + extent * 0.10f), ink, extent * 0.05f);
        Span<Vector2> body = stackalloc Vector2[4]
        {
            At(center, extent, -0.16f, -0.06f), At(center, extent, 0.16f, -0.06f), At(center, extent, 0.46f, 0.58f),
            At(center, extent, -0.46f, 0.58f),
        };
        FillConvex(dl, ink, body);
        var baseCenter = At(center, extent, 0f, 0.76f);
        var baseHalf = extent * 0.62f;
        dl.AddRectFilled(new Vector2(baseCenter.X - baseHalf, baseCenter.Y - extent * 0.14f),
            new Vector2(baseCenter.X + baseHalf, baseCenter.Y + extent * 0.14f), ink, extent * 0.10f);
        dl.AddCircleFilled(At(center, extent, -0.10f, -0.58f), headRadius * 0.30f, hole, 12);
    }

    private static void DrawStack(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        Span<float> rows = stackalloc float[4] { 0.74f, 0.30f, -0.14f, -0.74f };
        Span<float> halfWidths = stackalloc float[4] { 0.92f, 0.80f, 0.68f, 0.50f };
        Span<float> offsets = stackalloc float[4] { 0f, -0.06f, 0.08f, 0.40f };
        var halfHeight = extent * 0.18f;
        var rounding = extent * 0.08f;
        for (var row = 0; row < rows.Length; row++)
        {
            var bar = At(center, extent, offsets[row], rows[row]);
            var halfWidth = extent * halfWidths[row];
            dl.AddRectFilled(new Vector2(bar.X - halfWidth, bar.Y - halfHeight),
                new Vector2(bar.X + halfWidth, bar.Y + halfHeight), ink, rounding);
        }
    }

    private static void DrawCrystalDrop(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var large = At(center, extent, -0.34f, 0.44f);
        var medium = At(center, extent, 0.48f, 0.54f);
        var small = At(center, extent, 0.14f, -0.18f);
        var falling = At(center, extent, -0.42f, -0.72f);
        dl.AddCircleFilled(large, extent * 0.54f, ink, 28);
        dl.AddCircleFilled(medium, extent * 0.42f, ink, 24);
        dl.AddCircleFilled(small, extent * 0.34f, ink, 22);
        dl.AddCircleFilled(falling, extent * 0.22f, ink, 18);
        dl.AddCircleFilled(new Vector2(large.X - extent * 0.20f, large.Y - extent * 0.20f), extent * 0.14f, hole, 14);
        dl.AddCircleFilled(new Vector2(medium.X - extent * 0.16f, medium.Y - extent * 0.16f), extent * 0.11f, hole, 12);
    }

    private static void DrawBeat(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        Span<float> columns = stackalloc float[4] { -0.72f, -0.24f, 0.24f, 0.72f };
        Span<float> tops = stackalloc float[4] { 0.06f, -0.62f, -0.16f, -0.88f };
        var halfWidth = extent * 0.16f;
        var rounding = extent * 0.14f;
        var bottom = At(center, extent, 0f, 0.92f).Y;
        for (var column = 0; column < columns.Length; column++)
        {
            var bar = At(center, extent, columns[column], tops[column]);
            dl.AddRectFilled(new Vector2(bar.X - halfWidth, bar.Y), new Vector2(bar.X + halfWidth, bottom), ink,
                rounding);
        }
    }

    private static void DrawBladeThrow(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var wheel = At(center, extent, 0f, -0.22f);
        var radius = extent * 0.60f;
        dl.AddCircleFilled(wheel, radius, ink, 36);
        dl.AddCircleFilled(wheel, radius * 0.44f, hole, 24);
        var tip = new Vector2(wheel.X, wheel.Y + radius * 0.98f);
        var halfWidth = extent * 0.13f;
        var bladeBase = new Vector2(tip.X, tip.Y + extent * 0.42f);
        Span<Vector2> blade = stackalloc Vector2[3]
        {
            tip, new(bladeBase.X + halfWidth, bladeBase.Y), new(bladeBase.X - halfWidth, bladeBase.Y),
        };
        FillConvex(dl, ink, blade);
        var handleWidth = halfWidth * 0.7f;
        dl.AddRectFilled(new Vector2(tip.X - handleWidth, bladeBase.Y),
            new Vector2(tip.X + handleWidth, bladeBase.Y + extent * 0.46f), ink, extent * 0.06f);
    }

    private static void DrawTrivia(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var bubble = At(center, extent, 0f, -0.10f);
        var halfWidth = extent * 0.86f;
        var halfHeight = extent * 0.68f;
        dl.AddRectFilled(new Vector2(bubble.X - halfWidth, bubble.Y - halfHeight),
            new Vector2(bubble.X + halfWidth, bubble.Y + halfHeight), ink, extent * 0.26f);
        Span<Vector2> tail = stackalloc Vector2[3]
        {
            new(bubble.X - extent * 0.34f, bubble.Y + halfHeight * 0.92f),
            new(bubble.X - extent * 0.06f, bubble.Y + halfHeight * 0.92f),
            new(bubble.X - extent * 0.30f, bubble.Y + halfHeight + extent * 0.40f),
        };
        FillConvex(dl, ink, tail);
        var markCenter = new Vector2(bubble.X, bubble.Y - extent * 0.12f);
        var markRadius = extent * 0.26f;
        dl.PathArcTo(markCenter, markRadius, -MathF.PI, MathF.PI * 0.42f, 20);
        dl.PathStroke(hole, ImDrawFlags.None, extent * 0.16f);
        dl.AddRectFilled(new Vector2(markCenter.X - extent * 0.08f, markCenter.Y + markRadius * 0.55f),
            new Vector2(markCenter.X + extent * 0.08f, markCenter.Y + markRadius * 1.25f), hole, extent * 0.03f);
        dl.AddCircleFilled(new Vector2(markCenter.X, markCenter.Y + markRadius * 1.72f), extent * 0.10f, hole, 12);
    }

    private static Vector2 At(Vector2 center, float extent, float unitX, float unitY)
    {
        return new Vector2(center.X + unitX * extent, center.Y + unitY * extent);
    }

    private static Vector2 Polar(Vector2 center, float extent, float radius, float angle)
    {
        return new Vector2(center.X + MathF.Cos(angle) * radius * extent,
            center.Y + MathF.Sin(angle) * radius * extent);
    }

    private static void FillConvex(ImDrawListPtr dl, uint color, ReadOnlySpan<Vector2> points)
    {
        dl.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            dl.PathLineTo(points[index]);
        }

        dl.PathFillConvex(color);
    }
}
