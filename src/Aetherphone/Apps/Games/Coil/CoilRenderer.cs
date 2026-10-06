using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Coil;

internal static class CoilRenderer
{
    private const int GrooveStride = 2;
    private const float GuideSpacing = 3.2f;
    private const float GuideFlowSpeed = 0.9f;
    private const int VortexArms = 4;
    private const int VortexArmPoints = 12;
    private const int AimDots = 9;
    private const int TrailLength = 4;

    private static readonly Vector4 GrooveBody = new(0.050f, 0.055f, 0.075f, 1f);
    private static readonly Vector4 GrooveDeep = new(0.020f, 0.022f, 0.032f, 1f);
    private static readonly Vector4 GrooveShadow = new(0f, 0f, 0f, 0.38f);
    private static readonly Vector4 GrooveRidge = new(1f, 1f, 1f, 0.045f);
    private static readonly Vector4 DangerRed = new(0.98f, 0.26f, 0.28f, 1f);
    private static readonly Vector4 LauncherMetal = new(0.11f, 0.12f, 0.16f, 1f);
    private static readonly Vector4 PortalShadow = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 VortexWell = new(0f, 0f, 0f, 0.7f);
    private static readonly Vector4 VortexEye = new(0.01f, 0.01f, 0.02f, 1f);
    private static readonly Vector4 LauncherShadow = new(0f, 0f, 0f, 0.32f);
    private static readonly Vector4 LauncherSheen = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 EyeInk = new(0.06f, 0.06f, 0.09f, 1f);
    private static readonly Vector4 EyeShine = new(1f, 1f, 1f, 0.9f);
    private static readonly Vector4 MouthWell = new(0.02f, 0.02f, 0.04f, 0.85f);

    public static Vector4 Danger => DangerRed;

    public static Rect FieldRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(CoilBoard.FieldWidth, CoilBoard.FieldHeight)));

    public static void DrawTrack(ImDrawListPtr drawList, CoilBoard board, in Camera2D camera, Vector4 accent,
        float scale, float time)
    {
        var radius = camera.Px(CoilBoard.MarbleRadius);
        var track = board.Track;
        StrokeGroove(drawList, track, in camera, new Vector2(0f, 2.2f * scale), ImGui.GetColorU32(GrooveShadow),
            radius * 2.9f);
        StrokeGroove(drawList, track, in camera, new Vector2(0f, -0.9f * scale),
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.55f) with { W = 0.16f }), radius * 2.75f);
        StrokeGroove(drawList, track, in camera, Vector2.Zero, ImGui.GetColorU32(GrooveBody), radius * 2.5f);
        StrokeGroove(drawList, track, in camera, new Vector2(0f, radius * 0.18f), ImGui.GetColorU32(GrooveDeep),
            radius * 1.75f);
        StrokeGroove(drawList, track, in camera, new Vector2(0f, radius * 0.62f), ImGui.GetColorU32(GrooveRidge),
            radius * 0.5f);
        DrawGuide(drawList, track, in camera, radius, accent, time);
        DrawPortal(drawList, camera.ToScreen(track.Start), radius, accent, time);
    }

    private static void StrokeGroove(ImDrawListPtr drawList, CoilTrack track, in Camera2D camera, Vector2 offset,
        uint color, float thickness)
    {
        var samples = track.Samples;
        for (var index = 0; index < samples.Length; index += GrooveStride)
        {
            drawList.PathLineTo(camera.ToScreen(samples[index]) + offset);
        }

        if ((samples.Length - 1) % GrooveStride != 0)
        {
            drawList.PathLineTo(camera.ToScreen(samples[^1]) + offset);
        }

        drawList.PathStroke(color, ImDrawFlags.None, thickness);
    }

    private static void DrawGuide(ImDrawListPtr drawList, CoilTrack track, in Camera2D camera, float radius,
        Vector4 accent, float time)
    {
        var spacing = CoilBoard.MarbleRadius * GuideSpacing;
        var flow = time * GuideFlowSpeed * CoilBoard.MarbleRadius % spacing;
        var tint = GamePalette.Lighten(accent, 0.45f);
        var size = radius * 0.32f;
        for (var arc = flow; arc < track.Length; arc += spacing)
        {
            var center = camera.ToScreen(track.PositionAt(arc));
            var along = track.TangentAt(arc);
            var across = new Vector2(-along.Y, along.X);
            var fade = 0.08f + 0.12f * (arc / track.Length);
            var color = ImGui.GetColorU32(tint with { W = fade });
            drawList.AddTriangleFilled(center + along * size, center - along * size * 0.6f + across * size * 0.8f,
                center - along * size * 0.6f - across * size * 0.8f, color);
        }
    }

    private static void DrawPortal(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float time)
    {
        drawList.AddCircleFilled(center, radius * 1.45f, ImGui.GetColorU32(PortalShadow));
        drawList.AddCircleFilled(center, radius * 1.1f, ImGui.GetColorU32(GamePalette.Darken(accent, 0.75f)));
        var spin = time * 1.6f;
        for (var dash = 0; dash < 6; dash++)
        {
            var start = spin + dash * MathF.Tau / 6f;
            drawList.PathArcTo(center, radius * 1.32f, start, start + 0.55f, 6);
            drawList.PathStroke(ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f) with { W = 0.55f }),
                ImDrawFlags.None, MathF.Max(1f, radius * 0.16f));
        }
    }

    public static void DrawVortex(ImDrawListPtr drawList, CoilBoard board, in Camera2D camera, Vector4 accent,
        float time)
    {
        var radius = camera.Px(CoilBoard.MarbleRadius);
        var center = camera.ToScreen(board.Track.End);
        var lead = board.LeadProgress;
        var draining = board.State == CoilState.Draining;
        var heat = draining ? 1f : MathF.Max(lead * lead, board.Danger);
        var pulse = 0.5f + 0.5f * MathF.Sin(time * (3f + heat * 9f));
        var tint = Vector4.Lerp(accent, DangerRed, Math.Clamp(heat * 1.4f - 0.3f, 0f, 1f));
        ProgressRing.Glow(center, radius * (3.2f + heat * 2.2f + pulse * heat), tint, 0.5f + heat * 1.6f);
        drawList.AddCircleFilled(center, radius * 1.9f, ImGui.GetColorU32(VortexWell));
        var spin = time * (1.4f + heat * 5f);
        for (var arm = 0; arm < VortexArms; arm++)
        {
            var baseAngle = spin + arm * MathF.Tau / VortexArms;
            for (var point = 0; point < VortexArmPoints; point++)
            {
                var progress = point / (float)(VortexArmPoints - 1);
                var angle = baseAngle + progress * 2.4f;
                drawList.PathLineTo(center + CoilShapes.Polar(angle) * radius * (1.85f - progress * 1.6f));
            }

            drawList.PathStroke(ImGui.GetColorU32(GamePalette.Lighten(tint, 0.35f) with { W = 0.45f + heat * 0.4f }),
                ImDrawFlags.None, MathF.Max(1f, radius * 0.22f));
        }

        drawList.AddCircleFilled(center, radius * 0.42f, ImGui.GetColorU32(VortexEye));
        drawList.AddCircle(center, radius * (1.95f + pulse * 0.35f * (0.3f + heat)),
            ImGui.GetColorU32(tint with { W = 0.25f + heat * 0.55f * pulse }), 0, MathF.Max(1f, radius * 0.18f));
    }

    public static void DrawMarbles(ImDrawListPtr drawList, CoilBoard board, in Camera2D camera, float time)
    {
        var radius = camera.Px(CoilBoard.MarbleRadius);
        var marbles = board.Marbles;
        for (var pass = 0; pass < 2; pass++)
        {
            for (var index = 0; index < marbles.Length; index++)
            {
                ref readonly var marble = ref marbles[index];
                var size = SizeFactor(board.Track, marble.Arc - marble.Lag);
                if (size <= 0f)
                {
                    continue;
                }

                var arc = marble.Arc - marble.Lag;
                var center = camera.ToScreen(board.Track.PositionAt(arc));
                if (marble.Arrive > 0f)
                {
                    var blend = marble.Arrive * marble.Arrive;
                    center = Vector2.Lerp(center, camera.ToScreen(marble.ArriveFrom), blend);
                }

                if (pass == 0)
                {
                    CoilArt.Shadow(drawList, center, radius * size, 1f);
                    continue;
                }

                CoilArt.Marble(drawList, center, radius * size, marble.Colour, marble.Power,
                    arc / CoilBoard.MarbleRadius, board.Track.TangentAt(arc), 1f, time);
            }
        }
    }

    private static float SizeFactor(CoilTrack track, float arc)
    {
        var emerging = (arc + CoilBoard.MarbleRadius) / CoilBoard.Diameter;
        var sinking = (track.Length + CoilBoard.MarbleRadius - arc) / CoilBoard.Diameter;
        return Math.Clamp(MathF.Min(emerging, sinking), 0f, 1f);
    }

    public static void DrawShots(ImDrawListPtr drawList, CoilBoard board, in Camera2D camera, float time)
    {
        var radius = camera.Px(CoilBoard.MarbleRadius);
        var shots = board.Shots;
        for (var index = 0; index < shots.Length; index++)
        {
            ref readonly var shot = ref shots[index];
            var center = camera.ToScreen(shot.Position);
            var direction = Vector2.Normalize(shot.Velocity);
            var fill = shot.Kind == CoilPower.None ? CoilArt.ColourOf(shot.Colour) : CoilArt.PowerColour(shot.Kind);
            for (var trail = TrailLength; trail >= 1; trail--)
            {
                var fade = 1f - trail / (float)(TrailLength + 1);
                drawList.AddCircleFilled(center - direction * radius * 0.9f * trail, radius * (0.9f - trail * 0.12f),
                    ImGui.GetColorU32(fill with { W = 0.22f * fade }));
            }

            if (shot.Kind != CoilPower.None)
            {
                ProgressRing.Glow(center, radius * 2.2f, fill, 1.4f);
            }

            CoilArt.Marble(drawList, center, radius, shot.Colour, CoilPower.None, time * 18f, direction, 1f, time);
        }
    }

    public static void DrawAim(ImDrawListPtr drawList, CoilBoard board, in Camera2D camera, Vector2 direction,
        float scale)
    {
        var radius = camera.Px(CoilBoard.MarbleRadius);
        var mouth = camera.ToScreen(board.Mouth(direction));
        var tint = board.ArmedPower == CoilPower.None
            ? CoilArt.ColourOf(board.LoadedColour)
            : CoilArt.PowerColour(board.ArmedPower);
        if (board.GuideLeft > 0f)
        {
            board.PredictImpact(direction, out var impact);
            var target = camera.ToScreen(impact);
            var guide = CoilArt.PowerColour(CoilPower.Guide);
            var fade = MathF.Min(1f, board.GuideLeft);
            drawList.AddLine(mouth, target, ImGui.GetColorU32(guide with { W = 0.16f * fade }), radius * 0.9f);
            drawList.AddLine(mouth, target, ImGui.GetColorU32(GamePalette.Lighten(guide, 0.4f) with { W = 0.85f * fade }),
                MathF.Max(1f, 1.4f * scale));
            drawList.AddCircle(target, radius, ImGui.GetColorU32(tint with { W = 0.75f * fade }), 0, 1.6f * scale);
            drawList.AddCircleFilled(target, radius * 0.8f, ImGui.GetColorU32(tint with { W = 0.16f * fade }));
            return;
        }

        for (var dot = 1; dot <= AimDots; dot++)
        {
            var fade = 1f - dot / (float)(AimDots + 1);
            drawList.AddCircleFilled(mouth + direction * radius * 1.35f * (dot + 0.6f), MathF.Max(1f, radius * 0.16f),
                ImGui.GetColorU32(tint with { W = 0.42f * fade }));
        }
    }

    public static void DrawLauncher(ImDrawListPtr drawList, CoilBoard board, in Camera2D camera, Vector2 direction,
        Vector4 accent, float recoil, float swap, float time)
    {
        var radius = camera.Px(CoilBoard.MarbleRadius);
        var center = camera.ToScreen(board.Launcher);
        var across = new Vector2(-direction.Y, direction.X);
        var squashAlong = 1f - 0.16f * recoil;
        var squashAcross = 1f + 0.12f * recoil;
        var aimAngle = MathF.Atan2(direction.Y, direction.X);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.2f, radius * 0.55f), radius * 2.45f,
            ImGui.GetColorU32(LauncherShadow));
        drawList.AddCircleFilled(center, radius * 2.35f, ImGui.GetColorU32(LauncherMetal));
        drawList.AddCircle(center, radius * 2.2f, ImGui.GetColorU32(accent with { W = 0.55f }), 0,
            MathF.Max(1f, radius * 0.14f));
        for (var stud = 0; stud < 10; stud++)
        {
            var angle = aimAngle + stud * MathF.Tau / 10f;
            drawList.AddCircleFilled(center + CoilShapes.Polar(angle) * radius * 1.98f, radius * 0.17f,
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.85f }));
        }

        var bodyColor = ImGui.GetColorU32(GamePalette.Darken(accent, 0.3f));
        var shellColor = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.12f));
        DrawHead(drawList, center, direction, across, radius, squashAlong, squashAcross, 1f, bodyColor);
        DrawHead(drawList, center, direction, across, radius, squashAlong, squashAcross, 0.78f, shellColor);
        var sheen = Local(center, direction, across, radius, squashAlong, squashAcross, -0.3f, -0.5f);
        drawList.AddCircleFilled(sheen, radius * 0.5f, ImGui.GetColorU32(LauncherSheen));
        for (var side = -1; side <= 1; side += 2)
        {
            var eye = Local(center, direction, across, radius, squashAlong, squashAcross, 0.55f, side * 0.82f);
            drawList.AddCircleFilled(eye, radius * 0.22f, ImGui.GetColorU32(EyeInk));
            drawList.AddCircleFilled(eye - new Vector2(radius * 0.06f, radius * 0.06f), radius * 0.08f,
                ImGui.GetColorU32(EyeShine));
        }

        var swapScale = 1f - 0.35f * swap;
        var next = Local(center, direction, across, radius, squashAlong, squashAcross, -1.25f, 0f);
        CoilArt.Marble(drawList, next, radius * 0.55f * swapScale, board.NextColour, CoilPower.None, 0f, direction, 1f,
            time);
        var mouthRing = Local(center, direction, across, radius, squashAlong, squashAcross, 1.35f - recoil * 0.5f, 0f);
        drawList.AddCircleFilled(mouthRing, radius * 1.08f, ImGui.GetColorU32(MouthWell));
        if (board.ArmedPower != CoilPower.None)
        {
            ProgressRing.Glow(mouthRing, radius * 1.7f, CoilArt.PowerColour(board.ArmedPower),
                1.2f + 0.5f * MathF.Sin(time * 7f));
        }

        CoilArt.Marble(drawList, mouthRing, radius * 0.95f * swapScale, board.LoadedColour, board.ArmedPower, 0f,
            direction, 1f, time);
    }

    private static void DrawHead(ImDrawListPtr drawList, Vector2 center, Vector2 along, Vector2 across, float radius,
        float squashAlong, float squashAcross, float size, uint color)
    {
        for (var step = 0; step <= 10; step++)
        {
            var angle = MathF.PI * 0.35f + step / 10f * MathF.PI * 1.3f;
            var local = CoilShapes.Polar(angle) * 1.55f * size;
            drawList.PathLineTo(Local(center, along, across, radius, squashAlong, squashAcross, local.X - 0.15f, local.Y));
        }

        drawList.PathLineTo(Local(center, along, across, radius, squashAlong, squashAcross, 2.05f * size, -0.95f * size));
        drawList.PathLineTo(Local(center, along, across, radius, squashAlong, squashAcross, 2.05f * size, 0.95f * size));
        drawList.PathFillConvex(color);
    }

    private static Vector2 Local(Vector2 center, Vector2 along, Vector2 across, float radius, float squashAlong,
        float squashAcross, float forward, float side) =>
        center + along * (forward * radius * squashAlong) + across * (side * radius * squashAcross);
}
