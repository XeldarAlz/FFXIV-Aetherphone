using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Pinball;

internal enum LampState : byte
{
    Off,
    On,
    Blink,
}

internal static class PinballRenderer
{
    public const float PlungerTravel = 0.45f;
    private const float KickTravel = 0.3f;
    public static readonly Vector4 Magenta = new(1f, 0.36f, 0.74f, 1f);
    public static readonly Vector4 Cyan = new(0.36f, 0.9f, 1f, 1f);
    public static readonly Vector4 Violet = new(0.7f, 0.52f, 1f, 1f);
    public static readonly Vector4 Ember = new(1f, 0.42f, 0.3f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private const float WallCore = 0.035f;
    private const float WallHalo = 0.14f;
    private const float RailCore = 0.045f;
    private const float RailHalo = 0.16f;
    private const float BlinkHertz = 4f;
    private const float FastBlinkHertz = 8f;
    private const float FlipperPivotRadius = 0.11f;
    private const float FlipperTipRadius = 0.06f;
    private const float BumperCapRadius = 0.24f;
    private const float LampRadius = 0.09f;
    private const float ApronTop = 11.62f;
    private const int RayCount = 16;
    private const int EllipseSegments = 32;
    private static readonly Vector2 ShadowOffset = new(0.05f, 0.08f);
    private static readonly Vector2 RampShadowOffset = new(0.14f, 0.2f);
    private static readonly Vector2 EmblemCenter = new(2.7f, 7.25f);
    private static readonly Vector2 ShootAgainLamp = new(2.7f, 11.32f);
    private static readonly Vector2 JackpotLamp = new(4.6f, 6.5f);
    private static readonly Vector2 SaucerLamp = new(1.68f, 4.2f);
    private static readonly Vector2 SpinnerLamp = new(0.3f, 5.55f);
    private static readonly Vector2 OrbitLamp = new(5.12f, 5.2f);
    private static readonly Vector2[] LockLamps = { new(1.15f, 3.55f), new(1.45f, 3.62f), new(1.75f, 3.55f) };
    private static readonly float[] MultiplierAngles = { -2.6f, -1.92f, -1.22f, -0.54f };
    private static readonly Vector4 Playfield = new(0.03f, 0.028f, 0.06f, 0.95f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.42f);
    private static readonly Vector4 Steel = new(0.8f, 0.82f, 0.88f, 1f);
    private static readonly Vector4 Hole = new(0.01f, 0.01f, 0.02f, 1f);
    private static readonly Vector4 Apron = new(0.06f, 0.05f, 0.09f, 1f);

    public static Rect TableRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(PinballTable.Width, PinballTable.CabinetBottom)));

    public static Vector4 BankColor(int bank) => bank == 0 ? Magenta : Cyan;

    public static LampState Lamp(bool on, bool blink) => blink ? LampState.Blink : on ? LampState.On : LampState.Off;

    public static void DrawTable(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, Vector4 accent,
        float time, float plungerKick)
    {
        var gold = GamePalette.Lighten(accent, 0.3f);
        DrawPlayfield(drawList, in camera, accent, time);
        DrawLamps(drawList, in camera, board, gold, time);
        DrawWalls(drawList, in camera, gold);
        DrawSlings(drawList, in camera, board);
        DrawPosts(drawList, in camera, gold);
        DrawTargets(drawList, in camera, board);
        DrawSaucer(drawList, in camera, board, time);
        DrawSpinner(drawList, in camera, board, gold);
        DrawBumpers(drawList, in camera, board, gold);
        DrawPlunger(drawList, in camera, board, gold, plungerKick);
        DrawApron(drawList, in camera, board, gold, time);
        DrawFlippers(drawList, in camera, board, gold);
    }

    public static void DrawBall(ImDrawListPtr drawList, in Camera2D camera, Vector2 position, Vector4 accent,
        bool raised)
    {
        var lift = raised ? 1.08f : 1f;
        var radius = camera.Px(PinballTable.BallRadius * lift);
        var center = camera.ToScreen(position);
        var shadow = camera.ToScreen(position + (raised ? RampShadowOffset : ShadowOffset) * 0.6f);
        drawList.AddCircleFilled(shadow, radius, ImGui.GetColorU32(Shadow), 16);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(Steel, 0.25f)), 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.08f), radius * 0.86f, ImGui.GetColorU32(Steel), 20);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.35f), radius * 0.32f,
            ImGui.GetColorU32(accent with { W = 0.35f }), 12);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.32f), radius * 0.3f, ImGui.GetColorU32(White), 12);
    }

    public static void DrawRamp(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, float time)
    {
        var outer = PinballTable.RampOuterRail;
        var inner = PinballTable.RampInnerRail;
        var shadow = ImGui.GetColorU32(Shadow with { W = 0.3f });
        for (var index = 0; index < outer.Length - 1; index++)
        {
            drawList.AddQuadFilled(camera.ToScreen(outer[index] + RampShadowOffset),
                camera.ToScreen(outer[index + 1] + RampShadowOffset), camera.ToScreen(inner[index + 1] + RampShadowOffset),
                camera.ToScreen(inner[index] + RampShadowOffset), shadow);
        }

        var flash = board.RampFlash;
        var floor = ImGui.GetColorU32(Cyan with { W = 0.1f + 0.25f * flash });
        for (var index = 0; index < outer.Length - 1; index++)
        {
            drawList.AddQuadFilled(camera.ToScreen(outer[index]), camera.ToScreen(outer[index + 1]),
                camera.ToScreen(inner[index + 1]), camera.ToScreen(inner[index]), floor);
        }

        DrawChevrons(drawList, in camera, board, time);
        Stroke(drawList, in camera, outer, Cyan, RailCore, RailHalo);
        Stroke(drawList, in camera, inner, Cyan, RailCore, RailHalo);
        var lip = ImGui.GetColorU32(GamePalette.Lighten(Cyan, 0.4f));
        drawList.AddLine(camera.ToScreen(outer[0]), camera.ToScreen(inner[0]), lip, MathF.Max(1f, camera.Px(0.05f)));
    }

    private static void DrawChevrons(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, float time)
    {
        var path = PinballTable.RampPath;
        var hot = board.JackpotLit || board.RampFlash > 0f;
        var march = (int)(time * (hot ? 10f : 3f));
        for (var index = 1; index < path.Length - 2; index += 2)
        {
            var direction = Vector2.Normalize(path[index + 1] - path[index]);
            var side = new Vector2(-direction.Y, direction.X);
            var tip = path[index] + direction * 0.09f;
            var lit = ((index / 2 + march) % 4) == 0;
            var color = (board.JackpotLit ? GamePalette.Lighten(Ember, 0.2f) : Cyan) with
            {
                W = lit ? (hot ? 0.95f : 0.5f) : 0.16f,
            };
            var thickness = MathF.Max(1f, camera.Px(0.04f));
            drawList.AddLine(camera.ToScreen(tip - direction * 0.12f + side * 0.12f), camera.ToScreen(tip),
                ImGui.GetColorU32(color), thickness);
            drawList.AddLine(camera.ToScreen(tip - direction * 0.12f - side * 0.12f), camera.ToScreen(tip),
                ImGui.GetColorU32(color), thickness);
        }
    }

    private static void DrawPlayfield(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent, float time)
    {
        var cabinet = PinballTable.Cabinet;
        drawList.PathClear();
        for (var index = 0; index < cabinet.Length; index++)
        {
            drawList.PathLineTo(camera.ToScreen(cabinet[index]));
        }

        drawList.PathFillConvex(ImGui.GetColorU32(Playfield));
        ProgressRing.Glow(camera.ToScreen(new Vector2(PinballTable.Width * 0.5f, 3.4f)), camera.Px(2.6f), accent, 0.5f);
        ProgressRing.Glow(camera.ToScreen(EmblemCenter), camera.Px(1.5f), Magenta, 0.35f);
        var center = camera.ToScreen(EmblemCenter);
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 1.6f);
        var faint = ImGui.GetColorU32(accent with { W = 0.1f + 0.05f * pulse });
        var line = MathF.Max(1f, camera.Px(0.025f));
        drawList.AddCircle(center, camera.Px(0.82f), faint, 48, line);
        drawList.AddCircle(center, camera.Px(0.62f), ImGui.GetColorU32(accent with { W = 0.06f }), 48, line);
        for (var ray = 0; ray < RayCount; ray++)
        {
            var angle = ray * MathF.Tau / RayCount + time * 0.05f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + direction * camera.Px(0.88f), center + direction * camera.Px(1.02f), faint, line);
        }

        var saucer = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.2f) with { W = 0.22f });
        var ellipseCenter = center + new Vector2(0f, camera.Px(0.06f));
        var radii = new Vector2(camera.Px(0.46f), camera.Px(0.13f));
        Shapes.StrokeEllipse(drawList, ellipseCenter, radii, saucer, line * 1.4f, EllipseSegments);
        drawList.PathClear();
        drawList.PathArcTo(center + new Vector2(0f, camera.Px(0.02f)), camera.Px(0.24f), MathF.PI, MathF.Tau, 16);
        drawList.PathStroke(saucer, ImDrawFlags.None, line * 1.4f);
    }

    private static void DrawLamps(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, Vector4 gold,
        float time)
    {
        var skillShowing = board.BallInLane || board.SkillArmed;
        for (var lane = 0; lane < PinballTable.TopLaneCount; lane++)
        {
            var position = new Vector2(PinballTable.TopLanes[lane].X, 2.12f);
            var blink = skillShowing && lane == board.SkillLane;
            DrawCircleLamp(drawList, in camera, position, LampRadius, gold, Lamp(board.LaneLit(lane), blink), time,
                board.LaneFlash(lane));
        }

        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            var bank = target / PinballTable.BankSize;
            var position = PinballTable.Targets[target] + new Vector2(bank == 0 ? 0.42f : -0.42f, 0f);
            var direction = bank == 0 ? -Vector2.UnitX : Vector2.UnitX;
            DrawArrowLamp(drawList, in camera, position, direction, 0.11f, BankColor(bank),
                Lamp(board.TargetDown(target), false), time, 0f);
        }

        DrawArrowLamp(drawList, in camera, JackpotLamp, -Vector2.UnitY, 0.24f, Ember, Lamp(false, board.JackpotLit),
            time, board.RampFlash);
        var saucerDirection = Vector2.Normalize(PinballTable.Saucer - SaucerLamp);
        DrawArrowLamp(drawList, in camera, SaucerLamp, saucerDirection, 0.16f, Violet, Lamp(false, board.LockLit), time,
            board.SaucerFlash);
        for (var lockIndex = 0; lockIndex < PinballBoard.LocksForMultiball; lockIndex++)
        {
            var state = board.MultiballActive
                ? LampState.Blink
                : Lamp(lockIndex < board.Locks, board.LockLit && lockIndex == board.Locks);
            DrawCircleLamp(drawList, in camera, LockLamps[lockIndex], 0.075f, Violet, state, time, 0f);
        }

        var spinning = MathF.Abs(MathF.Sin(board.SpinnerAngle * 4f));
        DrawArrowLamp(drawList, in camera, SpinnerLamp, -Vector2.UnitY, 0.15f, gold, LampState.Off, time, spinning);
        DrawArrowLamp(drawList, in camera, OrbitLamp, -Vector2.UnitY, 0.15f, Cyan, LampState.Off, time, 0f);
        for (var step = 0; step < MultiplierAngles.Length; step++)
        {
            var angle = MultiplierAngles[step];
            var position = EmblemCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 0.95f;
            DrawCircleLamp(drawList, in camera, position, 0.08f, gold, Lamp(board.Multiplier >= step + 2, false), time,
                0f);
        }

        for (var rollover = 0; rollover < PinballTable.RolloverCount; rollover++)
        {
            var outlane = rollover == PinballTable.LeftOutlane || rollover == PinballTable.RightOutlane;
            DrawArrowLamp(drawList, in camera, PinballTable.Rollovers[rollover] + new Vector2(0f, 0.3f), Vector2.UnitY,
                0.09f, outlane ? Ember : Cyan, LampState.Off, time, board.RolloverFlash(rollover));
        }
    }

    private static void DrawWalls(ImDrawListPtr drawList, in Camera2D camera, Vector4 gold)
    {
        Stroke(drawList, in camera, PinballTable.Cabinet, gold, WallCore * 1.6f, WallHalo * 1.4f);
        Stroke(drawList, in camera, PinballTable.LaneWall, gold, WallCore, WallHalo);
        Stroke(drawList, in camera, PinballTable.LeftOrbitWall, gold, WallCore, WallHalo);
        Stroke(drawList, in camera, PinballTable.LeftDeflector, gold, WallCore, WallHalo);
        Stroke(drawList, in camera, PinballTable.RightOrbitWall, gold, WallCore, WallHalo);
        Stroke(drawList, in camera, PinballTable.RampMouthRail, gold, WallCore, WallHalo);
        Stroke(drawList, in camera, PinballTable.RightDeflector, gold, WallCore, WallHalo);
        Stroke(drawList, in camera, PinballTable.LeftDivider, gold, WallCore, WallHalo);
        Stroke(drawList, in camera, PinballTable.RightDivider, gold, WallCore, WallHalo);
        var guide = ImGui.GetColorU32(gold);
        var post = ImGui.GetColorU32(GamePalette.Lighten(gold, 0.4f));
        for (var index = 0; index < PinballTable.LaneGuides.Length; index++)
        {
            var x = PinballTable.LaneGuides[index];
            var top = camera.ToScreen(new Vector2(x, PinballTable.LaneGuideTop));
            var bottom = camera.ToScreen(new Vector2(x, PinballTable.LaneGuideBottom));
            drawList.AddLine(top, bottom, ImGui.GetColorU32(gold with { W = 0.18f }), camera.Px(WallHalo));
            drawList.AddLine(top, bottom, guide, MathF.Max(1f, camera.Px(WallCore * 1.5f)));
            drawList.AddCircleFilled(top, camera.Px(0.05f), post, 10);
            drawList.AddCircleFilled(bottom, camera.Px(0.05f), post, 10);
        }

        var gate = ImGui.GetColorU32(Steel);
        drawList.AddLine(camera.ToScreen(PinballTable.GateStart), camera.ToScreen(PinballTable.GateEnd), gate,
            MathF.Max(1f, camera.Px(0.04f)));
        drawList.AddCircleFilled(camera.ToScreen(PinballTable.GateEnd), camera.Px(0.04f), gate, 8);
    }

    private static void DrawSlings(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board)
    {
        for (var sling = 0; sling < PinballTable.SlingCount; sling++)
        {
            var points = PinballTable.Sling(sling);
            var flash = board.SlingFlash(sling);
            var top = camera.ToScreen(points[0]);
            var corner = camera.ToScreen(points[1]);
            var bottom = camera.ToScreen(points[2]);
            drawList.AddTriangleFilled(top, corner, bottom, ImGui.GetColorU32(Violet with { W = 0.16f + 0.4f * flash }));
            Stroke(drawList, in camera, points, Violet, WallCore, WallHalo);
            var face = Vector4.Lerp(GamePalette.Lighten(Violet, 0.3f), White, flash);
            var normal = PinballTable.SlingFaceNormal(sling) * (0.05f * flash);
            var faceStart = camera.ToScreen(points[0] + normal);
            var faceEnd = camera.ToScreen(points[2] + normal);
            drawList.AddLine(faceStart, faceEnd, ImGui.GetColorU32(face with { W = 0.3f }), camera.Px(0.16f + 0.1f * flash));
            drawList.AddLine(faceStart, faceEnd, ImGui.GetColorU32(face), MathF.Max(1.5f, camera.Px(0.06f)));
        }
    }

    private static void DrawPosts(ImDrawListPtr drawList, in Camera2D camera, Vector4 gold)
    {
        for (var post = 0; post < PinballTable.Posts.Length; post++)
        {
            var center = camera.ToScreen(PinballTable.Posts[post]);
            drawList.AddCircleFilled(center, camera.Px(PinballTable.PostRadius + 0.03f), ImGui.GetColorU32(White with
            {
                W = 0.85f,
            }), 14);
            drawList.AddCircleFilled(center, camera.Px(PinballTable.PostRadius * 0.6f), ImGui.GetColorU32(gold), 12);
        }
    }

    private static void DrawTargets(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board)
    {
        var half = PinballTable.TargetHalfExtents;
        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            var color = BankColor(target / PinballTable.BankSize);
            var min = camera.ToScreen(PinballTable.Targets[target] - half);
            var max = camera.ToScreen(PinballTable.Targets[target] + half);
            if (board.TargetDown(target))
            {
                drawList.AddRect(min, max, ImGui.GetColorU32(color with { W = 0.3f }), camera.Px(0.02f), ImDrawFlags.None,
                    MathF.Max(1f, camera.Px(0.015f)));
                continue;
            }

            var shadow = camera.Px(0.05f);
            drawList.AddRectFilled(min + new Vector2(shadow), max + new Vector2(shadow), ImGui.GetColorU32(Shadow),
                camera.Px(0.02f));
            drawList.AddRectFilled(min - new Vector2(camera.Px(0.05f)), max + new Vector2(camera.Px(0.05f)),
                ImGui.GetColorU32(color with { W = 0.16f }), camera.Px(0.06f));
            drawList.AddRectFilledMultiColor(min, max, ImGui.GetColorU32(GamePalette.Lighten(color, 0.35f)),
                ImGui.GetColorU32(GamePalette.Lighten(color, 0.35f)), ImGui.GetColorU32(GamePalette.Darken(color, 0.2f)),
                ImGui.GetColorU32(GamePalette.Darken(color, 0.2f)));
            drawList.AddRect(min, max, ImGui.GetColorU32(White with { W = 0.5f }), 0f, ImDrawFlags.None,
                MathF.Max(1f, camera.Px(0.012f)));
        }
    }

    private static void DrawSaucer(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, float time)
    {
        var center = camera.ToScreen(PinballTable.Saucer);
        var radius = camera.Px(PinballTable.SaucerRadius);
        var glow = board.LockLit ? 0.45f + 0.35f * MathF.Sin(time * 5f) : 0.1f;
        ProgressRing.Glow(center, radius * 1.2f, Violet, glow + board.SaucerFlash);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(Violet, 0.55f)), 24);
        drawList.AddCircleFilled(center, radius * 0.72f, ImGui.GetColorU32(Hole), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Lighten(Violet, 0.2f)), 24,
            MathF.Max(1f, camera.Px(0.03f)));
    }

    private static void DrawSpinner(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, Vector4 gold)
    {
        var center = PinballTable.Spinner;
        var width = PinballTable.SpinnerHalfWidth;
        var depth = 0.12f * MathF.Cos(board.SpinnerAngle);
        var facing = depth >= 0f ? gold : GamePalette.Darken(gold, 0.45f);
        var min = camera.ToScreen(center - new Vector2(width, MathF.Abs(depth) + 0.01f));
        var max = camera.ToScreen(center + new Vector2(width, MathF.Abs(depth) + 0.01f));
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(facing), camera.Px(0.02f));
        drawList.AddRect(min, max, ImGui.GetColorU32(White with { W = 0.45f }), camera.Px(0.02f), ImDrawFlags.None,
            MathF.Max(1f, camera.Px(0.01f)));
        drawList.AddLine(camera.ToScreen(center - new Vector2(width + 0.02f, 0f)),
            camera.ToScreen(center + new Vector2(width + 0.02f, 0f)), ImGui.GetColorU32(Steel),
            MathF.Max(1f, camera.Px(0.025f)));
    }

    private static void DrawBumpers(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, Vector4 gold)
    {
        for (var bumper = 0; bumper < PinballTable.BumperCount; bumper++)
        {
            var world = PinballTable.Bumpers[bumper];
            var flash = board.BumperFlash(bumper);
            var center = camera.ToScreen(world);
            var radius = camera.Px(PinballTable.BumperRadius);
            var cap = camera.Px(BumperCapRadius * (1f - 0.08f * flash));
            drawList.AddCircleFilled(camera.ToScreen(world + ShadowOffset), radius, ImGui.GetColorU32(Shadow), 28);
            ProgressRing.Glow(center, radius * (1.1f + 0.3f * flash), Magenta, 0.35f + flash);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(Magenta, 0.55f)), 28);
            drawList.AddCircle(center, radius, ImGui.GetColorU32(Vector4.Lerp(Magenta, White, flash)), 28,
                MathF.Max(1.5f, camera.Px(0.05f)));
            Squircle.FillCircleVerticalGradient(drawList, center, cap,
                ImGui.GetColorU32(Vector4.Lerp(GamePalette.Lighten(Magenta, 0.35f), White, flash)),
                ImGui.GetColorU32(GamePalette.Darken(Magenta, 0.1f)), 28);
            drawList.AddCircle(center, cap, ImGui.GetColorU32(gold), 28, MathF.Max(1f, camera.Px(0.025f)));
            DrawCrystal(drawList, center, cap * 0.55f, ImGui.GetColorU32(White with { W = 0.9f }));
        }
    }

    private static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float size, uint color)
    {
        drawList.AddQuadFilled(center + new Vector2(0f, -size), center + new Vector2(size * 0.55f, 0f),
            center + new Vector2(0f, size), center + new Vector2(-size * 0.55f, 0f), color);
    }

    private static void DrawPlunger(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, Vector4 gold,
        float kick)
    {
        var pull = board.PlungerPull;
        var tipY = PinballTable.PlungerFloorY + pull * PlungerTravel - MathF.Min(KickTravel, kick * KickTravel);
        var tipMin = camera.ToScreen(new Vector2(PinballTable.LaneCenterX - 0.18f, tipY));
        var tipMax = camera.ToScreen(new Vector2(PinballTable.LaneCenterX + 0.18f, tipY + 0.1f));
        var rodX = PinballTable.LaneCenterX;
        var steel = ImGui.GetColorU32(Steel);
        var coil = ImGui.GetColorU32(GamePalette.Darken(Steel, 0.2f));
        var coilTop = tipY + 0.12f;
        var coilBottom = PinballTable.CabinetBottom;
        const int turns = 6;
        var step = (coilBottom - coilTop) / turns;
        for (var turn = 0; turn < turns; turn++)
        {
            var from = camera.ToScreen(new Vector2(rodX - 0.13f, coilTop + turn * step));
            var to = camera.ToScreen(new Vector2(rodX + 0.13f, coilTop + (turn + 0.5f) * step));
            var back = camera.ToScreen(new Vector2(rodX - 0.13f, coilTop + (turn + 1f) * step));
            drawList.AddLine(from, to, coil, MathF.Max(1f, camera.Px(0.03f)));
            drawList.AddLine(to, back, coil, MathF.Max(1f, camera.Px(0.03f)));
        }

        drawList.AddRectFilled(tipMin, tipMax, steel, camera.Px(0.04f));
        drawList.AddRectFilled(tipMin, new Vector2(tipMax.X, tipMin.Y + camera.Px(0.03f)),
            ImGui.GetColorU32(White with { W = 0.8f }), camera.Px(0.02f));
        if (!board.BallWaiting)
        {
            return;
        }

        var meterMin = camera.ToScreen(new Vector2(PinballTable.LaneOuterX - 0.09f, 9.3f));
        var meterMax = camera.ToScreen(new Vector2(PinballTable.LaneOuterX - 0.04f, PinballTable.PlungerFloorY));
        drawList.AddRectFilled(meterMin, meterMax, ImGui.GetColorU32(gold with { W = 0.15f }), camera.Px(0.03f));
        var fillTop = meterMax.Y - (meterMax.Y - meterMin.Y) * pull;
        drawList.AddRectFilledMultiColor(new Vector2(meterMin.X, fillTop), meterMax,
            ImGui.GetColorU32(White), ImGui.GetColorU32(White), ImGui.GetColorU32(gold), ImGui.GetColorU32(gold));
    }

    private static void DrawApron(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, Vector4 gold,
        float time)
    {
        var min = camera.ToScreen(new Vector2(0f, ApronTop));
        var max = camera.ToScreen(new Vector2(PinballTable.LaneInnerX, PinballTable.CabinetBottom));
        drawList.AddRectFilledMultiColor(min, max, ImGui.GetColorU32(Apron with { W = 0.75f }),
            ImGui.GetColorU32(Apron with { W = 0.75f }), ImGui.GetColorU32(Apron), ImGui.GetColorU32(Apron));
        drawList.AddLine(min, new Vector2(max.X, min.Y), ImGui.GetColorU32(gold with { W = 0.5f }),
            MathF.Max(1f, camera.Px(0.025f)));
        var saving = board.BallSaveLeft > 0f;
        var state = board.ShootAgainLit
            ? LampState.On
            : saving
                ? board.BallSaveLeft < 2f ? LampState.Blink : LampState.On
                : LampState.Off;
        DrawCircleLamp(drawList, in camera, ShootAgainLamp, 0.15f, Ember, state, time, 0f);
    }

    private static void DrawFlippers(ImDrawListPtr drawList, in Camera2D camera, PinballBoard board, Vector4 gold)
    {
        for (var flipper = 0; flipper < PinballTable.FlipperCount; flipper++)
        {
            var pivot = PinballTable.Pivot(flipper);
            var angle = board.FlipperAngle(flipper);
            var lift = board.FlipperLift(flipper);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tip = pivot + direction * (PinballTable.FlipperLength - FlipperTipRadius);
            DrawCapsule(drawList, in camera, pivot + ShadowOffset, tip + ShadowOffset, FlipperPivotRadius,
                FlipperTipRadius, ImGui.GetColorU32(Shadow));
            if (lift > 0.05f)
            {
                DrawCapsule(drawList, in camera, pivot, tip, FlipperPivotRadius + 0.05f, FlipperTipRadius + 0.05f,
                    ImGui.GetColorU32(gold with { W = 0.22f * lift }));
            }

            DrawCapsule(drawList, in camera, pivot, tip, FlipperPivotRadius, FlipperTipRadius,
                ImGui.GetColorU32(GamePalette.Lighten(gold, 0.15f)));
            DrawCapsule(drawList, in camera, pivot, tip, FlipperPivotRadius * 0.72f, FlipperTipRadius * 0.6f,
                ImGui.GetColorU32(Vector4.Lerp(new Vector4(0.96f, 0.95f, 0.92f, 1f), White, lift)));
            drawList.AddCircleFilled(camera.ToScreen(pivot), camera.Px(0.035f), ImGui.GetColorU32(Steel), 10);
        }
    }

    private static void DrawCapsule(ImDrawListPtr drawList, in Camera2D camera, Vector2 from, Vector2 to,
        float fromRadius, float toRadius, uint color)
    {
        var direction = Vector2.Normalize(to - from);
        var side = new Vector2(-direction.Y, direction.X);
        var start = camera.ToScreen(from);
        var end = camera.ToScreen(to);
        var startRadius = camera.Px(fromRadius);
        var endRadius = camera.Px(toRadius);
        drawList.AddCircleFilled(start, startRadius, color, 16);
        drawList.AddCircleFilled(end, endRadius, color, 12);
        drawList.AddQuadFilled(start + side * startRadius, end + side * endRadius, end - side * endRadius,
            start - side * startRadius, color);
    }

    private static void DrawCircleLamp(ImDrawListPtr drawList, in Camera2D camera, Vector2 position, float radius,
        Vector4 color, LampState state, float time, float flash)
    {
        var center = camera.ToScreen(position);
        var size = camera.Px(radius);
        var level = Level(state, time, flash);
        drawList.AddCircleFilled(center, size, ImGui.GetColorU32(GamePalette.Darken(color, 0.6f) with { W = 0.55f }), 16);
        if (level > 0.01f)
        {
            ProgressRing.Glow(center, size * 1.4f, color, level);
            drawList.AddCircleFilled(center, size * 0.85f, ImGui.GetColorU32(color with { W = level }), 16);
            drawList.AddCircleFilled(center, size * 0.38f, ImGui.GetColorU32(White with { W = level * 0.8f }), 10);
        }

        drawList.AddCircle(center, size, ImGui.GetColorU32(color with { W = 0.35f + 0.4f * level }), 16,
            MathF.Max(1f, camera.Px(0.012f)));
    }

    private static void DrawArrowLamp(ImDrawListPtr drawList, in Camera2D camera, Vector2 position, Vector2 direction,
        float size, Vector4 color, LampState state, float time, float flash)
    {
        var side = new Vector2(-direction.Y, direction.X);
        var tip = camera.ToScreen(position + direction * size);
        var left = camera.ToScreen(position - direction * size * 0.6f + side * size * 0.8f);
        var right = camera.ToScreen(position - direction * size * 0.6f - side * size * 0.8f);
        var level = Level(state, time, flash);
        drawList.AddTriangleFilled(tip, left, right, ImGui.GetColorU32(GamePalette.Darken(color, 0.6f) with { W = 0.5f }));
        if (level > 0.01f)
        {
            ProgressRing.Glow(camera.ToScreen(position), camera.Px(size * 1.3f), color, level);
            drawList.AddTriangleFilled(tip, left, right, ImGui.GetColorU32(color with { W = level }));
        }

        drawList.PathClear();
        drawList.PathLineTo(tip);
        drawList.PathLineTo(left);
        drawList.PathLineTo(right);
        drawList.PathStroke(ImGui.GetColorU32(color with { W = 0.35f + 0.4f * level }), ImDrawFlags.Closed,
            MathF.Max(1f, camera.Px(0.012f)));
    }

    private static float Level(LampState state, float time, float flash)
    {
        var level = state switch
        {
            LampState.On => 1f,
            LampState.Blink => ((int)(time * BlinkHertz * 2f) & 1) == 0 ? 1f : 0.12f,
            _ => 0f,
        };
        if (flash > 0f && ((int)(time * FastBlinkHertz * 2f) & 1) == 0)
        {
            level = MathF.Max(level, flash);
        }

        return level;
    }

    private static void Stroke(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> points, Vector4 color,
        float coreWidth, float haloWidth)
    {
        Trace(drawList, in camera, points);
        drawList.PathStroke(ImGui.GetColorU32(color with { W = 0.16f }), ImDrawFlags.None, camera.Px(haloWidth));
        Trace(drawList, in camera, points);
        drawList.PathStroke(ImGui.GetColorU32(color), ImDrawFlags.None, MathF.Max(1f, camera.Px(coreWidth)));
    }

    private static void Trace(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(camera.ToScreen(points[index]));
        }
    }
}
