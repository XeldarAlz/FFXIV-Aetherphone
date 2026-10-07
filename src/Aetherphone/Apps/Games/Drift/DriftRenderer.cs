using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Drift;

internal static class DriftRenderer
{
    public static readonly Vector4 RockTint = new(0.80f, 0.88f, 1f, 1f);
    public static readonly Vector4 SaucerColor = new(0.98f, 0.40f, 0.56f, 1f);
    public static readonly Vector4 ShotColor = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 SaucerShotColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 FlameColor = new(1f, 0.72f, 0.36f, 1f);
    private const int RockVertices = 11;
    private const ulong ShapeSeed = 0x4452494654UL;
    private const float StrokeWidth = 1.5f;
    private const float FrameAlpha = 0.16f;
    private const float BracketAlpha = 0.55f;
    private const float BracketFraction = 0.08f;
    private const float RockFillAlpha = 0.06f;
    private const float ShipFillAlpha = 0.14f;
    private const float BlinkRate = 10f;
    private const float ShotTail = 0.025f;
    private static readonly float[] RockJitter = BuildJitter();
    private static readonly Vector2[] ShipOutline =
    {
        new(0f, -1.25f), new(0.85f, 1f), new(0f, 0.55f), new(-0.85f, 1f),
    };

    private static readonly Vector2[] SaucerHull =
    {
        new(-1f, 0f), new(-0.55f, 0.42f), new(0.55f, 0.42f), new(1f, 0f), new(0.55f, -0.32f), new(-0.55f, -0.32f),
    };

    private static readonly Vector2[] SaucerDome =
    {
        new(-0.42f, -0.32f), new(-0.26f, -0.72f), new(0.26f, -0.72f), new(0.42f, -0.32f),
    };

    public static Rect WorldRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(DriftBoard.Width, DriftBoard.Height)));

    public static Vector4 RockColor(RockSize size, Vector4 accent) => size switch
    {
        RockSize.Large => Vector4.Lerp(RockTint, accent, 0.45f),
        RockSize.Medium => Vector4.Lerp(RockTint, accent, 0.25f),
        _ => RockTint,
    };

    public static void DrawFrame(ImDrawListPtr drawList, Rect world, Vector4 accent, float scale)
    {
        drawList.AddRect(world.Min, world.Max, ImGui.GetColorU32(accent with { W = FrameAlpha }), 0f, ImDrawFlags.None,
            MathF.Max(1f, scale));
        var arm = world.Width * BracketFraction;
        var color = accent with { W = BracketAlpha };
        var width = MathF.Max(1f, scale);
        Bracket(drawList, world.Min, new Vector2(arm, 0f), new Vector2(0f, arm), color, width);
        Bracket(drawList, new Vector2(world.Max.X, world.Min.Y), new Vector2(-arm, 0f), new Vector2(0f, arm), color,
            width);
        Bracket(drawList, new Vector2(world.Min.X, world.Max.Y), new Vector2(arm, 0f), new Vector2(0f, -arm), color,
            width);
        Bracket(drawList, world.Max, new Vector2(-arm, 0f), new Vector2(0f, -arm), color, width);
    }

    public static void DrawWorld(ImDrawListPtr drawList, DriftBoard board, in Camera2D camera, Vector4 accent,
        float scale, float time)
    {
        var width = MathF.Max(1f, StrokeWidth * scale);
        Span<Vector2> offsets = stackalloc Vector2[4];
        Span<Vector2> points = stackalloc Vector2[RockVertices];
        for (var index = 0; index < board.RockCount; index++)
        {
            ref readonly var rock = ref board.RockAt(index);
            var radius = DriftBoard.RockRadius(rock.Size);
            var color = RockColor(rock.Size, accent);
            var copies = WrapCopies(rock.Position, radius, true, offsets);
            for (var copy = 0; copy < copies; copy++)
            {
                RockPoints(in rock, rock.Position + offsets[copy], radius, in camera, points);
                NeonStroke.Fill(drawList, points, color, RockFillAlpha);
                NeonStroke.Path(drawList, points, true, color, width);
            }
        }

        if (board.SaucerActive)
        {
            DrawSaucer(drawList, board, in camera, width, time, offsets);
        }

        DrawShots(drawList, board, in camera, accent, scale);
        DrawShip(drawList, board, in camera, accent, width, time, offsets);
    }

    public static void DrawShipShape(ImDrawListPtr drawList, Vector2 center, float angle, float size, in Camera2D camera,
        Vector4 color, float width, float alpha)
    {
        Span<Vector2> points = stackalloc Vector2[ShipOutline.Length];
        Transform(ShipOutline, center, angle, size, in camera, points);
        var tint = color with { W = color.W * alpha };
        NeonStroke.Fill(drawList, points, tint, ShipFillAlpha);
        NeonStroke.Path(drawList, points, true, tint, width);
    }

    private static void DrawShip(ImDrawListPtr drawList, DriftBoard board, in Camera2D camera, Vector4 accent,
        float width, float time, Span<Vector2> offsets)
    {
        if (board.State == ShipState.Warping)
        {
            var progress = board.WarpProgress;
            DrawShipShape(drawList, board.WarpFrom, board.ShipAngle, DriftBoard.ShipSize * (1f - progress), in camera,
                accent, width, 1f - progress);
            DrawShipShape(drawList, board.WarpTarget, board.ShipAngle, DriftBoard.ShipSize * progress, in camera, accent,
                width, progress);
            return;
        }

        if (!board.ShipVisible)
        {
            return;
        }

        var alpha = board.Invulnerable && (int)(time * BlinkRate) % 2 == 0 ? 0.35f : 1f;
        var copies = WrapCopies(board.ShipPosition, DriftBoard.ShipSize * 1.3f, true, offsets);
        Span<Vector2> flame = stackalloc Vector2[3];
        var reach = 1.5f + 0.7f * (0.5f + 0.5f * MathF.Sin(time * 47f));
        Span<Vector2> flameLocal = stackalloc Vector2[3]
        {
            new(-0.45f, 0.82f), new(0f, 0.82f + reach), new(0.45f, 0.82f),
        };
        for (var copy = 0; copy < copies; copy++)
        {
            var center = board.ShipPosition + offsets[copy];
            if (board.Thrusting)
            {
                Transform(flameLocal, center, board.ShipAngle, DriftBoard.ShipSize, in camera, flame);
                NeonStroke.Path(drawList, flame, false, FlameColor with { W = alpha }, width);
            }

            DrawShipShape(drawList, center, board.ShipAngle, DriftBoard.ShipSize, in camera, accent, width, alpha);
        }
    }

    private static void DrawSaucer(ImDrawListPtr drawList, DriftBoard board, in Camera2D camera, float width,
        float time, Span<Vector2> offsets)
    {
        var radius = board.SaucerRadius;
        Span<Vector2> hull = stackalloc Vector2[SaucerHull.Length];
        Span<Vector2> dome = stackalloc Vector2[SaucerDome.Length];
        var copies = WrapCopies(board.SaucerPosition, radius, false, offsets);
        for (var copy = 0; copy < copies; copy++)
        {
            var center = board.SaucerPosition + offsets[copy];
            Transform(SaucerHull, center, 0f, radius, in camera, hull);
            Transform(SaucerDome, center, 0f, radius, in camera, dome);
            NeonStroke.Fill(drawList, hull, SaucerColor, RockFillAlpha * 2f);
            NeonStroke.Path(drawList, hull, true, SaucerColor, width);
            NeonStroke.Path(drawList, dome, false, SaucerColor, width);
            NeonStroke.Line(drawList, camera.ToScreen(center + new Vector2(-radius, 0f)),
                camera.ToScreen(center + new Vector2(radius, 0f)), SaucerColor, width * 0.8f);
            var lit = (int)(time * 6f) % 3;
            for (var light = 0; light < 3; light++)
            {
                var spot = camera.ToScreen(center + new Vector2((light - 1) * radius * 0.45f, radius * 0.18f));
                var glow = light == lit ? 1f : 0.35f;
                NeonStroke.Dot(drawList, spot, MathF.Max(1f, camera.Px(radius * 0.08f)),
                    ShotColor with { W = glow });
            }
        }
    }

    private static void DrawShots(ImDrawListPtr drawList, DriftBoard board, in Camera2D camera, Vector4 accent,
        float scale)
    {
        var dotRadius = MathF.Max(1.2f * scale, camera.Px(0.45f));
        var tailWidth = MathF.Max(1f, scale);
        var shotGlow = Vector4.Lerp(ShotColor, accent, 0.35f);
        for (var index = 0; index < board.ShotCount; index++)
        {
            ref readonly var shot = ref board.ShotAt(index);
            var head = camera.ToScreen(shot.Position);
            var tail = camera.ToScreen(shot.Position - shot.Velocity * ShotTail);
            NeonStroke.Line(drawList, tail, head, shotGlow with { W = 0.6f }, tailWidth);
            NeonStroke.Dot(drawList, head, dotRadius, shotGlow);
        }

        for (var index = 0; index < board.SaucerShotCount; index++)
        {
            ref readonly var shot = ref board.SaucerShotAt(index);
            NeonStroke.Dot(drawList, camera.ToScreen(shot.Position), dotRadius, SaucerShotColor);
        }
    }

    private static void RockPoints(in Rock rock, Vector2 center, float radius, in Camera2D camera, Span<Vector2> points)
    {
        var offset = rock.Shape * RockVertices;
        for (var vertex = 0; vertex < RockVertices; vertex++)
        {
            var angle = rock.Angle + vertex * MathF.Tau / RockVertices;
            var reach = radius * RockJitter[offset + vertex];
            points[vertex] = camera.ToScreen(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach);
        }
    }

    private static void Transform(ReadOnlySpan<Vector2> local, Vector2 center, float angle, float size,
        in Camera2D camera, Span<Vector2> points)
    {
        var right = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var down = -DriftBoard.HeadingFor(angle);
        for (var index = 0; index < local.Length; index++)
        {
            var world = center + (right * local[index].X + down * local[index].Y) * size;
            points[index] = camera.ToScreen(world);
        }
    }

    private static int WrapCopies(Vector2 position, float radius, bool wrapX, Span<Vector2> offsets)
    {
        var count = 0;
        offsets[count++] = Vector2.Zero;
        var shiftX = 0f;
        if (wrapX)
        {
            shiftX = position.X - radius < 0f ? DriftBoard.Width : position.X + radius > DriftBoard.Width ? -DriftBoard.Width : 0f;
        }

        var shiftY = position.Y - radius < 0f ? DriftBoard.Height : position.Y + radius > DriftBoard.Height ? -DriftBoard.Height : 0f;
        if (shiftX != 0f)
        {
            offsets[count++] = new Vector2(shiftX, 0f);
        }

        if (shiftY != 0f)
        {
            offsets[count++] = new Vector2(0f, shiftY);
        }

        if (shiftX != 0f && shiftY != 0f)
        {
            offsets[count++] = new Vector2(shiftX, shiftY);
        }

        return count;
    }

    private static void Bracket(ImDrawListPtr drawList, Vector2 corner, Vector2 alongX, Vector2 alongY, Vector4 color,
        float width)
    {
        NeonStroke.Line(drawList, corner, corner + alongX, color, width);
        NeonStroke.Line(drawList, corner, corner + alongY, color, width);
    }

    private static float[] BuildJitter()
    {
        var random = GameRandom.FromSeed(ShapeSeed);
        var jitter = new float[DriftBoard.RockShapeCount * RockVertices];
        for (var index = 0; index < jitter.Length; index++)
        {
            jitter[index] = random.Range(0.74f, 1.06f);
        }

        return jitter;
    }
}
