using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Snip;

internal static class SnipRenderer
{
    public const float StarCapsuleWidth = 76f;
    public const float EatSeconds = 0.32f;
    private const int MaxRopePoints = PhysicsWorld.MaxRopeSegments + 1;
    private const float RopeWidth = 0.075f;
    private const float HeadRadius = 0.62f;
    private const float StarGlyph = 0.62f;
    private const float CapsuleStar = 13f;
    private const float SpikeSpacing = 0.22f;
    private const float SpikeLength = 0.2f;
    private const int EllipseSegments = 32;
    public static readonly Vector4 Fur = new(0.99f, 0.97f, 0.94f, 1f);
    public static readonly Vector4 BubbleTint = new(0.50f, 0.78f, 0.98f, 1f);
    public static readonly Vector4 PuffTint = new(0.92f, 0.96f, 1f, 0.9f);
    public static readonly Vector4 Steel = new(0.36f, 0.38f, 0.45f, 1f);
    private static readonly Vector4 RopeDark = new(0.34f, 0.24f, 0.16f, 1f);
    private static readonly Vector4 RopeLight = new(0.70f, 0.54f, 0.36f, 1f);
    private static readonly Vector4 Pin = new(0.26f, 0.28f, 0.34f, 1f);
    private static readonly Vector4 PinRing = new(0.62f, 0.65f, 0.72f, 1f);
    private static readonly Vector4 FurShade = new(0.86f, 0.82f, 0.80f, 1f);
    private static readonly Vector4 Outline = new(0.36f, 0.30f, 0.32f, 1f);
    private static readonly Vector4 Wing = new(0.56f, 0.40f, 0.74f, 1f);
    private static readonly Vector4 Pompom = new(0.93f, 0.27f, 0.33f, 1f);
    private static readonly Vector4 Nose = new(0.94f, 0.50f, 0.52f, 1f);
    private static readonly Vector4 Mouth = new(0.38f, 0.12f, 0.18f, 1f);
    private static readonly Vector4 Tongue = new(0.95f, 0.48f, 0.55f, 1f);
    private static readonly Vector4 Blush = new(0.98f, 0.62f, 0.66f, 0.55f);
    private static readonly Vector4 Cushion = new(0.42f, 0.62f, 0.70f, 1f);
    private static readonly Vector4 CushionDark = new(0.22f, 0.36f, 0.44f, 1f);
    private static readonly Vector4 SpikeTip = new(0.72f, 0.75f, 0.82f, 1f);
    private static readonly Vector4 StarRim = new(0.62f, 0.42f, 0.08f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0.10f, 0.08f, 0.12f, 1f);

    public static void DrawSpikes(ImDrawListPtr drawList, in Camera2D camera, SnipLevel level)
    {
        var spikes = level.Spikes;
        for (var index = 0; index < spikes.Length; index++)
        {
            DrawSpikeBar(drawList, camera, spikes[index]);
        }
    }

    public static void DrawCushions(ImDrawListPtr drawList, in Camera2D camera, SnipLevel level, SnipBoard board)
    {
        var cushions = level.Cushions;
        for (var index = 0; index < cushions.Length; index++)
        {
            DrawCushion(drawList, camera, cushions[index], board.PuffAge(index) / SnipBoard.PuffSeconds);
        }
    }

    public static void DrawFreeBubbles(ImDrawListPtr drawList, in Camera2D camera, SnipLevel level, SnipBoard board,
        float time)
    {
        var bubbles = level.Bubbles;
        for (var index = 0; index < bubbles.Length; index++)
        {
            if (board.Bubble(index) != BubbleState.Free)
            {
                continue;
            }

            var wobble = 1f + 0.04f * MathF.Sin(time * 3.1f + index * 1.7f);
            var drift = new Vector2(0f, 0.05f * MathF.Sin(time * 1.6f + index));
            DrawBubble(drawList, camera.ToScreen(bubbles[index] + drift), camera.Px(SnipBoard.BubbleRadius) * wobble, time);
        }
    }

    public static void DrawHeldBubble(ImDrawListPtr drawList, in Camera2D camera, Vector2 candy, float time)
    {
        var wobble = 1f + 0.05f * MathF.Sin(time * 4.2f);
        DrawBubble(drawList, camera.ToScreen(candy), camera.Px(SnipBoard.BubbleRadius * 0.9f) * wobble, time);
    }

    public static void DrawRopes(ImDrawListPtr drawList, in Camera2D camera, SnipBoard board)
    {
        var world = board.World;
        Span<Vector2> points = stackalloc Vector2[MaxRopePoints];
        var width = MathF.Max(1.5f, camera.Px(RopeWidth));
        for (var ropeIndex = 0; ropeIndex < board.RopeCount; ropeIndex++)
        {
            var rope = board.Rope(ropeIndex);
            if (!world.IsRopeAlive(rope))
            {
                continue;
            }

            var segments = world.RopeSegments(rope);
            for (var pass = 0; pass < 3; pass++)
            {
                var color = ImGui.GetColorU32(pass == 0 ? RopeDark : pass == 1 ? RopeLight : White with { W = 0.35f });
                var thickness = pass == 0 ? width * 1.7f : pass == 1 ? width : width * 0.35f;
                var offset = pass == 2 ? new Vector2(-width * 0.2f, -width * 0.2f) : Vector2.Zero;
                var count = 0;
                points[count++] = camera.ToScreen(world.RopePoint(rope, 0)) + offset;
                for (var segment = 0; segment < segments; segment++)
                {
                    if (world.IsRopeSegmentCut(rope, segment))
                    {
                        StrokeRun(drawList, points[..count], color, thickness);
                        count = 0;
                    }

                    points[count++] = camera.ToScreen(world.RopePoint(rope, segment + 1)) + offset;
                }

                StrokeRun(drawList, points[..count], color, thickness);
            }
        }

        for (var ropeIndex = 0; ropeIndex < board.RopeCount; ropeIndex++)
        {
            DrawPin(drawList, camera.ToScreen(board.Level.Ropes[ropeIndex].Anchor), camera.Px(SnipBoard.AnchorRadius),
                board.RopeHolds(ropeIndex));
        }
    }

    public static void DrawStars(ImDrawListPtr drawList, in Camera2D camera, SnipLevel level, SnipBoard board,
        float time)
    {
        var stars = level.Stars;
        for (var index = 0; index < stars.Length; index++)
        {
            if (board.StarTaken(index))
            {
                continue;
            }

            var bob = new Vector2(0f, 0.06f * MathF.Sin(time * 2.4f + index * 2.1f));
            var center = camera.ToScreen(stars[index] + bob);
            var size = camera.Px(StarGlyph) * (1f + 0.06f * MathF.Sin(time * 5f + index));
            ProgressRing.Glow(center, size * 0.9f, GamePalette.Star, 0.45f);
            ProgressRing.CenterIcon(drawList, center + new Vector2(0f, size * 0.06f), FontAwesomeIcon.Star, StarRim,
                size * 1.12f);
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Star, GamePalette.Star, size);
        }
    }

    public static void DrawCandy(ImDrawListPtr drawList, Vector2 center, float radius, float angle, Vector4 accent,
        float alpha)
    {
        if (alpha <= 0f || radius <= 0.5f)
        {
            return;
        }

        var light = GamePalette.Lighten(accent, 0.45f);
        var dark = GamePalette.Darken(accent, 0.25f);
        ProgressRing.Glow(center, radius * 2.2f, accent, 0.5f * alpha);
        Span<Vector2> rim = stackalloc Vector2[6];
        var axis = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var side = new Vector2(-axis.Y, axis.X);
        rim[0] = center - side * radius * 1.35f;
        rim[1] = center + axis * radius * 0.78f - side * radius * 0.42f;
        rim[2] = center + axis * radius * 0.62f + side * radius * 0.62f;
        rim[3] = center + side * radius * 1.3f;
        rim[4] = center - axis * radius * 0.62f + side * radius * 0.62f;
        rim[5] = center - axis * radius * 0.78f - side * radius * 0.42f;
        var core = center - side * radius * 0.15f;
        for (var facet = 0; facet < rim.Length; facet++)
        {
            var next = rim[(facet + 1) % rim.Length];
            var shade = facet switch
            {
                0 => light,
                1 => GamePalette.Lighten(accent, 0.2f),
                2 => accent,
                3 => dark,
                4 => GamePalette.Darken(accent, 0.1f),
                _ => GamePalette.Lighten(accent, 0.3f),
            };
            drawList.AddTriangleFilled(core, rim[facet], next, ImGui.GetColorU32(shade with { W = alpha }));
        }

        drawList.PathClear();
        for (var point = 0; point < rim.Length; point++)
        {
            drawList.PathLineTo(rim[point]);
        }

        drawList.PathStroke(ImGui.GetColorU32(GamePalette.Darken(accent, 0.45f) with { W = alpha }), ImDrawFlags.Closed,
            MathF.Max(1f, radius * 0.1f));
        drawList.AddCircleFilled(core - side * radius * 0.45f + axis * radius * 0.1f, radius * 0.16f,
            ImGui.GetColorU32(White with { W = 0.85f * alpha }), 10);
    }

    public static void DrawMoogle(ImDrawListPtr drawList, in Camera2D camera, Vector2 mouth, float mouthOpen,
        float chomp, bool blinking, float time)
    {
        var bob = 0.05f * MathF.Sin(time * 2.2f);
        var squash = chomp > 0f ? 1f + 0.18f * MathF.Sin(MathF.Min(1f, chomp) * MathF.PI) : 1f;
        var head = camera.ToScreen(mouth + new Vector2(0f, -0.26f + bob));
        var radius = camera.Px(HeadRadius);
        var body = head + new Vector2(0f, radius * 1.05f);
        var flap = MathF.Sin(time * 9f) * 0.35f;
        DrawWing(drawList, body + new Vector2(-radius * 0.55f, -radius * 0.1f), radius, -1f, flap);
        DrawWing(drawList, body + new Vector2(radius * 0.55f, -radius * 0.1f), radius, 1f, flap);
        drawList.AddCircleFilled(body + new Vector2(0f, radius * 0.08f), radius * 0.62f, ImGui.GetColorU32(Shadow with { W = 0.18f }), 32);
        drawList.AddCircleFilled(body, radius * 0.6f, ImGui.GetColorU32(FurShade), 32);
        drawList.AddCircle(body, radius * 0.6f, ImGui.GetColorU32(Outline), 32, MathF.Max(1f, radius * 0.06f));
        var antennaBase = head - new Vector2(0f, radius * 0.95f);
        var tip = antennaBase + new Vector2(radius * 0.18f * MathF.Sin(time * 2.6f), -radius * 0.62f);
        drawList.AddBezierQuadratic(antennaBase, antennaBase + new Vector2(-radius * 0.2f, -radius * 0.35f), tip,
            ImGui.GetColorU32(Outline), MathF.Max(1f, radius * 0.07f), 12);
        ProgressRing.Glow(tip, radius * 0.4f, Pompom, 0.35f);
        drawList.AddCircleFilled(tip, radius * 0.2f, ImGui.GetColorU32(Pompom), 20);
        drawList.AddCircleFilled(tip - new Vector2(radius * 0.06f, radius * 0.06f), radius * 0.07f,
            ImGui.GetColorU32(White with { W = 0.7f }), 10);
        var headHalf = new Vector2(radius * squash, radius / squash);
        FillEllipse(drawList, head + new Vector2(0f, radius * 0.06f), headHalf * 1.02f,
            ImGui.GetColorU32(Shadow with { W = 0.16f }));
        FillEllipse(drawList, head, headHalf, ImGui.GetColorU32(Fur));
        StrokeEllipse(drawList, head, headHalf, ImGui.GetColorU32(Outline), MathF.Max(1f, radius * 0.06f));
        DrawEar(drawList, head, radius, -1f);
        DrawEar(drawList, head, radius, 1f);
        DrawFace(drawList, head, radius, mouthOpen, chomp, blinking);
    }

    public static void DrawStarCapsule(ImDrawListPtr drawList, Rect rect, int earned, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        StarRow.Draw(drawList, rect.Center, CapsuleStar * scale, earned, new Vector4(0.5f, 0.5f, 0.55f, 0.45f), 1f);
    }

    public static Vector2 CapsuleStarCenter(Rect rect, int index, float scale)
    {
        var step = CapsuleStar * scale * 1.2f;
        return new Vector2(rect.Center.X + (index - 1) * step, rect.Center.Y);
    }

    private static void StrokeRun(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, uint color, float thickness)
    {
        if (points.Length < 2)
        {
            return;
        }

        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathStroke(color, ImDrawFlags.None, thickness);
    }

    private static void DrawPin(ImDrawListPtr drawList, Vector2 center, float radius, bool taut)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.3f), radius * 1.15f,
            ImGui.GetColorU32(Shadow with { W = 0.22f }), 16);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Pin), 16);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(taut ? PinRing : PinRing with { W = 0.45f }), 16,
            MathF.Max(1f, radius * 0.3f));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.28f,
            ImGui.GetColorU32(White with { W = 0.6f }), 8);
    }

    private static void DrawBubble(ImDrawListPtr drawList, Vector2 center, float radius, float time)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BubbleTint with { W = 0.22f }), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(BubbleTint with { W = 0.85f }), 40,
            MathF.Max(1f, radius * 0.06f));
        var sheen = time * 0.6f;
        drawList.PathArcTo(center, radius * 0.78f, MathF.PI * 1.08f + 0.1f * MathF.Sin(sheen),
            MathF.PI * 1.42f + 0.1f * MathF.Sin(sheen), 12);
        drawList.PathStroke(ImGui.GetColorU32(White with { W = 0.85f }), ImDrawFlags.None, MathF.Max(1f, radius * 0.1f));
        drawList.AddCircleFilled(center + new Vector2(radius * 0.42f, radius * 0.38f), radius * 0.08f,
            ImGui.GetColorU32(White with { W = 0.6f }), 8);
    }

    private static void DrawCushion(ImDrawListPtr drawList, in Camera2D camera, in SnipCushion cushion, float puff)
    {
        var center = camera.ToScreen(cushion.Position);
        var radius = camera.Px(SnipBoard.CushionRadius);
        var squeeze = puff < 1f ? 1f - 0.28f * MathF.Sin(puff * MathF.PI) : 1f;
        var forward = cushion.Direction;
        var side = new Vector2(-forward.Y, forward.X);
        Span<Vector2> body = stackalloc Vector2[4];
        var back = center - forward * radius * 0.7f * squeeze;
        var front = center + forward * radius * 0.35f;
        body[0] = back - side * radius;
        body[1] = front - side * radius * 0.8f;
        body[2] = front + side * radius * 0.8f;
        body[3] = back + side * radius;
        drawList.AddConvexPolyFilled(ref body[0], body.Length, ImGui.GetColorU32(Cushion));
        drawList.AddPolyline(ref body[0], body.Length, ImGui.GetColorU32(CushionDark), ImDrawFlags.Closed,
            MathF.Max(1f, radius * 0.1f));
        for (var stitch = -1; stitch <= 1; stitch++)
        {
            var at = Vector2.Lerp(back, front, 0.35f) + side * radius * 0.45f * stitch;
            drawList.AddLine(at - forward * radius * 0.2f, at + forward * radius * 0.2f, ImGui.GetColorU32(CushionDark),
                MathF.Max(1f, radius * 0.07f));
        }

        var nozzle = front + forward * radius * 0.45f;
        drawList.AddQuadFilled(front - side * radius * 0.32f, nozzle - side * radius * 0.18f, nozzle + side * radius * 0.18f,
            front + side * radius * 0.32f, ImGui.GetColorU32(CushionDark));
        if (puff < 1f)
        {
            ProgressRing.Glow(nozzle + forward * radius * 0.5f, radius * (0.6f + puff), PuffTint, 0.6f * (1f - puff));
        }
    }

    private static void DrawSpikeBar(ImDrawListPtr drawList, in Camera2D camera, in SnipSpike spike)
    {
        var from = camera.ToScreen(spike.From);
        var to = camera.ToScreen(spike.To);
        var span = to - from;
        var length = span.Length();
        if (length <= 0.5f)
        {
            return;
        }

        var along = span / length;
        var normal = new Vector2(-along.Y, along.X);
        var half = camera.Px(SnipBoard.SpikeHalfWidth * 0.55f);
        var tip = camera.Px(SpikeLength);
        var spacing = camera.Px(SpikeSpacing);
        var count = Math.Max(1, (int)(length / spacing));
        var step = length / count;
        var steel = ImGui.GetColorU32(Steel);
        var light = ImGui.GetColorU32(SpikeTip);
        for (var index = 0; index < count; index++)
        {
            var baseCenter = from + along * (step * (index + 0.5f));
            var left = baseCenter - along * step * 0.5f;
            var right = baseCenter + along * step * 0.5f;
            drawList.AddTriangleFilled(left, right, baseCenter + normal * (half + tip), steel);
            drawList.AddTriangleFilled(left, right, baseCenter - normal * (half + tip), steel);
            drawList.AddLine(baseCenter + normal * half, baseCenter + normal * (half + tip), light, 1f);
            drawList.AddLine(baseCenter - normal * half, baseCenter - normal * (half + tip), light, 1f);
        }

        drawList.AddLine(from, to, ImGui.GetColorU32(GamePalette.Darken(Steel, 0.3f)), half * 2f);
    }

    private static void DrawWing(ImDrawListPtr drawList, Vector2 root, float radius, float side, float flap)
    {
        var lift = flap * radius;
        var tip = root + new Vector2(side * radius * 0.95f, -radius * 0.55f - lift);
        var lower = root + new Vector2(side * radius * 0.75f, radius * 0.1f - lift * 0.4f);
        var notch = root + new Vector2(side * radius * 0.55f, -radius * 0.12f - lift * 0.6f);
        var wing = ImGui.GetColorU32(Wing);
        drawList.AddTriangleFilled(root, tip, notch, wing);
        drawList.AddTriangleFilled(root, notch, lower, wing);
        drawList.AddLine(root, tip, ImGui.GetColorU32(GamePalette.Darken(Wing, 0.35f)), MathF.Max(1f, radius * 0.05f));
    }

    private static void DrawEar(ImDrawListPtr drawList, Vector2 head, float radius, float side)
    {
        var baseLeft = head + new Vector2(side * radius * 0.35f, -radius * 0.78f);
        var baseRight = head + new Vector2(side * radius * 0.78f, -radius * 0.42f);
        var tip = head + new Vector2(side * radius * 0.82f, -radius * 0.98f);
        drawList.AddTriangleFilled(baseLeft, baseRight, tip, ImGui.GetColorU32(Fur));
        drawList.AddLine(baseLeft, tip, ImGui.GetColorU32(Outline), MathF.Max(1f, radius * 0.05f));
        drawList.AddLine(tip, baseRight, ImGui.GetColorU32(Outline), MathF.Max(1f, radius * 0.05f));
    }

    private static void DrawFace(ImDrawListPtr drawList, Vector2 head, float radius, float mouthOpen, float chomp,
        bool blinking)
    {
        var ink = ImGui.GetColorU32(Outline);
        var eyeY = head.Y - radius * 0.1f;
        var eyeOffset = radius * 0.34f;
        var happy = chomp > 0f;
        for (var side = -1; side <= 1; side += 2)
        {
            var eye = new Vector2(head.X + side * eyeOffset, eyeY);
            if (happy || blinking)
            {
                drawList.PathArcTo(eye + new Vector2(0f, happy ? radius * 0.06f : 0f), radius * 0.1f,
                    happy ? MathF.PI * 1.1f : MathF.PI * 0.1f, happy ? MathF.PI * 1.9f : MathF.PI * 0.9f, 8);
                drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1f, radius * 0.06f));
                continue;
            }

            var wide = 0.075f + 0.035f * mouthOpen;
            drawList.AddCircleFilled(eye, radius * wide, ink, 12);
            drawList.AddCircleFilled(eye - new Vector2(radius * 0.025f, radius * 0.03f), radius * wide * 0.4f,
                ImGui.GetColorU32(White), 8);
        }

        var cheek = ImGui.GetColorU32(Blush);
        drawList.AddCircleFilled(new Vector2(head.X - radius * 0.55f, head.Y + radius * 0.15f), radius * 0.13f, cheek, 12);
        drawList.AddCircleFilled(new Vector2(head.X + radius * 0.55f, head.Y + radius * 0.15f), radius * 0.13f, cheek, 12);
        var nose = new Vector2(head.X, head.Y + radius * 0.08f);
        FillEllipse(drawList, nose, new Vector2(radius * 0.11f, radius * 0.08f), ImGui.GetColorU32(Nose));
        var mouthCenter = new Vector2(head.X, head.Y + radius * 0.4f);
        if (happy)
        {
            var close = MathF.Min(1f, chomp * 3f);
            var gape = radius * 0.24f * (1f - close);
            if (gape > 1f)
            {
                FillEllipse(drawList, mouthCenter, new Vector2(radius * 0.26f, gape), ImGui.GetColorU32(Mouth));
            }

            drawList.PathArcTo(mouthCenter - new Vector2(0f, radius * 0.08f), radius * 0.18f, MathF.PI * 0.15f,
                MathF.PI * 0.85f, 10);
            drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1f, radius * 0.06f));
            return;
        }

        if (mouthOpen <= 0.05f)
        {
            drawList.PathArcTo(mouthCenter - new Vector2(0f, radius * 0.1f), radius * 0.12f, MathF.PI * 0.2f,
                MathF.PI * 0.8f, 8);
            drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1f, radius * 0.05f));
            return;
        }

        var half = new Vector2(radius * (0.16f + 0.12f * mouthOpen), radius * (0.05f + 0.24f * mouthOpen));
        FillEllipse(drawList, mouthCenter, half, ImGui.GetColorU32(Mouth));
        FillEllipse(drawList, mouthCenter + new Vector2(0f, half.Y * 0.45f), half * new Vector2(0.6f, 0.45f),
            ImGui.GetColorU32(Tongue));
        StrokeEllipse(drawList, mouthCenter, half, ink, MathF.Max(1f, radius * 0.05f));
    }

    private static void EllipsePath(ImDrawListPtr drawList, Vector2 center, Vector2 half)
    {
        drawList.PathClear();
        for (var segment = 0; segment < EllipseSegments; segment++)
        {
            var angle = segment * MathF.Tau / EllipseSegments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * half.X, MathF.Sin(angle) * half.Y));
        }
    }

    private static void FillEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 half, uint color)
    {
        EllipsePath(drawList, center, half);
        drawList.PathFillConvex(color);
    }

    private static void StrokeEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 half, uint color, float thickness)
    {
        EllipsePath(drawList, center, half);
        drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
    }
}
