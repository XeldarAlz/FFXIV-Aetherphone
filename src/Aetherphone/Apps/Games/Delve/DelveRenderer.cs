using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Delve;

internal readonly struct DelveWindow
{
    public readonly int MinColumn;
    public readonly int MaxColumn;
    public readonly int MinRow;
    public readonly int MaxRow;

    public DelveWindow(int minColumn, int maxColumn, int minRow, int maxRow)
    {
        MinColumn = minColumn;
        MaxColumn = maxColumn;
        MinRow = minRow;
        MaxRow = maxRow;
    }
}

internal static class DelveRenderer
{
    public static readonly Vector4 GemCore = new(0.36f, 0.92f, 0.86f, 1f);
    public static readonly Vector4 GemLight = new(0.82f, 1f, 0.97f, 1f);
    public static readonly Vector4 DirtSpeck = new(0.58f, 0.43f, 0.28f, 1f);
    public static readonly Vector4 Ember = new(1f, 0.58f, 0.22f, 1f);
    public static readonly Vector4 Flame = new(1f, 0.92f, 0.62f, 1f);
    public static readonly Vector4 Rock = new(0.58f, 0.55f, 0.52f, 1f);
    private const int EllipseSegments = 20;
    private static readonly Vector4 Tunnel = new(0.065f, 0.05f, 0.045f, 0.94f);
    private static readonly Vector4 Dirt = new(0.42f, 0.29f, 0.18f, 1f);
    private static readonly Vector4 DirtDark = new(0.30f, 0.20f, 0.12f, 1f);
    private static readonly Vector4 DirtEdge = new(0.66f, 0.50f, 0.32f, 1f);
    private static readonly Vector4 Steel = new(0.34f, 0.37f, 0.44f, 1f);
    private static readonly Vector4 SteelLight = new(0.52f, 0.56f, 0.64f, 1f);
    private static readonly Vector4 SteelDark = new(0.20f, 0.22f, 0.27f, 1f);
    private static readonly Vector4 RockDark = new(0.34f, 0.32f, 0.30f, 1f);
    private static readonly Vector4 GemDark = new(0.10f, 0.52f, 0.54f, 1f);
    private static readonly Vector4 BatBody = new(0.30f, 0.18f, 0.42f, 1f);
    private static readonly Vector4 BatWing = new(0.46f, 0.28f, 0.62f, 1f);
    private static readonly Vector4 BatEye = new(1f, 0.82f, 0.30f, 1f);
    private static readonly Vector4 Slime = new(0.44f, 0.86f, 0.36f, 1f);
    private static readonly Vector4 SlimeDark = new(0.22f, 0.58f, 0.22f, 1f);
    private static readonly Vector4 Fluff = new(0.99f, 0.97f, 0.93f, 1f);
    private static readonly Vector4 FluffShade = new(0.84f, 0.81f, 0.78f, 1f);
    private static readonly Vector4 Helmet = new(0.98f, 0.74f, 0.22f, 1f);
    private static readonly Vector4 HelmetDark = new(0.78f, 0.52f, 0.10f, 1f);
    private static readonly Vector4 Lamp = new(1f, 0.96f, 0.72f, 1f);
    private static readonly Vector4 Pompom = new(0.94f, 0.27f, 0.36f, 1f);
    private static readonly Vector4 Wing = new(0.58f, 0.44f, 0.82f, 1f);
    private static readonly Vector4 Ink = new(0.12f, 0.10f, 0.12f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4 ExitFrame = new(0.30f, 0.30f, 0.36f, 1f);
    private static readonly Vector4 ExitDark = new(0.08f, 0.08f, 0.12f, 1f);

    public static void DrawCave(ImDrawListPtr drawList, DelveBoard board, in Camera2D camera, in DelveWindow window,
        float time, Vector4 accent, float exitFlash)
    {
        var levelMin = camera.ToScreen(Vector2.Zero);
        var levelMax = camera.ToScreen(new Vector2(board.Columns, board.Rows));
        drawList.AddRectFilled(levelMin, levelMax, ImGui.GetColorU32(Tunnel));
        var cell = camera.Px(1f);
        for (var row = window.MinRow; row <= window.MaxRow; row++)
        {
            for (var column = window.MinColumn; column <= window.MaxColumn; column++)
            {
                var tile = board.TileAt(column, row);
                var min = camera.ToScreen(new Vector2(column, row));
                switch (tile)
                {
                    case DelveTile.Dirt:
                        DrawDirt(drawList, board, column, row, min, cell);
                        break;
                    case DelveTile.Wall:
                        DrawWall(drawList, min, cell);
                        break;
                    case DelveTile.Exit:
                        DrawExit(drawList, min + new Vector2(cell, cell) * 0.5f, cell, board.ExitOpen, time, accent,
                            exitFlash);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    public static void DrawObjects(ImDrawListPtr drawList, DelveBoard board, in Camera2D camera,
        in DelveWindow window, float time, float squash, float lampGlow)
    {
        var cell = camera.Px(1f);
        var alpha = board.Alpha;
        for (var row = window.MinRow; row <= window.MaxRow; row++)
        {
            for (var column = window.MinColumn; column <= window.MaxColumn; column++)
            {
                var index = board.Cell(column, row);
                var tile = board.TileAt(index);
                if (tile is DelveTile.Empty or DelveTile.Dirt or DelveTile.Wall or DelveTile.Exit)
                {
                    continue;
                }

                var position = new Vector2(column + 0.5f, row + 0.5f);
                var arrived = board.ArrivedFrom(index);
                if (arrived >= 0)
                {
                    position -= DelveBoard.Step(arrived) * (1f - alpha);
                }

                var center = camera.ToScreen(position);
                var seed = (column * 73856093) ^ (row * 19349663);
                switch (tile)
                {
                    case DelveTile.Boulder:
                        DrawBoulder(drawList, center, cell, seed);
                        break;
                    case DelveTile.Gem:
                        DrawGem(drawList, center, cell, time, seed);
                        break;
                    case DelveTile.Bat:
                        DrawBat(drawList, center, cell, time, seed);
                        break;
                    case DelveTile.Slime:
                        DrawSlime(drawList, center, cell, time, seed);
                        break;
                    case DelveTile.Blast:
                    case DelveTile.GemBlast:
                        DrawBlast(drawList, center, cell, board.BlastLeft(index), tile == DelveTile.GemBlast);
                        break;
                    case DelveTile.Player:
                        DrawMiner(drawList, center, cell, board.Facing, squash, time, lampGlow);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    public static void DrawMiner(ImDrawListPtr drawList, Vector2 center, float cell, int facing, float squash,
        float time, float lampGlow)
    {
        var horizontal = facing is 1 or 3;
        var stretchX = horizontal ? 1f - 0.18f * squash : 1f + 0.12f * squash;
        var stretchY = horizontal ? 1f + 0.10f * squash : 1f - 0.18f * squash;
        var bob = MathF.Sin(time * 5f) * cell * 0.02f;
        var body = new Vector2(cell * 0.34f * stretchX, cell * 0.30f * stretchY);
        var feet = center + new Vector2(0f, cell * 0.42f);
        var bodyCenter = feet - new Vector2(0f, body.Y * 1.05f) + new Vector2(0f, bob);
        ProgressRing.Glow(bodyCenter, cell * 1.8f, Lamp, 0.10f + 0.08f * lampGlow);
        FillEllipse(drawList, feet, new Vector2(cell * 0.28f, cell * 0.07f), ImGui.GetColorU32(Shadow));
        var side = facing switch
        {
            3 => -1f,
            1 => 1f,
            _ => 0f,
        };
        var flap = 0.5f + 0.5f * MathF.Sin(time * 9f);
        DrawWing(drawList, bodyCenter + new Vector2(-body.X * 0.85f, -body.Y * 0.2f), -1f, cell, flap);
        DrawWing(drawList, bodyCenter + new Vector2(body.X * 0.85f, -body.Y * 0.2f), 1f, cell, flap);
        FillEllipse(drawList, bodyCenter + new Vector2(0f, body.Y * 0.08f), body * 1.05f, ImGui.GetColorU32(FluffShade));
        FillEllipse(drawList, bodyCenter, body, ImGui.GetColorU32(Fluff));
        var helmetCenter = bodyCenter - new Vector2(0f, body.Y * 0.45f);
        var helmetRadius = body.X * 0.95f;
        drawList.PathClear();
        drawList.PathArcTo(helmetCenter, helmetRadius, MathF.PI, MathF.Tau, 16);
        drawList.PathFillConvex(ImGui.GetColorU32(Helmet));
        drawList.AddLine(helmetCenter - new Vector2(helmetRadius * 1.08f, 0f), helmetCenter + new Vector2(helmetRadius * 1.08f, 0f),
            ImGui.GetColorU32(HelmetDark), MathF.Max(1f, cell * 0.05f));
        var lamp = helmetCenter + new Vector2(side * helmetRadius * 0.55f, -helmetRadius * 0.5f);
        ProgressRing.Glow(lamp, cell * 0.35f, Lamp, 0.6f + 0.3f * lampGlow);
        drawList.AddCircleFilled(lamp, cell * 0.07f, ImGui.GetColorU32(Lamp), 10);
        var stemBase = helmetCenter - new Vector2(-side * helmetRadius * 0.2f, helmetRadius * 0.95f);
        var pompom = stemBase + new Vector2(MathF.Sin(time * 3f) * cell * 0.04f - side * cell * 0.04f, -cell * 0.16f);
        drawList.AddLine(stemBase, pompom, ImGui.GetColorU32(Ink with { W = 0.8f }), MathF.Max(1f, cell * 0.025f));
        drawList.AddCircleFilled(pompom, cell * 0.07f, ImGui.GetColorU32(Pompom), 12);
        if (facing == 0)
        {
            return;
        }

        var faceCenter = bodyCenter + new Vector2(side * body.X * 0.3f, body.Y * 0.2f);
        var eyeSpread = body.X * (side == 0f ? 0.36f : 0.28f);
        var eyeInk = ImGui.GetColorU32(Ink);
        var eyeWidth = cell * 0.07f;
        var thickness = MathF.Max(1f, cell * 0.03f);
        drawList.AddLine(faceCenter + new Vector2(-eyeSpread - eyeWidth * 0.5f, 0f),
            faceCenter + new Vector2(-eyeSpread + eyeWidth * 0.5f, 0f), eyeInk, thickness);
        drawList.AddLine(faceCenter + new Vector2(eyeSpread - eyeWidth * 0.5f, 0f),
            faceCenter + new Vector2(eyeSpread + eyeWidth * 0.5f, 0f), eyeInk, thickness);
        drawList.AddCircleFilled(faceCenter + new Vector2(0f, body.Y * 0.3f), cell * 0.035f,
            ImGui.GetColorU32(Pompom with { W = 0.75f }), 8);
    }

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 radii, uint color)
    {
        drawList.PathClear();
        for (var segment = 0; segment < EllipseSegments; segment++)
        {
            var angle = segment * MathF.Tau / EllipseSegments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radii.X, MathF.Sin(angle) * radii.Y));
        }

        drawList.PathFillConvex(color);
    }

    private static void DrawDirt(ImDrawListPtr drawList, DelveBoard board, int column, int row, Vector2 min,
        float cell)
    {
        var hash = (uint)((column * 92837111) ^ (row * 689287499));
        var shade = ((hash >> 3) & 15) / 15f;
        var fill = Vector4.Lerp(DirtDark, Dirt, 0.6f + 0.4f * shade);
        var max = min + new Vector2(cell, cell);
        drawList.AddRectFilled(min, max + new Vector2(0.5f, 0.5f), ImGui.GetColorU32(fill));
        var speck = ImGui.GetColorU32(DirtSpeck with { W = 0.5f });
        var dark = ImGui.GetColorU32(DirtDark with { W = 0.8f });
        for (var index = 0; index < 3; index++)
        {
            var bits = hash >> (index * 7);
            var offset = new Vector2((bits & 7) / 8f + 0.06f, ((bits >> 3) & 7) / 8f + 0.06f) * cell;
            drawList.AddCircleFilled(min + offset, cell * (0.035f + 0.02f * (index & 1)), index == 1 ? dark : speck, 6);
        }

        var edge = MathF.Max(1f, cell * 0.08f);
        if (board.TileAt(column, row - 1) is not DelveTile.Dirt and not DelveTile.Wall)
        {
            drawList.AddRectFilled(min, new Vector2(max.X, min.Y + edge), ImGui.GetColorU32(DirtEdge));
        }

        if (board.TileAt(column, row + 1) is not DelveTile.Dirt and not DelveTile.Wall)
        {
            drawList.AddRectFilled(new Vector2(min.X, max.Y - edge), max, ImGui.GetColorU32(DirtDark));
        }
    }

    private static void DrawWall(ImDrawListPtr drawList, Vector2 min, float cell)
    {
        var max = min + new Vector2(cell, cell);
        var inset = cell * 0.04f;
        var radius = cell * 0.14f;
        Squircle.FillVerticalGradient(drawList, min + new Vector2(inset, inset), max - new Vector2(inset, inset), radius,
            ImGui.GetColorU32(SteelLight), ImGui.GetColorU32(Steel));
        Squircle.Stroke(drawList, min + new Vector2(inset, inset), max - new Vector2(inset, inset), radius,
            ImGui.GetColorU32(SteelDark), MathF.Max(1f, cell * 0.04f));
        var rivet = ImGui.GetColorU32(SteelDark);
        var reach = cell * 0.24f;
        var center = (min + max) * 0.5f;
        drawList.AddCircleFilled(center + new Vector2(-reach, -reach), cell * 0.045f, rivet, 6);
        drawList.AddCircleFilled(center + new Vector2(reach, reach), cell * 0.045f, rivet, 6);
    }

    private static void DrawExit(ImDrawListPtr drawList, Vector2 center, float cell, bool open, float time,
        Vector4 accent, float flash)
    {
        var half = new Vector2(cell * 0.44f, cell * 0.46f);
        Squircle.Fill(drawList, center - half, center + half, cell * 0.2f, ImGui.GetColorU32(ExitFrame));
        var inner = half * 0.74f;
        Squircle.Fill(drawList, center - inner, center + inner, cell * 0.14f, ImGui.GetColorU32(ExitDark));
        if (!open)
        {
            drawList.AddLine(center - new Vector2(inner.X * 0.6f, 0f), center + new Vector2(inner.X * 0.6f, 0f),
                ImGui.GetColorU32(ExitFrame), MathF.Max(1f, cell * 0.06f));
            drawList.AddCircleFilled(center, cell * 0.08f, ImGui.GetColorU32(accent with { W = 0.35f }), 10);
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(time * 4f);
        ProgressRing.Glow(center, cell * (1.1f + 0.6f * flash), accent, 0.55f + 0.3f * pulse + flash);
        var spin = time * 2.2f;
        var ring = cell * 0.26f;
        for (var spoke = 0; spoke < 3; spoke++)
        {
            var angle = spin + spoke * MathF.Tau / 3f;
            drawList.PathClear();
            drawList.PathArcTo(center, ring, angle, angle + 1.4f, 10);
            drawList.PathStroke(ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f)), ImDrawFlags.None,
                MathF.Max(1.5f, cell * 0.07f));
        }

        drawList.AddCircleFilled(center, cell * (0.1f + 0.03f * pulse), ImGui.GetColorU32(White with { W = 0.9f }), 12);
    }

    private static void DrawBoulder(ImDrawListPtr drawList, Vector2 center, float cell, int seed)
    {
        var radius = cell * 0.45f;
        drawList.AddCircleFilled(center + new Vector2(0f, cell * 0.05f), radius, ImGui.GetColorU32(Shadow), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(RockDark), 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.08f, radius * 0.1f), radius * 0.86f,
            ImGui.GetColorU32(Rock), 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.35f, radius * 0.38f), radius * 0.26f,
            ImGui.GetColorU32(White with { W = 0.28f }), 12);
        var crack = ImGui.GetColorU32(RockDark with { W = 0.85f });
        var turn = (seed & 3) * 0.5f;
        var start = center + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * radius * 0.2f;
        var bend = start + new Vector2(MathF.Cos(turn + 0.9f), MathF.Sin(turn + 0.9f)) * radius * 0.35f;
        var finish = bend + new Vector2(MathF.Cos(turn + 0.2f), MathF.Sin(turn + 0.2f)) * radius * 0.3f;
        var thickness = MathF.Max(1f, cell * 0.035f);
        drawList.AddLine(start, bend, crack, thickness);
        drawList.AddLine(bend, finish, crack, thickness);
    }

    private static void DrawGem(ImDrawListPtr drawList, Vector2 center, float cell, float time, int seed)
    {
        var half = cell * 0.30f;
        var tall = cell * 0.40f;
        ProgressRing.Glow(center, cell * 0.55f, GemCore, 0.22f);
        var top = center + new Vector2(0f, -tall);
        var bottom = center + new Vector2(0f, tall);
        var left = center + new Vector2(-half, -tall * 0.15f);
        var right = center + new Vector2(half, -tall * 0.15f);
        var waist = center + new Vector2(0f, -tall * 0.15f);
        drawList.AddTriangleFilled(top, right, left, ImGui.GetColorU32(GemLight));
        drawList.AddTriangleFilled(left, waist, bottom, ImGui.GetColorU32(GemCore));
        drawList.AddTriangleFilled(waist, right, bottom, ImGui.GetColorU32(GemDark));
        drawList.AddTriangleFilled(top, waist, left, ImGui.GetColorU32(GemCore with { W = 0.6f }));
        var phase = (time * 0.7f + (seed & 255) / 255f) % 1f;
        if (phase > 0.16f)
        {
            return;
        }

        var twinkle = MathF.Sin(phase / 0.16f * MathF.PI);
        var arm = cell * 0.28f * twinkle;
        var spark = top + new Vector2(half * 0.3f, tall * 0.45f);
        var sparkColor = ImGui.GetColorU32(White with { W = 0.95f * twinkle });
        drawList.AddLine(spark - new Vector2(arm, 0f), spark + new Vector2(arm, 0f), sparkColor, MathF.Max(1f, cell * 0.04f));
        drawList.AddLine(spark - new Vector2(0f, arm), spark + new Vector2(0f, arm), sparkColor, MathF.Max(1f, cell * 0.04f));
    }

    private static void DrawBat(ImDrawListPtr drawList, Vector2 center, float cell, float time, int seed)
    {
        var flap = MathF.Sin(time * 14f + (seed & 15));
        var body = cell * 0.2f;
        var span = cell * 0.44f;
        var lift = flap * cell * 0.16f;
        var wing = ImGui.GetColorU32(BatWing);
        drawList.AddTriangleFilled(center + new Vector2(-body * 0.4f, -body * 0.2f),
            center + new Vector2(-span, -lift - cell * 0.08f), center + new Vector2(-span * 0.55f, cell * 0.12f), wing);
        drawList.AddTriangleFilled(center + new Vector2(body * 0.4f, -body * 0.2f),
            center + new Vector2(span * 0.55f, cell * 0.12f), center + new Vector2(span, -lift - cell * 0.08f), wing);
        drawList.AddCircleFilled(center, body, ImGui.GetColorU32(BatBody), 14);
        drawList.AddTriangleFilled(center + new Vector2(-body * 0.8f, -body * 0.4f),
            center + new Vector2(-body * 0.5f, -body * 1.5f), center + new Vector2(-body * 0.15f, -body * 0.7f),
            ImGui.GetColorU32(BatBody));
        drawList.AddTriangleFilled(center + new Vector2(body * 0.15f, -body * 0.7f),
            center + new Vector2(body * 0.5f, -body * 1.5f), center + new Vector2(body * 0.8f, -body * 0.4f),
            ImGui.GetColorU32(BatBody));
        ProgressRing.Glow(center, body * 1.3f, BatEye, 0.25f);
        drawList.AddCircleFilled(center + new Vector2(-body * 0.35f, -body * 0.1f), body * 0.2f, ImGui.GetColorU32(BatEye), 8);
        drawList.AddCircleFilled(center + new Vector2(body * 0.35f, -body * 0.1f), body * 0.2f, ImGui.GetColorU32(BatEye), 8);
    }

    private static void DrawSlime(ImDrawListPtr drawList, Vector2 center, float cell, float time, int seed)
    {
        var wobble = MathF.Sin(time * 6f + (seed & 15));
        var radii = new Vector2(cell * (0.38f + 0.04f * wobble), cell * (0.30f - 0.04f * wobble));
        var ground = center + new Vector2(0f, cell * 0.42f);
        var blob = ground - new Vector2(0f, radii.Y);
        FillEllipse(drawList, ground, new Vector2(radii.X, cell * 0.06f), ImGui.GetColorU32(Shadow));
        FillEllipse(drawList, blob, radii, ImGui.GetColorU32(SlimeDark));
        FillEllipse(drawList, blob - new Vector2(0f, radii.Y * 0.12f), radii * 0.88f, ImGui.GetColorU32(Slime));
        FillEllipse(drawList, blob - new Vector2(radii.X * 0.4f, radii.Y * 0.45f), radii * 0.22f,
            ImGui.GetColorU32(White with { W = 0.55f }));
        var eye = ImGui.GetColorU32(Ink);
        drawList.AddCircleFilled(blob + new Vector2(-radii.X * 0.3f, -radii.Y * 0.05f), cell * 0.05f, eye, 8);
        drawList.AddCircleFilled(blob + new Vector2(radii.X * 0.3f, -radii.Y * 0.05f), cell * 0.05f, eye, 8);
    }

    private static void DrawBlast(ImDrawListPtr drawList, Vector2 center, float cell, int ticksLeft, bool gems)
    {
        var life = ticksLeft / (float)DelveBoard.BlastTicks;
        var outer = gems ? GemCore : Ember;
        var inner = gems ? GemLight : Flame;
        ProgressRing.Glow(center, cell * (0.9f + 0.4f * (1f - life)), outer, 0.7f * life + 0.2f);
        drawList.AddCircleFilled(center, cell * 0.42f * (0.6f + 0.4f * life), ImGui.GetColorU32(outer with { W = 0.85f }), 16);
        drawList.AddCircleFilled(center, cell * 0.22f * life, ImGui.GetColorU32(inner), 12);
    }

    private static void DrawWing(ImDrawListPtr drawList, Vector2 root, float side, float cell, float flap)
    {
        var span = cell * (0.14f + 0.04f * flap);
        var tip = root + new Vector2(side * span, -cell * (0.1f + 0.05f * flap));
        var lower = root + new Vector2(side * span * 0.7f, cell * 0.06f);
        drawList.AddTriangleFilled(root, tip, lower, ImGui.GetColorU32(Wing));
    }
}
