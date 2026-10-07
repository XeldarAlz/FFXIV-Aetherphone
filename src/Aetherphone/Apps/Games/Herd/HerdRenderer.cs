using Aetherphone.Apps.Games.Framework;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Herd;

internal static class HerdRenderer
{
    public const float MoogleScale = 0.95f;
    private const float PillarWidth = 0.14f;
    private static readonly Vector4 WaterDeep = new(0.16f, 0.42f, 0.82f, 0.62f);
    private static readonly Vector4 WaterTop = new(0.42f, 0.72f, 0.98f, 0.7f);
    private static readonly Vector4 Foam = new(0.86f, 0.95f, 1f, 0.85f);
    private static readonly Vector4 Wood = new(0.55f, 0.36f, 0.22f, 1f);
    private static readonly Vector4 WoodDark = new(0.36f, 0.22f, 0.13f, 1f);
    private static readonly Vector4 Hollow = new(0.1f, 0.07f, 0.06f, 1f);
    private static readonly Vector4 HutWall = new(0.96f, 0.9f, 0.78f, 1f);
    private static readonly Vector4 HutShade = new(0.82f, 0.72f, 0.58f, 1f);
    private static readonly Vector4 Roof = new(0.84f, 0.3f, 0.32f, 1f);
    private static readonly Vector4 RoofDark = new(0.62f, 0.18f, 0.22f, 1f);
    private static readonly Vector4 Doorway = new(1f, 0.82f, 0.46f, 1f);
    private static readonly Vector4 Stone = new(0.42f, 0.42f, 0.48f, 0.85f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.22f);

    public static void DrawBounds(ImDrawListPtr drawList, in Camera2D camera)
    {
        var top = camera.ToScreen(Vector2.Zero).Y;
        var bottom = camera.ToScreen(new Vector2(0f, HerdBoard.WorldHeight)).Y;
        var color = ImGui.GetColorU32(Stone);
        var leftEdge = camera.ToScreen(Vector2.Zero).X;
        var rightEdge = camera.ToScreen(new Vector2(HerdBoard.WorldWidth, 0f)).X;
        var width = camera.Px(PillarWidth);
        drawList.AddRectFilledMultiColor(new Vector2(leftEdge - width, top), new Vector2(leftEdge, bottom),
            ImGui.GetColorU32(Stone with { W = 0f }), color, color, ImGui.GetColorU32(Stone with { W = 0f }));
        drawList.AddRectFilledMultiColor(new Vector2(rightEdge, top), new Vector2(rightEdge + width, bottom), color,
            ImGui.GetColorU32(Stone with { W = 0f }), ImGui.GetColorU32(Stone with { W = 0f }), color);
    }

    public static void DrawWater(ImDrawListPtr drawList, in Camera2D camera, HerdBoard board, float time)
    {
        var deep = ImGui.GetColorU32(WaterDeep);
        var surface = ImGui.GetColorU32(WaterTop);
        var foam = ImGui.GetColorU32(Foam);
        for (var row = 0; row < HerdLevel.Rows; row++)
        {
            for (var column = 0; column < HerdLevel.Columns; column++)
            {
                if (!board.IsWater(column, row))
                {
                    continue;
                }

                var min = camera.ToScreen(new Vector2(column, row));
                var max = camera.ToScreen(new Vector2(column + 1, row + 1));
                var open = !board.IsWater(column, row - 1);
                if (!open)
                {
                    drawList.AddRectFilled(min, max, deep);
                    continue;
                }

                var wave = camera.Px(0.06f) * MathF.Sin(time * 2.2f + column * 1.3f);
                var surfaceY = min.Y + camera.Px(0.12f) + wave;
                drawList.AddRectFilledMultiColor(new Vector2(min.X, surfaceY), max, surface, surface, deep, deep);
                drawList.AddLine(new Vector2(min.X, surfaceY), new Vector2(max.X, surfaceY), foam,
                    MathF.Max(1f, camera.Px(0.04f)));
                var glint = 0.5f + 0.5f * MathF.Sin(time * 3.1f + column * 2.1f);
                drawList.AddCircleFilled(new Vector2(min.X + (max.X - min.X) * (0.3f + 0.4f * glint), surfaceY + camera.Px(0.12f)),
                    MathF.Max(1f, camera.Px(0.03f)), ImGui.GetColorU32(Foam with { W = 0.4f * glint }), 6);
            }
        }
    }

    public static void DrawDoor(ImDrawListPtr drawList, in Camera2D camera, HerdBoard board, float open, float pulse)
    {
        var spawn = HerdBoard.CellPoint(board.DoorColumn, board.DoorRow);
        var center = camera.ToScreen(spawn + new Vector2(0f, -0.38f));
        var halfWidth = camera.Px(0.62f);
        var halfHeight = camera.Px(0.3f);
        var min = center - new Vector2(halfWidth, halfHeight);
        var max = center + new Vector2(halfWidth, halfHeight);
        var radius = camera.Px(0.16f);
        drawList.AddRectFilled(min + new Vector2(0f, camera.Px(0.06f)), max + new Vector2(0f, camera.Px(0.06f)),
            ImGui.GetColorU32(Shadow), radius);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Wood), radius);
        drawList.AddRect(min, max, ImGui.GetColorU32(WoodDark), radius, ImDrawFlags.RoundCornersAll,
            MathF.Max(1f, camera.Px(0.05f)));
        var hole = new Vector2(halfWidth * 0.78f, halfHeight * 0.55f);
        drawList.AddRectFilled(center - hole + new Vector2(0f, halfHeight * 0.3f), center + hole + new Vector2(0f, halfHeight * 0.3f),
            ImGui.GetColorU32(Hollow), radius * 0.6f);
        var flap = halfWidth * 0.78f * (1f - Math.Clamp(open, 0f, 1f));
        var hinge = center + new Vector2(-hole.X, halfHeight * 0.3f + hole.Y);
        drawList.AddRectFilled(hinge, hinge + new Vector2(flap, camera.Px(0.06f)), ImGui.GetColorU32(WoodDark));
        var flapRight = center + new Vector2(hole.X, halfHeight * 0.3f + hole.Y);
        drawList.AddRectFilled(flapRight - new Vector2(flap, 0f), flapRight + new Vector2(0f, camera.Px(0.06f)),
            ImGui.GetColorU32(WoodDark));
        if (pulse > 0f)
        {
            drawList.AddRectFilled(center - hole + new Vector2(0f, halfHeight * 0.3f), center + hole + new Vector2(0f, halfHeight * 0.3f),
                ImGui.GetColorU32(new Vector4(1f, 0.92f, 0.7f, 0.35f * pulse)), radius * 0.6f);
        }

        var arrowCenter = center + new Vector2(board.DoorDirection * halfWidth * 0.5f, -halfHeight * 0.35f);
        var arrow = camera.Px(0.1f);
        drawList.AddTriangleFilled(arrowCenter + new Vector2(-board.DoorDirection * arrow, -arrow),
            arrowCenter + new Vector2(board.DoorDirection * arrow, 0f), arrowCenter + new Vector2(-board.DoorDirection * arrow, arrow),
            ImGui.GetColorU32(new Vector4(1f, 0.9f, 0.7f, 0.85f)));
    }

    public static void DrawExit(ImDrawListPtr drawList, in Camera2D camera, HerdBoard board, float time, float celebrate,
        Vector4 accent)
    {
        var floor = HerdBoard.CellPoint(board.ExitColumn, board.ExitRow);
        var bounce = 1f + celebrate * 0.12f;
        var width = camera.Px(1.1f) * bounce;
        var wallHeight = camera.Px(0.78f) * bounce;
        var baseCenter = camera.ToScreen(floor);
        var wallMin = new Vector2(baseCenter.X - width * 0.5f, baseCenter.Y - wallHeight);
        var wallMax = new Vector2(baseCenter.X + width * 0.5f, baseCenter.Y);
        ProgressRing.Glow(baseCenter - new Vector2(0f, wallHeight * 0.5f), width * 0.9f, Doorway, 0.18f + 0.3f * celebrate);
        drawList.AddRectFilled(wallMin + new Vector2(camera.Px(0.06f), camera.Px(0.04f)), wallMax + new Vector2(camera.Px(0.06f), 0f),
            ImGui.GetColorU32(Shadow), camera.Px(0.08f));
        drawList.AddRectFilledMultiColor(wallMin, wallMax, ImGui.GetColorU32(HutWall), ImGui.GetColorU32(HutWall),
            ImGui.GetColorU32(HutShade), ImGui.GetColorU32(HutShade));
        var roofLeft = new Vector2(wallMin.X - width * 0.14f, wallMin.Y + camera.Px(0.04f));
        var roofRight = new Vector2(wallMax.X + width * 0.14f, wallMin.Y + camera.Px(0.04f));
        var roofTop = new Vector2(baseCenter.X, wallMin.Y - wallHeight * 0.7f);
        drawList.AddTriangleFilled(roofLeft + new Vector2(0f, camera.Px(0.06f)), roofTop + new Vector2(0f, camera.Px(0.06f)),
            roofRight + new Vector2(0f, camera.Px(0.06f)), ImGui.GetColorU32(RoofDark));
        drawList.AddTriangleFilled(roofLeft, roofTop, roofRight, ImGui.GetColorU32(Roof));
        var doorHalf = width * 0.2f;
        var doorTop = baseCenter.Y - wallHeight * 0.72f;
        var flicker = 0.85f + 0.15f * MathF.Sin(time * 6f);
        drawList.AddRectFilled(new Vector2(baseCenter.X - doorHalf, doorTop + doorHalf), new Vector2(baseCenter.X + doorHalf, baseCenter.Y),
            ImGui.GetColorU32(Doorway with { W = flicker }));
        drawList.AddCircleFilled(new Vector2(baseCenter.X, doorTop + doorHalf), doorHalf, ImGui.GetColorU32(Doorway with { W = flicker }), 16);
        var poleBase = roofTop;
        var poleTop = poleBase - new Vector2(0f, camera.Px(0.42f));
        drawList.AddLine(poleBase, poleTop, ImGui.GetColorU32(WoodDark), MathF.Max(1f, camera.Px(0.04f)));
        var wave = MathF.Sin(time * 4f) * camera.Px(0.05f);
        var flagTip = poleTop + new Vector2(camera.Px(0.36f), camera.Px(0.08f) + wave);
        drawList.AddTriangleFilled(poleTop, flagTip, poleTop + new Vector2(0f, camera.Px(0.18f)), ImGui.GetColorU32(accent));
        drawList.AddCircleFilled(poleTop, camera.Px(0.07f), ImGui.GetColorU32(HerdArt.PomPom), 10);
    }

    public static void DrawMoogles(ImDrawListPtr drawList, in Camera2D camera, HerdBoard board, float alpha, float time)
    {
        var unit = camera.Px(1f) * MoogleScale;
        for (var index = 0; index < board.MoogleCount; index++)
        {
            ref readonly var moogle = ref board.Moogle(index);
            if (moogle.Gone)
            {
                continue;
            }

            var feet = camera.ToScreen(board.Feet(index, alpha));
            var fade = moogle.Action switch
            {
                HerdAction.Exiting => 1f - Math.Clamp(moogle.ActionTicks / (float)HerdBoard.ExitTicks, 0f, 1f),
                HerdAction.Splatting => 1f - Math.Clamp(moogle.ActionTicks / (float)HerdBoard.SplatTicks, 0f, 1f),
                HerdAction.Drowning => 1f - Math.Clamp(moogle.ActionTicks / (float)HerdBoard.DrownTicks, 0f, 1f),
                _ => 1f,
            };
            var pose = HerdArt.PoseFor(moogle, time);
            var tint = PomPomFor(moogle.Variant);
            if (moogle.Action == HerdAction.Popping && MathF.Sin(time * 18f) > 0f)
            {
                tint = new Vector4(1f, 0.95f, 0.6f, 1f);
            }

            HerdArt.DrawMoogle(drawList, feet, unit, moogle.Direction, pose, fade, tint);
            if (moogle.Climber || moogle.Floater)
            {
                DrawBadges(drawList, feet, unit, moogle, fade);
            }
        }
    }

    public static Vector4 PomPomFor(byte variant)
    {
        var shift = (variant % 5) * 0.035f;
        return new Vector4(HerdArt.PomPom.X, HerdArt.PomPom.Y + shift, HerdArt.PomPom.Z + shift * 1.4f, 1f);
    }

    private static void DrawBadges(ImDrawListPtr drawList, Vector2 feet, float unit, in HerdMoogle moogle, float alpha)
    {
        var center = feet + new Vector2(-unit * 0.34f * moogle.Direction, -unit * 0.95f);
        var radius = MathF.Max(2f, unit * 0.08f);
        if (moogle.Climber)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.44f, 0.86f, 0.52f, alpha)), 10);
            center += new Vector2(0f, radius * 2.4f);
        }

        if (moogle.Floater)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.5f, 0.78f, 1f, alpha)), 10);
        }
    }

    public static void DrawTarget(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float time)
    {
        var pulse = 1f + 0.08f * MathF.Sin(time * 8f);
        drawList.AddCircle(center, radius * pulse, ImGui.GetColorU32(color with { W = 0.9f }), 24, MathF.Max(1.5f, radius * 0.1f));
        drawList.AddCircleFilled(center, radius * pulse, ImGui.GetColorU32(color with { W = 0.12f }), 24);
    }
}
