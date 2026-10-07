using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.MiniGolf;

internal static class MiniGolfRenderer
{
    public const int MaxAimPoints = 6;
    private const float RailWidth = 0.15f;
    private const float FlagHeight = 0.9f;
    private const float FlagFadeRange = 1.3f;
    private const float AimDotSpacing = 0.22f;
    private const float ChevronSpacing = 0.55f;
    private const float ChevronSpeed = 0.5f;
    private const int SpeckleCount = 14;
    public static readonly Vector4 Fairway = new(0.36f, 0.70f, 0.34f, 1f);
    public static readonly Vector4 SandTint = new(0.93f, 0.84f, 0.60f, 1f);
    public static readonly Vector4 WaterTint = new(0.26f, 0.56f, 0.90f, 1f);
    public static readonly Vector4 Grass = new(0.30f, 0.62f, 0.28f, 1f);
    public static readonly Vector4 BallWhite = new(0.98f, 0.98f, 0.97f, 1f);
    private static readonly Vector4 FairwayEdge = new(0.28f, 0.56f, 0.27f, 1f);
    private static readonly Vector4 Rail = new(0.55f, 0.37f, 0.22f, 1f);
    private static readonly Vector4 RailLight = new(0.80f, 0.62f, 0.42f, 1f);
    private static readonly Vector4 Stone = new(0.55f, 0.56f, 0.60f, 1f);
    private static readonly Vector4 SandRim = new(0.78f, 0.66f, 0.42f, 1f);
    private static readonly Vector4 WaterDeep = new(0.16f, 0.38f, 0.72f, 1f);
    private static readonly Vector4 Bumper = new(0.92f, 0.30f, 0.32f, 1f);
    private static readonly Vector4 Hole = new(0.08f, 0.10f, 0.08f, 1f);
    private static readonly Vector4 Pole = new(0.96f, 0.96f, 0.94f, 1f);
    private static readonly Vector4 Wood = new(0.66f, 0.46f, 0.28f, 1f);
    private static readonly Vector4 Roof = new(0.78f, 0.30f, 0.26f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 TeeMat = new(0.24f, 0.52f, 0.26f, 1f);

    public static void DrawCourse(ImDrawListPtr drawList, in Camera2D camera, MiniGolfHole hole, float alpha, float time)
    {
        var lift = new Vector2(0f, 0.14f);
        FillTriangles(drawList, camera, hole.Course, hole.CourseTriangles, lift, Shadow with { W = 0.28f * alpha });
        FillTriangles(drawList, camera, hole.Course, hole.CourseTriangles, Vector2.Zero, Fairway with { W = alpha });
        DrawMowing(drawList, camera, hole, alpha);
        for (var index = 0; index < hole.Slopes.Length; index++)
        {
            DrawSlope(drawList, camera, hole.Slopes[index], alpha, time);
        }

        for (var index = 0; index < hole.Sand.Length; index++)
        {
            DrawZone(drawList, camera, hole.Sand[index], SandTint with { W = alpha }, SandRim with { W = alpha });
            DrawSpeckles(drawList, camera, hole.Sand[index], index, alpha);
        }

        for (var index = 0; index < hole.Water.Length; index++)
        {
            DrawZone(drawList, camera, hole.Water[index], WaterTint with { W = alpha }, WaterDeep with { W = alpha });
            DrawRipples(drawList, camera, hole.Water[index], alpha, time + index);
        }

        DrawTee(drawList, camera, hole.Tee, alpha);
        for (var index = 0; index < hole.Tunnels.Length; index++)
        {
            DrawTunnel(drawList, camera, hole.Tunnels[index], alpha, time);
        }

        for (var index = 0; index < hole.Blocks.Length; index++)
        {
            FillTriangles(drawList, camera, hole.Blocks[index], hole.BlockTriangles[index], lift, Shadow with { W = 0.25f * alpha });
            FillTriangles(drawList, camera, hole.Blocks[index], hole.BlockTriangles[index], Vector2.Zero, Stone with { W = alpha });
            StrokeChain(drawList, camera, hole.Blocks[index], true, alpha);
        }

        StrokeChain(drawList, camera, hole.Course, true, alpha);
        for (var index = 0; index < hole.Walls.Length; index++)
        {
            StrokeChain(drawList, camera, hole.Walls[index], false, alpha);
        }
    }

    public static void DrawPosts(ImDrawListPtr drawList, in Camera2D camera, MiniGolfHole hole, Vector2 flashPoint,
        float flash, float alpha)
    {
        for (var index = 0; index < hole.Posts.Length; index++)
        {
            var post = hole.Posts[index];
            var lit = flash > 0f && Vector2.Distance(flashPoint, post.Center) < post.Radius + 0.3f ? flash : 0f;
            var center = camera.ToScreen(post.Center);
            var radius = camera.Px(post.Radius) * (1f + 0.12f * lit);
            drawList.AddCircleFilled(center + new Vector2(0f, camera.Px(0.08f)), radius, ImGui.GetColorU32(Shadow with { W = 0.3f * alpha }), 28);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Vector4.Lerp(Bumper, White, lit * 0.6f) with { W = alpha }), 28);
            drawList.AddCircleFilled(center, radius * 0.62f, ImGui.GetColorU32(White with { W = alpha }), 24);
            drawList.AddCircleFilled(center, radius * 0.38f, ImGui.GetColorU32(Bumper with { W = alpha }), 20);
            drawList.AddCircleFilled(center - new Vector2(radius * 0.35f, radius * 0.4f), radius * 0.16f,
                ImGui.GetColorU32(White with { W = 0.7f * alpha }), 10);
            if (lit > 0f)
            {
                ProgressRing.Glow(center, radius * 1.6f, Bumper, 0.6f * lit);
            }
        }
    }

    public static void DrawMills(ImDrawListPtr drawList, in Camera2D camera, MiniGolfBoard board, float alpha)
    {
        var hole = board.Hole;
        var world = board.World;
        for (var index = 0; index < board.MillCount; index++)
        {
            DrawMill(drawList, in camera, hole.Mills[index], world.RenderAngle(board.MillBody(index)), alpha);
        }
    }

    public static void DrawMill(ImDrawListPtr drawList, in Camera2D camera, in GolfMill mill, float angle, float alpha)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        var axis = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var side = new Vector2(-axis.Y, axis.X);
        var hub = camera.ToScreen(mill.Hub);
        var reach = camera.Px(mill.Reach);
        var thickness = camera.Px(MiniGolfBoard.MillThickness);
        var shadow = new Vector2(0f, camera.Px(0.1f));
        corners[0] = hub - axis * reach - side * thickness + shadow;
        corners[1] = hub + axis * reach - side * thickness + shadow;
        corners[2] = hub + axis * reach + side * thickness + shadow;
        corners[3] = hub - axis * reach + side * thickness + shadow;
        drawList.AddConvexPolyFilled(ref corners[0], corners.Length, ImGui.GetColorU32(Shadow with { W = 0.3f * alpha }));
        for (var blade = -1; blade <= 1; blade += 2)
        {
            var tip = hub + axis * reach * blade;
            var root = hub + axis * reach * 0.18f * blade;
            corners[0] = root - side * thickness * 1.6f;
            corners[1] = tip - side * thickness * 1.6f;
            corners[2] = tip + side * thickness * 1.6f;
            corners[3] = root + side * thickness * 1.6f;
            drawList.AddConvexPolyFilled(ref corners[0], corners.Length, ImGui.GetColorU32(White with { W = 0.92f * alpha }));
            drawList.AddPolyline(ref corners[0], corners.Length, ImGui.GetColorU32(Wood with { W = alpha }),
                ImDrawFlags.Closed, MathF.Max(1f, thickness * 0.45f));
            for (var slat = 1; slat < 4; slat++)
            {
                var at = Vector2.Lerp(root, tip, slat / 4f);
                drawList.AddLine(at - side * thickness * 1.6f, at + side * thickness * 1.6f,
                    ImGui.GetColorU32(Wood with { W = alpha }), MathF.Max(1f, thickness * 0.3f));
            }
        }

        var roof = camera.Px(0.24f);
        drawList.AddRectFilled(hub - new Vector2(roof), hub + new Vector2(roof), ImGui.GetColorU32(Roof with { W = alpha }),
            roof * 0.2f);
        drawList.AddLine(hub - new Vector2(roof, 0f), hub + new Vector2(roof, 0f),
            ImGui.GetColorU32(GamePalette.Darken(Roof, 0.3f) with { W = alpha }), MathF.Max(1f, roof * 0.18f));
        drawList.AddCircleFilled(hub, roof * 0.35f, ImGui.GetColorU32(Wood with { W = alpha }), 12);
    }

    public static void DrawSinking(ImDrawListPtr drawList, in Camera2D camera, Vector2 from, Vector2 cup, float progress,
        Vector4 band)
    {
        var eased = Easing.EaseInCubic(progress);
        var radius = camera.Px(MiniGolfBoard.BallRadius);
        DrawBall(drawList, camera.ToScreen(Vector2.Lerp(from, cup, eased)), radius * (1f - 0.55f * eased), band,
            1f - 0.6f * eased);
    }

    public static void DrawDrowning(ImDrawListPtr drawList, in Camera2D camera, Vector2 at, float progress, Vector4 band)
    {
        DrawBall(drawList, camera.ToScreen(at), camera.Px(MiniGolfBoard.BallRadius) * (1f - progress), band,
            1f - progress);
    }

    public static void DrawCup(ImDrawListPtr drawList, in Camera2D camera, Vector2 cup, Vector2 ball, Vector4 flag,
        float alpha, float time)
    {
        var center = camera.ToScreen(cup);
        var radius = camera.Px(MiniGolfBoard.CupRadius);
        drawList.AddCircleFilled(center, radius * 1.18f, ImGui.GetColorU32(White with { W = 0.55f * alpha }), 28);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Hole with { W = alpha }), 28);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.25f), radius * 0.7f,
            ImGui.GetColorU32(Shadow with { W = 0.5f * alpha }), 20);
        var near = Math.Clamp(Vector2.Distance(ball, cup) / FlagFadeRange, 0f, 1f);
        var flagAlpha = alpha * (0.25f + 0.75f * near);
        var top = center - new Vector2(0f, camera.Px(FlagHeight));
        drawList.AddLine(center, top, ImGui.GetColorU32(Pole with { W = flagAlpha }), MathF.Max(1.5f, camera.Px(0.035f)));
        var wave = MathF.Sin(time * 4f) * camera.Px(0.05f);
        var width = camera.Px(0.42f);
        var height = camera.Px(0.26f);
        drawList.AddTriangleFilled(top, top + new Vector2(width, height * 0.5f + wave), top + new Vector2(0f, height),
            ImGui.GetColorU32(flag with { W = flagAlpha }));
        drawList.AddCircleFilled(top, MathF.Max(1.5f, camera.Px(0.04f)), ImGui.GetColorU32(Pole with { W = flagAlpha }), 8);
    }

    public static void DrawBall(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 band, float alpha)
    {
        if (alpha <= 0f || radius <= 0.3f)
        {
            return;
        }

        drawList.AddCircleFilled(center + new Vector2(radius * 0.25f, radius * 0.45f), radius * 1.05f,
            ImGui.GetColorU32(Shadow with { W = 0.3f * alpha }), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BallWhite with { W = alpha }), 24);
        if (band.W > 0f)
        {
            drawList.AddCircle(center, radius * 0.72f, ImGui.GetColorU32(band with { W = band.W * alpha }), 20,
                MathF.Max(1f, radius * 0.28f));
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(0.7f, 0.72f, 0.74f, alpha)), 24,
            MathF.Max(1f, radius * 0.1f));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.32f, radius * 0.36f), radius * 0.3f,
            ImGui.GetColorU32(White with { W = 0.9f * alpha }), 12);
    }

    public static void DrawAim(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> path, Vector2 ball,
        Vector2 pullTo, float power, float time)
    {
        if (path.Length < 2)
        {
            return;
        }

        var color = PowerColor(power);
        var ballScreen = camera.ToScreen(ball);
        drawList.AddLine(ballScreen, pullTo, ImGui.GetColorU32(White with { W = 0.35f }), 2f);
        var radius = camera.Px(MiniGolfBoard.BallRadius);
        var ring = radius * 2.4f;
        ProgressRing.Track(drawList, ballScreen, ring, MathF.Max(2f, radius * 0.4f), White with { W = 0.2f });
        ProgressRing.Fill(drawList, ballScreen, ring, MathF.Max(2f, radius * 0.4f), power, color);
        var spacing = camera.Px(AimDotSpacing);
        var offset = time * spacing * 2.5f % spacing;
        var travelled = 0f;
        var total = MathF.Max(1f, TotalLength(camera, path));
        var dot = MathF.Max(1.5f, radius * 0.3f);
        for (var index = 1; index < path.Length; index++)
        {
            var from = camera.ToScreen(path[index - 1]);
            var to = camera.ToScreen(path[index]);
            var segment = to - from;
            var length = segment.Length();
            if (length <= 0.5f)
            {
                continue;
            }

            var direction = segment / length;
            for (var along = spacing - (travelled + offset) % spacing; along < length; along += spacing)
            {
                var fade = 1f - (travelled + along) / total;
                drawList.AddCircleFilled(from + direction * along, dot, ImGui.GetColorU32(color with { W = 0.4f + 0.6f * fade }), 8);
            }

            travelled += length;
        }

        var end = camera.ToScreen(path[^1]);
        var last = Vector2.Normalize(end - camera.ToScreen(path[^2]) + new Vector2(0.0001f, 0f));
        var wing = new Vector2(-last.Y, last.X);
        drawList.AddTriangleFilled(end + last * dot * 3f, end - last * dot * 1.5f + wing * dot * 2.2f,
            end - last * dot * 1.5f - wing * dot * 2.2f, ImGui.GetColorU32(color));
    }

    public static Vector4 PowerColor(float power)
    {
        var calm = new Vector4(0.45f, 0.95f, 0.55f, 1f);
        var warm = new Vector4(1f, 0.85f, 0.30f, 1f);
        var hot = new Vector4(1f, 0.38f, 0.30f, 1f);
        return power < 0.5f ? Vector4.Lerp(calm, warm, power * 2f) : Vector4.Lerp(warm, hot, (power - 0.5f) * 2f);
    }

    private static float TotalLength(in Camera2D camera, ReadOnlySpan<Vector2> path)
    {
        var total = 0f;
        for (var index = 1; index < path.Length; index++)
        {
            total += camera.Px(Vector2.Distance(path[index - 1], path[index]));
        }

        return total;
    }

    private static void FillTriangles(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> points,
        ReadOnlySpan<int> triangles, Vector2 offset, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        for (var index = 0; index + 2 < triangles.Length; index += 3)
        {
            drawList.AddTriangleFilled(camera.ToScreen(points[triangles[index]] + offset),
                camera.ToScreen(points[triangles[index + 1]] + offset), camera.ToScreen(points[triangles[index + 2]] + offset),
                packed);
        }
    }

    private static void DrawMowing(ImDrawListPtr drawList, in Camera2D camera, MiniGolfHole hole, float alpha)
    {
        var bounds = hole.Bounds;
        var stripe = ImGui.GetColorU32(White with { W = 0.045f * alpha });
        for (var stripeTop = MathF.Floor(bounds.Min.Y); stripeTop < bounds.Max.Y; stripeTop += 1f)
        {
            if (((int)stripeTop & 1) != 0)
            {
                continue;
            }

            for (var index = 0; index + 2 < hole.CourseTriangles.Length; index += 3)
            {
                ClipStripe(drawList, camera, hole.Course[hole.CourseTriangles[index]],
                    hole.Course[hole.CourseTriangles[index + 1]], hole.Course[hole.CourseTriangles[index + 2]], stripeTop,
                    stripeTop + 1f, stripe);
            }
        }
    }

    private static void ClipStripe(ImDrawListPtr drawList, in Camera2D camera, Vector2 a, Vector2 b, Vector2 c, float top,
        float bottom, uint color)
    {
        Span<Vector2> polygon = stackalloc Vector2[9];
        Span<Vector2> scratch = stackalloc Vector2[9];
        polygon[0] = a;
        polygon[1] = b;
        polygon[2] = c;
        var count = ClipAgainst(polygon, 3, scratch, top, true);
        count = ClipAgainst(scratch, count, polygon, bottom, false);
        if (count < 3)
        {
            return;
        }

        for (var index = 1; index + 1 < count; index++)
        {
            drawList.AddTriangleFilled(camera.ToScreen(polygon[0]), camera.ToScreen(polygon[index]),
                camera.ToScreen(polygon[index + 1]), color);
        }
    }

    private static int ClipAgainst(ReadOnlySpan<Vector2> input, int count, Span<Vector2> output, float line, bool keepBelow)
    {
        var written = 0;
        for (var index = 0; index < count; index++)
        {
            var current = input[index];
            var next = input[(index + 1) % count];
            var currentIn = keepBelow ? current.Y >= line : current.Y <= line;
            var nextIn = keepBelow ? next.Y >= line : next.Y <= line;
            if (currentIn)
            {
                output[written++] = current;
            }

            if (currentIn != nextIn)
            {
                var along = (line - current.Y) / (next.Y - current.Y);
                output[written++] = Vector2.Lerp(current, next, along);
            }
        }

        return written;
    }

    private static void StrokeChain(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> chain, bool closed,
        float alpha)
    {
        var width = camera.Px(RailWidth);
        var flags = closed ? ImDrawFlags.Closed : ImDrawFlags.None;
        var shadowOffset = new Vector2(0f, camera.Px(0.06f));
        for (var pass = 0; pass < 3; pass++)
        {
            drawList.PathClear();
            var offset = pass == 0 ? shadowOffset : pass == 2 ? new Vector2(0f, -width * 0.18f) : Vector2.Zero;
            for (var index = 0; index < chain.Length; index++)
            {
                drawList.PathLineTo(camera.ToScreen(chain[index]) + offset);
            }

            var color = pass switch
            {
                0 => Shadow with { W = 0.3f * alpha },
                1 => Rail with { W = alpha },
                _ => RailLight with { W = 0.8f * alpha },
            };
            drawList.PathStroke(ImGui.GetColorU32(color), flags, pass == 2 ? width * 0.3f : width);
        }

        for (var index = 0; index < chain.Length; index++)
        {
            if (!closed && (index == 0 || index == chain.Length - 1))
            {
                drawList.AddCircleFilled(camera.ToScreen(chain[index]), width * 0.5f, ImGui.GetColorU32(Rail with { W = alpha }), 10);
            }
        }
    }

    private static void DrawZone(ImDrawListPtr drawList, in Camera2D camera, in GolfZone zone, Vector4 fill, Vector4 rim)
    {
        var min = camera.ToScreen(zone.Min);
        var max = camera.ToScreen(zone.Max);
        if (zone.Round)
        {
            var center = (min + max) * 0.5f;
            var radius = (max.X - min.X) * 0.5f;
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 36);
            drawList.AddCircle(center, radius, ImGui.GetColorU32(rim), 36, MathF.Max(1f, camera.Px(0.05f)));
            return;
        }

        var rounding = MathF.Min(camera.Px(0.3f), MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(rim), rounding, ImDrawFlags.None, MathF.Max(1f, camera.Px(0.05f)));
    }

    private static void DrawSpeckles(ImDrawListPtr drawList, in Camera2D camera, in GolfZone zone, int seed, float alpha)
    {
        var size = zone.Max - zone.Min;
        var dot = MathF.Max(1f, camera.Px(0.025f));
        var color = ImGui.GetColorU32(SandRim with { W = 0.7f * alpha });
        for (var index = 0; index < SpeckleCount; index++)
        {
            var hashX = Fraction((index + 1) * 12.9898f + seed * 78.233f);
            var hashY = Fraction((index + 1) * 39.3468f + seed * 11.135f);
            var point = zone.Min + new Vector2(hashX * size.X, hashY * size.Y);
            if (!zone.Contains(point))
            {
                continue;
            }

            drawList.AddCircleFilled(camera.ToScreen(point), dot, color, 6);
        }
    }

    private static void DrawRipples(ImDrawListPtr drawList, in Camera2D camera, in GolfZone zone, float alpha, float time)
    {
        var min = camera.ToScreen(zone.Min);
        var max = camera.ToScreen(zone.Max);
        drawList.PushClipRect(min, max, true);
        var color = ImGui.GetColorU32(White with { W = 0.35f * alpha });
        var spacing = camera.Px(0.35f);
        var amplitude = camera.Px(0.05f);
        var index = 0;
        for (var rippleTop = min.Y + spacing * 0.5f; rippleTop < max.Y; rippleTop += spacing)
        {
            var phase = time * 1.6f + index * 1.3f;
            var left = min.X + (MathF.Sin(phase) * 0.5f + 0.5f) * (max.X - min.X) * 0.4f;
            var right = left + (max.X - min.X) * 0.45f;
            drawList.AddBezierQuadratic(new Vector2(left, rippleTop),
                new Vector2((left + right) * 0.5f, rippleTop - amplitude * 2f), new Vector2(right, rippleTop), color,
                MathF.Max(1f, camera.Px(0.03f)), 10);
            index++;
        }

        drawList.PopClipRect();
    }

    private static void DrawSlope(ImDrawListPtr drawList, in Camera2D camera, in GolfSlope slope, float alpha, float time)
    {
        var min = camera.ToScreen(slope.Zone.Min);
        var max = camera.ToScreen(slope.Zone.Max);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(FairwayEdge with { W = 0.45f * alpha }), camera.Px(0.12f));
        var push = slope.Push;
        var strength = push.Length();
        if (strength <= 0.001f)
        {
            return;
        }

        var direction = push / strength;
        var side = new Vector2(-direction.Y, direction.X);
        drawList.PushClipRect(min, max, true);
        var spacing = ChevronSpacing;
        var shift = time * ChevronSpeed * strength % spacing;
        var center = slope.Zone.Center;
        var reach = (slope.Zone.Max - slope.Zone.Min).Length() * 0.5f;
        var color = ImGui.GetColorU32(White with { W = 0.28f * alpha });
        var arm = camera.Px(0.16f);
        var thickness = MathF.Max(1f, camera.Px(0.05f));
        for (var along = -reach; along <= reach; along += spacing)
        {
            for (var across = -reach; across <= reach; across += spacing * 1.3f)
            {
                var point = camera.ToScreen(center + direction * (along + shift) + side * across);
                var back = point - direction * arm;
                drawList.AddLine(back + side * arm, point, color, thickness);
                drawList.AddLine(back - side * arm, point, color, thickness);
            }
        }

        drawList.PopClipRect();
    }

    private static void DrawTee(ImDrawListPtr drawList, in Camera2D camera, Vector2 tee, float alpha)
    {
        var center = camera.ToScreen(tee);
        var half = new Vector2(camera.Px(0.42f), camera.Px(0.26f));
        drawList.AddRectFilled(center - half, center + half, ImGui.GetColorU32(TeeMat with { W = alpha }), camera.Px(0.08f));
        drawList.AddRect(center - half, center + half, ImGui.GetColorU32(White with { W = 0.25f * alpha }), camera.Px(0.08f),
            ImDrawFlags.None, MathF.Max(1f, camera.Px(0.02f)));
    }

    private static void DrawTunnel(ImDrawListPtr drawList, in Camera2D camera, in GolfTunnel tunnel, float alpha, float time)
    {
        var entry = camera.ToScreen(tunnel.Entry);
        var exit = camera.ToScreen(tunnel.Exit);
        var radius = camera.Px(MiniGolfBoard.TunnelRadius);
        var span = exit - entry;
        var length = span.Length();
        if (length > 1f)
        {
            var direction = span / length;
            var spacing = camera.Px(0.3f);
            var offset = time * spacing * 1.5f % spacing;
            var dot = MathF.Max(1f, camera.Px(0.03f));
            for (var along = radius + offset; along < length - radius; along += spacing)
            {
                drawList.AddCircleFilled(entry + direction * along, dot, ImGui.GetColorU32(Shadow with { W = 0.22f * alpha }), 6);
            }
        }

        drawList.AddCircleFilled(entry, radius * 1.15f, ImGui.GetColorU32(Stone with { W = alpha }), 28);
        drawList.AddCircleFilled(entry, radius * 0.85f, ImGui.GetColorU32(Hole with { W = alpha }), 28);
        drawList.AddCircle(entry, radius * 0.85f, ImGui.GetColorU32(Shadow with { W = 0.5f * alpha }), 28,
            MathF.Max(1f, radius * 0.15f));
        drawList.AddCircle(exit, radius * 0.95f, ImGui.GetColorU32(Stone with { W = alpha }), 28, MathF.Max(1.5f, radius * 0.3f));
        drawList.AddCircleFilled(exit, radius * 0.7f, ImGui.GetColorU32(Hole with { W = 0.55f * alpha }), 24);
    }

    private static float Fraction(float value)
    {
        var raw = MathF.Sin(value) * 43758.5453f;
        return raw - MathF.Floor(raw);
    }
}
