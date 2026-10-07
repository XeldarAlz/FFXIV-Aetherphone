using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Herd;

internal static class HerdArt
{
    public static readonly Vector4 Fur = new(0.98f, 0.97f, 0.95f, 1f);
    public static readonly Vector4 FurShade = new(0.84f, 0.82f, 0.88f, 1f);
    public static readonly Vector4 Wing = new(0.56f, 0.38f, 0.74f, 1f);
    public static readonly Vector4 PomPom = new(0.98f, 0.33f, 0.45f, 1f);
    public static readonly Vector4 Nose = new(0.96f, 0.56f, 0.62f, 1f);
    public static readonly Vector4 Eye = new(0.15f, 0.12f, 0.18f, 1f);
    public static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);
    private const int EllipseSegments = 18;
    private const float Tau = MathF.PI * 2f;

    public readonly struct Pose
    {
        public readonly float Bob;
        public readonly float Lean;
        public readonly float LeftFoot;
        public readonly float RightFoot;
        public readonly float PawReach;
        public readonly float PawLift;
        public readonly float WingSpread;
        public readonly float WingFlap;
        public readonly float Squash;
        public readonly float Canopy;
        public readonly float Swell;
        public readonly bool ArmsOut;
        public readonly bool Happy;

        public Pose(float bob, float lean, float leftFoot, float rightFoot, float pawReach, float pawLift, float wingSpread,
            float wingFlap, float squash, float canopy, float swell, bool armsOut, bool happy)
        {
            Bob = bob;
            Lean = lean;
            LeftFoot = leftFoot;
            RightFoot = rightFoot;
            PawReach = pawReach;
            PawLift = pawLift;
            WingSpread = wingSpread;
            WingFlap = wingFlap;
            Squash = squash;
            Canopy = canopy;
            Swell = swell;
            ArmsOut = armsOut;
            Happy = happy;
        }
    }

    public static Pose PoseFor(in HerdMoogle moogle, float time)
    {
        var phase = time * 9f + moogle.Variant * 0.07f;
        var step = MathF.Sin(phase);
        var flutter = MathF.Sin(time * 22f + moogle.Variant);
        var ticks = moogle.ActionTicks;
        return moogle.Action switch
        {
            HerdAction.Walking => new Pose(MathF.Abs(step) * 0.05f, 0f, step * 0.06f, -step * 0.06f, 0.05f, 0f, 0.4f,
                flutter * 0.2f, 0f, 0f, 0f, false, false),
            HerdAction.Falling => new Pose(0f, 0f, 0.02f, -0.02f, 0.12f, 0.3f, 0.9f, flutter, 0f, 0f, 0f, false, false),
            HerdAction.Floating => new Pose(0f, MathF.Sin(time * 2.4f) * 0.08f, 0f, 0f, 0.08f, 0.32f, 0.6f,
                flutter * 0.4f, 0f, 1f, 0f, false, false),
            HerdAction.Blocking => new Pose(MathF.Abs(MathF.Sin(time * 3f)) * 0.02f, 0f, -0.08f, 0.08f, 0.32f, 0.02f, 1f,
                flutter * 0.15f, 0f, 0f, 0f, true, false),
            HerdAction.Digging => new Pose(0.08f + MathF.Abs(step) * 0.06f, 0.35f, 0f, 0f, 0.18f,
                -0.18f - MathF.Abs(MathF.Sin(phase * 1.3f)) * 0.12f, 0.5f, flutter * 0.3f, 0.06f, 0f, 0f, false, false),
            HerdAction.Bashing => new Pose(0.02f, 0.15f, step * 0.03f, -step * 0.03f,
                0.14f + MathF.Max(0f, MathF.Sin(phase * 1.6f)) * 0.2f, 0.06f, 0.6f, flutter * 0.3f, 0f, 0f, 0f, false,
                false),
            HerdAction.Building => new Pose(MathF.Abs(MathF.Sin(phase * 0.5f)) * 0.04f, 0.12f, step * 0.03f,
                -step * 0.03f, 0.22f, -0.04f + MathF.Sin(phase * 0.8f) * 0.05f, 0.5f, flutter * 0.2f, 0f, 0f, 0f, false,
                false),
            HerdAction.Climbing => new Pose(0f, -0.25f, MathF.Sin(phase * 0.7f) * 0.05f, -MathF.Sin(phase * 0.7f) * 0.05f,
                0.22f, 0.18f + MathF.Sin(phase * 0.7f) * 0.1f, 0.5f, flutter * 0.4f, 0f, 0f, 0f, false, false),
            HerdAction.Exiting => new Pose(MathF.Sin(MathF.Min(1f, ticks / 6f) * MathF.PI) * 0.35f, 0f, 0f, 0f, 0.14f,
                0.32f, 1f, flutter, 0f, 0f, 0f, false, true),
            HerdAction.Splatting => new Pose(0f, 0f, 0f, 0f, 0.2f, -0.02f, 1f, 0f, Math.Clamp(ticks / 3f, 0f, 1f) * 0.6f, 0f,
                0f, false, false),
            HerdAction.Drowning => new Pose(-MathF.Min(1f, ticks / 8f) * 0.6f, 0f, 0f, 0f, 0.14f, 0.34f, 1f, flutter, 0f, 0f,
                0f, false, false),
            HerdAction.Popping => new Pose(0f, 0f, 0f, 0f, 0.16f, 0.2f, 0.8f, flutter, 0f, 0f,
                0.12f + 0.1f * MathF.Abs(MathF.Sin(time * 14f)), false, false),
            _ => new Pose(0f, 0f, 0f, 0f, 0.05f, 0f, 0.4f, 0f, 0f, 0f, 0f, false, false),
        };
    }

    public static void DrawMoogle(ImDrawListPtr drawList, Vector2 feet, float unit, int direction, in Pose pose,
        float alpha, Vector4 pomPom)
    {
        if (alpha <= 0.01f || unit <= 0f)
        {
            return;
        }

        var facing = direction < 0 ? -1f : 1f;
        var squash = pose.Squash;
        var grow = 1f + pose.Swell;
        var scaleX = unit * grow * (1f + squash * 0.8f);
        var scaleY = unit * grow * (1f - squash);
        var root = feet + new Vector2(0f, -pose.Bob * unit);
        var lean = pose.Lean * facing;
        var fur = ImGui.GetColorU32(Fur with { W = alpha });
        var shade = ImGui.GetColorU32(FurShade with { W = alpha });
        var wing = ImGui.GetColorU32(Wing with { W = alpha });
        var bodyCenter = Local(root, scaleX, scaleY, lean, 0f, -0.24f);
        var headCenter = Local(root, scaleX, scaleY, lean, 0.03f * facing, -0.6f);
        if (pose.Canopy > 0f)
        {
            DrawCanopy(drawList, headCenter, unit, pose, alpha, pomPom);
        }

        DrawWings(drawList, bodyCenter, scaleX, scaleY, facing, pose, wing);
        FillEllipse(drawList, Local(root, scaleX, scaleY, lean, -0.11f + pose.LeftFoot, -0.04f), 0.09f * scaleX,
            0.05f * scaleY, shade);
        FillEllipse(drawList, Local(root, scaleX, scaleY, lean, 0.11f + pose.RightFoot, -0.04f), 0.09f * scaleX,
            0.05f * scaleY, shade);
        FillEllipse(drawList, bodyCenter + new Vector2(0f, 0.02f * scaleY), 0.25f * scaleX, 0.22f * scaleY, shade);
        FillEllipse(drawList, bodyCenter, 0.24f * scaleX, 0.21f * scaleY, fur);
        DrawPaws(drawList, bodyCenter, scaleX, scaleY, facing, pose, fur, shade);
        if (pose.Canopy <= 0f)
        {
            DrawAntenna(drawList, headCenter, scaleX, scaleY, facing, pose, alpha, pomPom);
        }

        FillEllipse(drawList, headCenter + new Vector2(0f, 0.02f * scaleY), 0.29f * scaleX, 0.27f * scaleY, shade);
        FillEllipse(drawList, headCenter, 0.28f * scaleX, 0.26f * scaleY, fur);
        DrawFace(drawList, headCenter, scaleX, scaleY, facing, pose, alpha);
    }

    private static Vector2 Local(Vector2 root, float scaleX, float scaleY, float lean, float x, float y)
    {
        var height = -y;
        return root + new Vector2((x + lean * height) * scaleX, y * scaleY);
    }

    private static void DrawWings(ImDrawListPtr drawList, Vector2 body, float scaleX, float scaleY, float facing,
        in Pose pose, uint color)
    {
        var spread = 0.18f + pose.WingSpread * 0.16f;
        var flap = pose.WingFlap * 0.1f;
        for (var side = -1; side <= 1; side += 2)
        {
            var back = side == -1 ? -facing : facing * 0.4f;
            var anchor = body + new Vector2(back * 0.12f * scaleX, -0.1f * scaleY);
            var tip = anchor + new Vector2(back * spread * scaleX, (-0.16f - flap) * scaleY);
            var lower = anchor + new Vector2(back * spread * 0.8f * scaleX, (0.04f - flap * 0.5f) * scaleY);
            var notch = anchor + new Vector2(back * spread * 0.55f * scaleX, -0.03f * scaleY);
            drawList.AddTriangleFilled(anchor, tip, notch, color);
            drawList.AddTriangleFilled(anchor, notch, lower, color);
        }
    }

    private static void DrawPaws(ImDrawListPtr drawList, Vector2 body, float scaleX, float scaleY, float facing,
        in Pose pose, uint fur, uint shade)
    {
        var radius = 0.07f * MathF.Min(scaleX, scaleY);
        if (pose.ArmsOut)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var paw = body + new Vector2(side * (0.24f + pose.PawReach) * scaleX, -0.06f * scaleY);
                drawList.AddLine(body + new Vector2(side * 0.18f * scaleX, -0.05f * scaleY), paw, shade,
                    MathF.Max(1f, radius * 1.2f));
                drawList.AddCircleFilled(paw, radius * 1.15f, fur, 10);
            }

            return;
        }

        var front = body + new Vector2(facing * (0.16f + pose.PawReach) * scaleX, -pose.PawLift * scaleY);
        var rear = body + new Vector2(-facing * 0.17f * scaleX, (0.02f - pose.PawLift * 0.6f) * scaleY);
        drawList.AddCircleFilled(rear, radius, shade, 10);
        drawList.AddCircleFilled(front, radius, shade, 10);
        drawList.AddCircleFilled(front - new Vector2(0f, radius * 0.2f), radius * 0.85f, fur, 10);
    }

    private static void DrawAntenna(ImDrawListPtr drawList, Vector2 head, float scaleX, float scaleY, float facing,
        in Pose pose, float alpha, Vector4 pomPom)
    {
        var stalkColor = ImGui.GetColorU32(new Vector4(0.42f, 0.3f, 0.3f, alpha));
        var start = head + new Vector2(0.02f * facing * scaleX, -0.24f * scaleY);
        var sway = (pose.WingFlap * 0.05f - pose.Lean * 0.2f) * facing;
        var trail = pose.PawLift > 0.25f ? 0.12f : 0f;
        var tip = start + new Vector2((-0.08f * facing + sway) * scaleX, (-0.22f - trail) * scaleY);
        var control = start + new Vector2(0.06f * facing * scaleX, -0.12f * scaleY);
        drawList.AddBezierQuadratic(start, control, tip, stalkColor, MathF.Max(1f, 0.025f * scaleX));
        var radius = 0.09f * MathF.Min(scaleX, scaleY);
        drawList.AddCircleFilled(tip, radius * 1.6f, ImGui.GetColorU32(pomPom with { W = 0.25f * alpha }), 14);
        drawList.AddCircleFilled(tip, radius, ImGui.GetColorU32(pomPom with { W = alpha }), 14);
        drawList.AddCircleFilled(tip - new Vector2(radius * 0.3f, radius * 0.35f), radius * 0.35f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.55f * alpha)), 8);
    }

    private static void DrawCanopy(ImDrawListPtr drawList, Vector2 head, float unit, in Pose pose, float alpha,
        Vector4 pomPom)
    {
        var tilt = pose.Lean;
        var top = head + new Vector2(tilt * unit, -0.62f * unit);
        var radius = 0.46f * unit * pose.Canopy;
        drawList.PathClear();
        drawList.PathArcTo(top, radius, MathF.PI + tilt, Tau + tilt, 16);
        drawList.PathFillConvex(ImGui.GetColorU32(pomPom with { W = alpha }));
        var rib = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f * alpha));
        drawList.AddLine(top, top + new Vector2(MathF.Cos(MathF.PI * 1.25f + tilt), MathF.Sin(MathF.PI * 1.25f + tilt)) * radius,
            rib, MathF.Max(1f, unit * 0.02f));
        drawList.AddLine(top, top + new Vector2(MathF.Cos(MathF.PI * 1.75f + tilt), MathF.Sin(MathF.PI * 1.75f + tilt)) * radius,
            rib, MathF.Max(1f, unit * 0.02f));
        var line = ImGui.GetColorU32(new Vector4(0.9f, 0.9f, 0.95f, 0.7f * alpha));
        var left = top + new Vector2(MathF.Cos(MathF.PI + tilt), MathF.Sin(MathF.PI + tilt)) * radius;
        var right = top + new Vector2(MathF.Cos(tilt), MathF.Sin(tilt)) * radius;
        var hold = head + new Vector2(0f, -0.2f * unit);
        drawList.AddLine(left, hold, line, 1f);
        drawList.AddLine(right, hold, line, 1f);
    }

    private static void DrawFace(ImDrawListPtr drawList, Vector2 head, float scaleX, float scaleY, float facing,
        in Pose pose, float alpha)
    {
        var eye = ImGui.GetColorU32(Eye with { W = alpha });
        var eyeRadius = MathF.Max(1f, 0.04f * MathF.Min(scaleX, scaleY));
        var look = facing * 0.05f * scaleX;
        var first = head + new Vector2(look - 0.09f * scaleX, -0.02f * scaleY);
        var second = head + new Vector2(look + 0.09f * scaleX, -0.02f * scaleY);
        if (pose.Happy || pose.Squash > 0f)
        {
            var thickness = MathF.Max(1f, eyeRadius * 0.8f);
            drawList.AddLine(first - new Vector2(eyeRadius, -eyeRadius * 0.5f), first, eye, thickness);
            drawList.AddLine(first, first + new Vector2(eyeRadius, eyeRadius * 0.5f), eye, thickness);
            drawList.AddLine(second - new Vector2(eyeRadius, -eyeRadius * 0.5f), second, eye, thickness);
            drawList.AddLine(second, second + new Vector2(eyeRadius, eyeRadius * 0.5f), eye, thickness);
        }
        else
        {
            drawList.AddCircleFilled(first, eyeRadius, eye, 8);
            drawList.AddCircleFilled(second, eyeRadius, eye, 8);
        }

        if (pose.ArmsOut)
        {
            var brow = MathF.Max(1f, eyeRadius * 0.7f);
            drawList.AddLine(first + new Vector2(-eyeRadius * 1.3f, -eyeRadius * 2.4f),
                first + new Vector2(eyeRadius * 1.1f, -eyeRadius * 1.6f), eye, brow);
            drawList.AddLine(second + new Vector2(eyeRadius * 1.3f, -eyeRadius * 2.4f),
                second + new Vector2(-eyeRadius * 1.1f, -eyeRadius * 1.6f), eye, brow);
        }

        drawList.AddCircleFilled(head + new Vector2(look * 1.1f, 0.08f * scaleY),
            MathF.Max(1f, 0.05f * MathF.Min(scaleX, scaleY)), ImGui.GetColorU32(Nose with { W = alpha }), 10);
        var blush = ImGui.GetColorU32(Nose with { W = 0.35f * alpha });
        drawList.AddCircleFilled(first + new Vector2(-0.04f * scaleX, 0.08f * scaleY), eyeRadius * 1.3f, blush, 8);
        drawList.AddCircleFilled(second + new Vector2(0.04f * scaleX, 0.08f * scaleY), eyeRadius * 1.3f, blush, 8);
    }

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, uint color)
    {
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return;
        }

        drawList.PathClear();
        for (var segment = 0; segment < EllipseSegments; segment++)
        {
            var angle = segment * Tau / EllipseSegments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY));
        }

        drawList.PathFillConvex(color);
    }

    public static void DrawSkillIcon(ImDrawListPtr drawList, HerdSkill skill, Vector2 center, float size, uint ink)
    {
        var half = size * 0.5f;
        var thickness = MathF.Max(1.5f, size * 0.11f);
        switch (skill)
        {
            case HerdSkill.Block:
                drawList.AddCircleFilled(center + new Vector2(0f, -half * 0.55f), half * 0.28f, ink, 14);
                drawList.AddLine(center + new Vector2(-half * 0.85f, -half * 0.05f), center + new Vector2(half * 0.85f, -half * 0.05f),
                    ink, thickness);
                drawList.AddLine(center + new Vector2(0f, -half * 0.3f), center + new Vector2(0f, half * 0.35f), ink, thickness);
                drawList.AddLine(center + new Vector2(0f, half * 0.3f), center + new Vector2(-half * 0.4f, half * 0.85f), ink,
                    thickness);
                drawList.AddLine(center + new Vector2(0f, half * 0.3f), center + new Vector2(half * 0.4f, half * 0.85f), ink,
                    thickness);
                break;
            case HerdSkill.Dig:
                drawList.AddLine(center + new Vector2(-half * 0.9f, half * 0.1f), center + new Vector2(-half * 0.3f, half * 0.1f),
                    ink, thickness);
                drawList.AddLine(center + new Vector2(half * 0.3f, half * 0.1f), center + new Vector2(half * 0.9f, half * 0.1f),
                    ink, thickness);
                drawList.AddLine(center + new Vector2(0f, -half * 0.9f), center + new Vector2(0f, half * 0.45f), ink, thickness);
                drawList.AddTriangleFilled(center + new Vector2(-half * 0.38f, half * 0.3f),
                    center + new Vector2(half * 0.38f, half * 0.3f), center + new Vector2(0f, half * 0.9f), ink);
                break;
            case HerdSkill.Bridge:
                for (var stepIndex = 0; stepIndex < 3; stepIndex++)
                {
                    var left = -half * 0.9f + stepIndex * half * 0.6f;
                    var top = half * 0.55f - stepIndex * half * 0.5f;
                    drawList.AddRectFilled(center + new Vector2(left, top), center + new Vector2(left + half * 0.62f, top + half * 0.26f),
                        ink, half * 0.06f);
                }

                break;
            case HerdSkill.Climb:
                drawList.AddRectFilled(center + new Vector2(half * 0.35f, -half * 0.9f), center + new Vector2(half * 0.75f, half * 0.9f),
                    ink, half * 0.08f);
                drawList.AddLine(center + new Vector2(-half * 0.3f, half * 0.8f), center + new Vector2(-half * 0.3f, -half * 0.45f),
                    ink, thickness);
                drawList.AddTriangleFilled(center + new Vector2(-half * 0.7f, -half * 0.3f),
                    center + new Vector2(half * 0.1f, -half * 0.3f), center + new Vector2(-half * 0.3f, -half * 0.9f), ink);
                break;
            case HerdSkill.Float:
                drawList.PathClear();
                drawList.PathArcTo(center + new Vector2(0f, -half * 0.15f), half * 0.85f, MathF.PI, Tau, 16);
                drawList.PathFillConvex(ink);
                drawList.AddLine(center + new Vector2(0f, -half * 0.15f), center + new Vector2(0f, half * 0.7f), ink, thickness * 0.8f);
                drawList.AddBezierQuadratic(center + new Vector2(0f, half * 0.7f), center + new Vector2(0f, half * 0.95f),
                    center + new Vector2(-half * 0.3f, half * 0.75f), ink, thickness * 0.8f);
                break;
            default:
                drawList.AddRectFilled(center + new Vector2(half * 0.45f, -half * 0.9f), center + new Vector2(half * 0.85f, half * 0.9f),
                    ink, half * 0.08f);
                drawList.AddCircleFilled(center + new Vector2(-half * 0.15f, 0f), half * 0.42f, ink, 16);
                drawList.AddLine(center + new Vector2(-half * 0.95f, -half * 0.45f), center + new Vector2(-half * 0.6f, -half * 0.3f),
                    ink, thickness * 0.7f);
                drawList.AddLine(center + new Vector2(-half * 0.95f, half * 0.45f), center + new Vector2(-half * 0.6f, half * 0.3f),
                    ink, thickness * 0.7f);
                break;
        }
    }

    public static void DrawPause(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        var half = size * 0.5f;
        drawList.AddRectFilled(center + new Vector2(-half * 0.6f, -half * 0.7f), center + new Vector2(-half * 0.15f, half * 0.7f),
            ink, half * 0.1f);
        drawList.AddRectFilled(center + new Vector2(half * 0.15f, -half * 0.7f), center + new Vector2(half * 0.6f, half * 0.7f),
            ink, half * 0.1f);
    }

    public static void DrawPlay(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        var half = size * 0.5f;
        drawList.AddTriangleFilled(center + new Vector2(-half * 0.5f, -half * 0.75f), center + new Vector2(half * 0.75f, 0f),
            center + new Vector2(-half * 0.5f, half * 0.75f), ink);
    }

    public static void DrawFast(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        var half = size * 0.5f;
        for (var arrow = 0; arrow < 2; arrow++)
        {
            var left = -half * 0.85f + arrow * half * 0.8f;
            drawList.AddTriangleFilled(center + new Vector2(left, -half * 0.6f), center + new Vector2(left + half * 0.8f, 0f),
                center + new Vector2(left, half * 0.6f), ink);
        }
    }

    public static void DrawBomb(ImDrawListPtr drawList, Vector2 center, float size, uint ink, uint spark)
    {
        var half = size * 0.5f;
        var body = center + new Vector2(-half * 0.1f, half * 0.15f);
        drawList.AddCircleFilled(body, half * 0.62f, ink, 18);
        drawList.AddBezierQuadratic(body + new Vector2(half * 0.35f, -half * 0.45f), center + new Vector2(half * 0.55f, -half * 0.9f),
            center + new Vector2(half * 0.85f, -half * 0.6f), ink, MathF.Max(1.5f, size * 0.08f));
        drawList.AddCircleFilled(center + new Vector2(half * 0.88f, -half * 0.62f), half * 0.18f, spark, 10);
    }
}
