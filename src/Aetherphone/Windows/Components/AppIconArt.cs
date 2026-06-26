using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AppIconArt
{
    public static bool TryDraw(string id, Vector2 center, float size, Vector4 ink, Vector4 hole)
    {
        var dl = ImGui.GetWindowDrawList();
        var extent = size * 0.30f;
        var inkColor = ImGui.GetColorU32(ink);
        var holeColor = ImGui.GetColorU32(hole);

        switch (id)
        {
            case "messages":
                DrawMessages(dl, center, extent, inkColor, holeColor);
                return true;
            case "contacts":
                DrawContacts(dl, center, extent, inkColor);
                return true;
            case "character":
                DrawCharacter(dl, center, extent, inkColor, holeColor);
                return true;
            case "camera":
                DrawCamera(dl, center, extent, inkColor, holeColor);
                return true;
            case "photos":
                DrawPhotos(dl, center, extent, inkColor, holeColor);
                return true;
            case "skywatcher":
                DrawSkywatcher(dl, center, extent, inkColor, holeColor);
                return true;
            case "market":
                DrawMarket(dl, center, extent, inkColor);
                return true;
            case "music":
                DrawMusic(dl, center, extent, inkColor);
                return true;
            case "wallet":
                DrawWallet(dl, center, extent, inkColor, holeColor);
                return true;
            case "clock":
                DrawClock(dl, center, extent, inkColor, holeColor);
                return true;
            case "notifications":
                DrawNotifications(dl, center, extent, inkColor);
                return true;
            case "settings":
                DrawSettings(dl, center, extent, inkColor, holeColor);
                return true;
            case "games":
                DrawGames(dl, center, extent, inkColor, holeColor);
                return true;
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
            default:
                return false;
        }
    }

    private static void DrawMessages(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var bubbleMin = At(center, extent, -0.95f, -0.85f);
        var bubbleMax = At(center, extent, 0.95f, 0.35f);
        dl.AddRectFilled(bubbleMin, bubbleMax, ink, extent * 0.5f);

        Span<Vector2> tail = stackalloc Vector2[3]
        {
            At(center, extent, -0.55f, 0.10f),
            At(center, extent, -0.16f, 0.10f),
            At(center, extent, -0.62f, 0.94f),
        };
        FillConvex(dl, ink, tail);

        var dotRadius = extent * 0.12f;
        dl.AddCircleFilled(At(center, extent, -0.42f, -0.25f), dotRadius, hole, 16);
        dl.AddCircleFilled(At(center, extent, 0f, -0.25f), dotRadius, hole, 16);
        dl.AddCircleFilled(At(center, extent, 0.42f, -0.25f), dotRadius, hole, 16);
    }

    private static void DrawContacts(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        dl.AddCircleFilled(At(center, extent, 0f, -0.42f), extent * 0.40f, ink, 32);

        dl.PathClear();
        dl.PathArcTo(At(center, extent, 0f, 1.05f), extent * 0.92f, MathF.PI, MathF.PI * 2f, 32);
        dl.PathFillConvex(ink);
    }

    private static void DrawCharacter(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        dl.AddCircleFilled(At(center, extent, 0f, -0.16f), extent * 0.32f, ink, 32);
        dl.AddCircleFilled(At(center, extent, 0f, 0.92f), extent * 0.80f, ink, 48);

        dl.AddCircle(center, extent * 1.08f, hole, 64, extent * 0.60f);
        dl.AddCircle(center, extent * 0.87f, ink, 48, extent * 0.11f);
    }

    private static void DrawCamera(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var humpMin = At(center, extent, -0.45f, -0.80f);
        var humpMax = At(center, extent, 0.20f, -0.40f);
        dl.AddRectFilled(humpMin, humpMax, ink, extent * 0.12f);

        var bodyMin = At(center, extent, -0.98f, -0.55f);
        var bodyMax = At(center, extent, 0.98f, 0.78f);
        dl.AddRectFilled(bodyMin, bodyMax, ink, extent * 0.26f);

        var lensCenter = At(center, extent, 0f, 0.18f);
        dl.AddCircleFilled(lensCenter, extent * 0.42f, hole, 32);
        dl.AddCircleFilled(lensCenter, extent * 0.24f, ink, 32);

        dl.AddCircleFilled(At(center, extent, 0.66f, -0.30f), extent * 0.10f, hole, 16);
    }

    private static void DrawPhotos(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var outerMin = At(center, extent, -0.95f, -0.82f);
        var outerMax = At(center, extent, 0.95f, 0.82f);
        dl.AddRectFilled(outerMin, outerMax, ink, extent * 0.30f);

        var innerMin = At(center, extent, -0.74f, -0.61f);
        var innerMax = At(center, extent, 0.74f, 0.61f);
        dl.AddRectFilled(innerMin, innerMax, hole, extent * 0.18f);

        dl.AddCircleFilled(At(center, extent, -0.34f, -0.26f), extent * 0.20f, ink, 24);

        Span<Vector2> ridge = stackalloc Vector2[3]
        {
            At(center, extent, -0.74f, 0.61f),
            At(center, extent, -0.02f, -0.12f),
            At(center, extent, 0.50f, 0.61f),
        };
        FillConvex(dl, ink, ridge);

        Span<Vector2> ridgeBack = stackalloc Vector2[3]
        {
            At(center, extent, 0.12f, 0.61f),
            At(center, extent, 0.48f, 0.08f),
            At(center, extent, 0.74f, 0.61f),
        };
        FillConvex(dl, ink, ridgeBack);
    }

    private static void DrawSkywatcher(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var sunCenter = At(center, extent, -0.40f, -0.42f);
        var rayThickness = extent * 0.10f;
        for (var ray = 0; ray < 8; ray++)
        {
            var angle = ray * (MathF.PI / 4f);
            dl.AddLine(Polar(sunCenter, extent, 0.40f, angle), Polar(sunCenter, extent, 0.60f, angle), ink, rayThickness);
        }

        dl.AddCircleFilled(sunCenter, extent * 0.30f, ink, 32);

        DrawCloud(dl, center, extent, hole, 0.14f);
        DrawCloud(dl, center, extent, ink, 0f);
    }

    private static void DrawMarket(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        var rounding = extent * 0.12f;

        var firstMin = At(center, extent, -0.82f, 0.22f);
        var firstMax = At(center, extent, -0.34f, 0.82f);
        dl.AddRectFilled(firstMin, firstMax, ink, rounding);

        var secondMin = At(center, extent, -0.24f, -0.18f);
        var secondMax = At(center, extent, 0.24f, 0.82f);
        dl.AddRectFilled(secondMin, secondMax, ink, rounding);

        var thirdMin = At(center, extent, 0.34f, -0.62f);
        var thirdMax = At(center, extent, 0.82f, 0.82f);
        dl.AddRectFilled(thirdMin, thirdMax, ink, rounding);
    }

    private static void DrawMusic(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        dl.AddCircleFilled(At(center, extent, -0.34f, 0.52f), extent * 0.34f, ink, 32);
        dl.AddCircleFilled(At(center, extent, 0.46f, 0.30f), extent * 0.34f, ink, 32);

        var leftStemMin = At(center, extent, -0.06f, -0.78f);
        var leftStemMax = At(center, extent, 0.06f, 0.52f);
        dl.AddRectFilled(leftStemMin, leftStemMax, ink, extent * 0.05f);

        var rightStemMin = At(center, extent, 0.74f, -0.96f);
        var rightStemMax = At(center, extent, 0.86f, 0.30f);
        dl.AddRectFilled(rightStemMin, rightStemMax, ink, extent * 0.05f);

        Span<Vector2> beam = stackalloc Vector2[4]
        {
            At(center, extent, -0.06f, -0.78f),
            At(center, extent, 0.86f, -0.96f),
            At(center, extent, 0.86f, -0.62f),
            At(center, extent, -0.06f, -0.44f),
        };
        FillConvex(dl, ink, beam);
    }

    private static void DrawWallet(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        var cardMin = At(center, extent, -0.34f, -0.78f);
        var cardMax = At(center, extent, 0.46f, -0.40f);
        dl.AddRectFilled(cardMin, cardMax, hole, extent * 0.08f);

        var bodyMin = At(center, extent, -0.92f, -0.52f);
        var bodyMax = At(center, extent, 0.92f, 0.74f);
        dl.AddRectFilled(bodyMin, bodyMax, ink, extent * 0.22f);

        var pocketMin = At(center, extent, 0.34f, 0.12f);
        var pocketMax = At(center, extent, 0.92f, 0.50f);
        dl.AddRectFilled(pocketMin, pocketMax, hole, extent * 0.14f);

        dl.AddCircleFilled(At(center, extent, 0.60f, 0.31f), extent * 0.12f, ink, 24);
    }

    private static void DrawCloud(ImDrawListPtr dl, Vector2 center, float extent, uint color, float inflate)
    {
        dl.AddCircleFilled(At(center, extent, -0.28f, 0.16f), (0.34f + inflate) * extent, color, 24);
        dl.AddCircleFilled(At(center, extent, 0.22f, -0.04f), (0.46f + inflate) * extent, color, 32);
        dl.AddCircleFilled(At(center, extent, 0.62f, 0.20f), (0.30f + inflate) * extent, color, 24);

        var baseMin = At(center, extent, -0.58f - inflate, 0.16f);
        var baseMax = At(center, extent, 0.80f + inflate, 0.64f + inflate);
        dl.AddRectFilled(baseMin, baseMax, color, extent * 0.24f);
    }

    private static void DrawClock(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        dl.AddCircleFilled(center, extent * 0.96f, ink, 48);

        for (var tick = 0; tick < 12; tick++)
        {
            var angle = tick * (MathF.PI / 6f);
            var major = tick % 3 == 0;
            dl.AddLine(Polar(center, extent, 0.74f, angle), Polar(center, extent, 0.88f, angle), hole, (major ? 0.10f : 0.05f) * extent);
        }

        const float minuteAngle = -MathF.PI / 6f;
        const float hourAngle = -MathF.PI * 5f / 6f;
        dl.AddLine(center, Polar(center, extent, 0.70f, minuteAngle), hole, extent * 0.09f);
        dl.AddLine(center, Polar(center, extent, 0.46f, hourAngle), hole, extent * 0.12f);

        dl.AddCircleFilled(center, extent * 0.10f, hole, 16);
    }

    private static void DrawNotifications(ImDrawListPtr dl, Vector2 center, float extent, uint ink)
    {
        dl.AddCircleFilled(At(center, extent, 0f, -0.82f), extent * 0.13f, ink, 16);

        Span<Vector2> body = stackalloc Vector2[4]
        {
            At(center, extent, -0.58f, 0.42f),
            At(center, extent, 0.58f, 0.42f),
            At(center, extent, 0.40f, -0.30f),
            At(center, extent, -0.40f, -0.30f),
        };
        FillConvex(dl, ink, body);

        dl.PathClear();
        dl.PathArcTo(At(center, extent, 0f, -0.30f), extent * 0.40f, MathF.PI, MathF.PI * 2f, 24);
        dl.PathFillConvex(ink);

        var rimMin = At(center, extent, -0.72f, 0.40f);
        var rimMax = At(center, extent, 0.72f, 0.60f);
        dl.AddRectFilled(rimMin, rimMax, ink, extent * 0.10f);

        dl.AddCircleFilled(At(center, extent, 0f, 0.80f), extent * 0.15f, ink, 16);
    }

    private static void DrawSettings(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        const int teeth = 8;
        const float innerRadius = 0.50f;
        const float outerRadius = 0.98f;
        const float baseHalf = 0.30f;
        const float tipHalf = 0.18f;

        Span<Vector2> quad = stackalloc Vector2[4];
        for (var tooth = 0; tooth < teeth; tooth++)
        {
            var angle = tooth * (MathF.PI * 2f / teeth);
            quad[0] = Polar(center, extent, innerRadius, angle - baseHalf);
            quad[1] = Polar(center, extent, outerRadius, angle - tipHalf);
            quad[2] = Polar(center, extent, outerRadius, angle + tipHalf);
            quad[3] = Polar(center, extent, innerRadius, angle + baseHalf);
            FillConvex(dl, ink, quad);
        }

        dl.AddCircleFilled(center, extent * 0.62f, ink, 48);
        dl.AddCircleFilled(center, extent * 0.26f, hole, 24);
    }

    private static void DrawGames(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        dl.AddCircleFilled(At(center, extent, -0.62f, 0.42f), extent * 0.42f, ink, 32);
        dl.AddCircleFilled(At(center, extent, 0.62f, 0.42f), extent * 0.42f, ink, 32);

        var bodyMin = At(center, extent, -0.88f, -0.42f);
        var bodyMax = At(center, extent, 0.88f, 0.30f);
        dl.AddRectFilled(bodyMin, bodyMax, ink, extent * 0.30f);

        var crossHorizontalMin = At(center, extent, -0.70f, -0.12f);
        var crossHorizontalMax = At(center, extent, -0.20f, 0.08f);
        dl.AddRectFilled(crossHorizontalMin, crossHorizontalMax, hole, extent * 0.04f);

        var crossVerticalMin = At(center, extent, -0.55f, -0.27f);
        var crossVerticalMax = At(center, extent, -0.35f, 0.23f);
        dl.AddRectFilled(crossVerticalMin, crossVerticalMax, hole, extent * 0.04f);

        var buttonRadius = extent * 0.11f;
        dl.AddCircleFilled(At(center, extent, 0.48f, -0.24f), buttonRadius, hole, 16);
        dl.AddCircleFilled(At(center, extent, 0.48f, 0.20f), buttonRadius, hole, 16);
        dl.AddCircleFilled(At(center, extent, 0.27f, -0.02f), buttonRadius, hole, 16);
        dl.AddCircleFilled(At(center, extent, 0.69f, -0.02f), buttonRadius, hole, 16);
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
            new(symbol.X, symbol.Y - extent * 0.28f),
            new(symbol.X + extent * 0.24f, symbol.Y),
            new(symbol.X, symbol.Y + extent * 0.28f),
            new(symbol.X - extent * 0.24f, symbol.Y),
        };
        FillConvex(dl, hole, diamond);
    }

    private static void DrawGem(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<Vector2> gem = stackalloc Vector2[5]
        {
            At(center, extent, -0.60f, -0.55f),
            At(center, extent, 0.60f, -0.55f),
            At(center, extent, 0.92f, -0.05f),
            At(center, extent, 0f, 0.92f),
            At(center, extent, -0.92f, -0.05f),
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
            At(center, extent, -0.45f, -0.45f),
            At(center, extent, 0.45f, -0.45f),
            At(center, extent, -0.45f, 0.45f),
            At(center, extent, 0.45f, 0.45f),
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
            dl.AddRectFilled(new Vector2(brick.X - brickWidth, brick.Y - brickHeight), new Vector2(brick.X + brickWidth, brick.Y + brickHeight), ink, rounding);
        }

        dl.AddCircleFilled(At(center, extent, 0.18f, 0.05f), extent * 0.15f, ink);

        var paddle = At(center, extent, 0f, 0.78f);
        var paddleWidth = extent * 0.52f;
        var paddleHeight = extent * 0.12f;
        dl.AddRectFilled(new Vector2(paddle.X - paddleWidth, paddle.Y - paddleHeight), new Vector2(paddle.X + paddleWidth, paddle.Y + paddleHeight), ink, paddleHeight);
    }

    private static void DrawBubbles(ImDrawListPtr dl, Vector2 center, float extent, uint ink, uint hole)
    {
        Span<Vector2> bubbles = stackalloc Vector2[5]
        {
            At(center, extent, -0.46f, -0.42f),
            At(center, extent, 0.46f, -0.42f),
            At(center, extent, 0f, 0.04f),
            At(center, extent, -0.42f, 0.5f),
            At(center, extent, 0.42f, 0.5f),
        };

        var radius = extent * 0.34f;
        for (var bubble = 0; bubble < bubbles.Length; bubble++)
        {
            dl.AddCircleFilled(bubbles[bubble], radius, ink);
            dl.AddCircleFilled(new Vector2(bubbles[bubble].X - radius * 0.32f, bubbles[bubble].Y - radius * 0.32f), radius * 0.3f, hole);
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
            dl.AddRectFilled(new Vector2(min.X + inset, fillTopY), new Vector2(max.X - inset, max.Y - inset), ink, halfWidth - inset, ImDrawFlags.RoundCornersBottom);
        }
    }

    private static Vector2 At(Vector2 center, float extent, float unitX, float unitY)
    {
        return new Vector2(center.X + unitX * extent, center.Y + unitY * extent);
    }

    private static Vector2 Polar(Vector2 center, float extent, float radius, float angle)
    {
        return new Vector2(center.X + MathF.Cos(angle) * radius * extent, center.Y + MathF.Sin(angle) * radius * extent);
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
