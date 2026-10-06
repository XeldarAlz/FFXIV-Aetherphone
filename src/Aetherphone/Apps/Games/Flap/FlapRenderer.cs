using Aetherphone.Apps.Games.Framework;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Flap;

internal static class FlapRenderer
{
    public static readonly Vector4 BirdBody = new(0.98f, 0.82f, 0.32f, 1f);
    private const float FarBaseline = FlapBoard.WorldHeight - 1f;
    private const float FarBump = 1.36f;
    private const float FarParallax = 0.4f;
    private const float NearBaseline = FlapBoard.WorldHeight - 0.56f;
    private const float NearBump = 1.92f;
    private const float NearParallax = 0.7f;
    private const float GrassTop = FlapBoard.WorldHeight - 0.29f;
    private const float BumpSpacing = 1.5f;
    private const float CapHeight = 0.56f;
    private const float CapOverhang = 0.12f;
    private const float PipeRounding = 0.18f;
    private static readonly Vector4 FarHill = new(0.46f, 0.66f, 0.60f, 1f);
    private static readonly Vector4 NearHill = new(0.38f, 0.64f, 0.42f, 1f);
    private static readonly Vector4 PipeBody = new(0.40f, 0.74f, 0.42f, 1f);
    private static readonly Vector4 BeakColor = new(0.95f, 0.55f, 0.20f, 1f);
    private static readonly Vector4 Pupil = new(0.1f, 0.1f, 0.12f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static void DrawGround(ImDrawListPtr drawList, in Camera2D camera)
    {
        DrawHillLayer(drawList, in camera, FarHill, FarBaseline, FarBump, FarParallax);
        DrawHillLayer(drawList, in camera, NearHill, NearBaseline, NearBump, NearParallax);
        var view = camera.View;
        var grassTop = camera.ToScreen(new Vector2(0f, GrassTop)).Y;
        drawList.AddRectFilled(new Vector2(view.Min.X, grassTop), new Vector2(view.Max.X, view.Max.Y + 2f),
            ImGui.GetColorU32(GamePalette.Darken(NearHill, 0.18f)));
    }

    private static void DrawHillLayer(ImDrawListPtr drawList, in Camera2D camera, Vector4 color, float baseline,
        float bumpRadius, float parallax)
    {
        var fill = ImGui.GetColorU32(color);
        var view = camera.View;
        var baseY = camera.ToScreen(new Vector2(0f, baseline)).Y;
        drawList.AddRectFilled(new Vector2(view.Min.X, baseY), new Vector2(view.Max.X, view.Max.Y + 2f), fill);
        var spacing = bumpRadius * BumpSpacing;
        var shift = camera.Origin.X * (1f - parallax);
        var visible = camera.VisibleWorld;
        var first = (int)MathF.Floor((visible.Min.X - shift) / spacing) - 1;
        var last = (int)MathF.Ceiling((visible.Max.X - shift) / spacing) + 1;
        for (var bump = first; bump <= last; bump++)
        {
            var wobble = MathF.Sin(bump * 0.7f) * bumpRadius * 0.35f;
            var center = camera.ToScreen(new Vector2(bump * spacing + shift, baseline + wobble * 0.2f));
            drawList.AddCircleFilled(center, camera.Px(bumpRadius + wobble), fill, 24);
        }
    }

    public static void DrawPipes(ImDrawListPtr drawList, in Camera2D camera, FlapBoard board, float scale)
    {
        var visible = camera.VisibleWorld;
        var width = camera.Px(FlapBoard.PipeWidth);
        var rounding = width * PipeRounding;
        var capHeight = camera.Px(CapHeight);
        var capOverhang = width * CapOverhang;
        var capRounding = capHeight * 0.4f;
        var edge = ImGui.GetColorU32(GamePalette.Darken(PipeBody, 0.28f));
        var body = ImGui.GetColorU32(PipeBody);
        var sheen = ImGui.GetColorU32(GamePalette.Lighten(PipeBody, 0.3f) with { W = 0.6f });
        var top = camera.View.Min.Y - 2f;
        var ground = camera.ToScreen(new Vector2(0f, FlapBoard.WorldHeight)).Y;
        var stroke = 1.4f * scale;
        for (var index = 0; index < board.PipeCount; index++)
        {
            ref readonly var pipe = ref board.PipeAt(index);
            if (pipe.X + FlapBoard.PipeWidth < visible.Min.X - FlapBoard.PipeWidth || pipe.X > visible.Max.X + FlapBoard.PipeWidth)
            {
                continue;
            }

            var left = camera.ToScreen(new Vector2(pipe.X, 0f)).X;
            var right = left + width;
            var gapTop = camera.ToScreen(new Vector2(0f, pipe.GapCenter - pipe.GapHalf)).Y;
            var gapBottom = camera.ToScreen(new Vector2(0f, pipe.GapCenter + pipe.GapHalf)).Y;
            DrawPipeSegment(drawList, new Vector2(left, top), new Vector2(right, gapTop), body, edge, sheen, rounding,
                stroke);
            DrawPipeSegment(drawList, new Vector2(left, gapBottom), new Vector2(right, ground), body, edge, sheen,
                rounding, stroke);
            var capLeft = left - capOverhang;
            var capRight = right + capOverhang;
            drawList.AddRectFilled(new Vector2(capLeft, gapTop - capHeight), new Vector2(capRight, gapTop), body,
                capRounding);
            drawList.AddRectFilled(new Vector2(capLeft, gapBottom), new Vector2(capRight, gapBottom + capHeight), body,
                capRounding);
            drawList.AddRect(new Vector2(capLeft, gapTop - capHeight), new Vector2(capRight, gapTop), edge, capRounding,
                ImDrawFlags.RoundCornersAll, stroke);
            drawList.AddRect(new Vector2(capLeft, gapBottom), new Vector2(capRight, gapBottom + capHeight), edge,
                capRounding, ImDrawFlags.RoundCornersAll, stroke);
        }
    }

    private static void DrawPipeSegment(ImDrawListPtr drawList, Vector2 min, Vector2 max, uint body, uint edge,
        uint sheen, float rounding, float stroke)
    {
        if (max.Y <= min.Y)
        {
            return;
        }

        drawList.AddRectFilled(min, max, body, rounding);
        drawList.AddRectFilled(new Vector2(min.X + (max.X - min.X) * 0.16f, min.Y),
            new Vector2(min.X + (max.X - min.X) * 0.34f, max.Y), sheen, rounding);
        drawList.AddRect(min, max, edge, rounding, ImDrawFlags.RoundCornersAll, stroke);
    }

    public static void DrawBird(ImDrawListPtr drawList, Vector2 center, float radius, float tilt, float wingPhase,
        float scale)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.16f), radius * 0.95f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.14f)), 24);
        ProgressRing.Glow(center, radius * 1.2f, BirdBody, 0.35f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BirdBody), 28);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.28f, radius * 0.34f), radius * 0.34f,
            ImGui.GetColorU32(White with { W = 0.35f }), 18);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Darken(BirdBody, 0.28f)), 28, 1.6f * scale);
        var wingFlap = MathF.Sin(wingPhase) * radius * 0.22f;
        var wing = Rotate(center, new Vector2(-radius * 0.15f, wingFlap), tilt);
        drawList.AddCircleFilled(wing, radius * 0.5f, ImGui.GetColorU32(GamePalette.Lighten(BirdBody, 0.18f)), 20);
        var eye = Rotate(center, new Vector2(radius * 0.4f, -radius * 0.32f), tilt);
        drawList.AddCircleFilled(eye, radius * 0.26f, ImGui.GetColorU32(White), 16);
        drawList.AddCircleFilled(Rotate(center, new Vector2(radius * 0.5f, -radius * 0.32f), tilt), radius * 0.12f,
            ImGui.GetColorU32(Pupil), 12);
        var beak = ImGui.GetColorU32(BeakColor);
        var beakTop = Rotate(center, new Vector2(radius * 0.9f, -radius * 0.05f), tilt);
        var beakTip = Rotate(center, new Vector2(radius * 1.5f, radius * 0.08f), tilt);
        var beakBottom = Rotate(center, new Vector2(radius * 0.9f, radius * 0.28f), tilt);
        drawList.AddTriangleFilled(beakTop, beakTip, beakBottom, beak);
    }

    private static Vector2 Rotate(Vector2 center, Vector2 offset, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(center.X + offset.X * cosine - offset.Y * sine, center.Y + offset.X * sine + offset.Y * cosine);
    }
}
