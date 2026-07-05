using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Flap;

internal sealed class FlapRenderer
{
    private static readonly Vector4 SkyTop = new(0.36f, 0.62f, 0.92f, 1f);
    private static readonly Vector4 SkyBottom = new(0.72f, 0.88f, 0.98f, 1f);
    private static readonly Vector4 SunColor = new(1f, 0.94f, 0.72f, 1f);
    private static readonly Vector4 FarHill = new(0.46f, 0.66f, 0.60f, 1f);
    private static readonly Vector4 NearHill = new(0.38f, 0.64f, 0.42f, 1f);
    private static readonly Vector4 PipeBody = new(0.40f, 0.74f, 0.42f, 1f);
    private static readonly Vector4 BirdBody = new(0.98f, 0.82f, 0.32f, 1f);

    public void Draw(FlapBoard board, Rect area, float birdY, float tilt, float scale, Vector2 shake, float flapPulse)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(area.Min, area.Max, true);
        DrawSky(drawList, area);
        DrawHills(drawList, area, shake);
        DrawPipes(drawList, board, area, scale, shake);
        DrawBird(drawList, FlapBoard.BirdXOf(area) + shake.X, birdY + shake.Y,
            FlapBoard.RadiusOf(area) * (1f + 0.14f * flapPulse), tilt, scale);
        drawList.PopClipRect();
    }

    private void DrawSky(ImDrawListPtr drawList, Rect area)
    {
        var top = ImGui.GetColorU32(SkyTop);
        var bottom = ImGui.GetColorU32(SkyBottom);
        drawList.AddRectFilledMultiColor(area.Min, area.Max, top, top, bottom, bottom);
        var sun = new Vector2(area.Min.X + area.Width * 0.82f, area.Min.Y + area.Height * 0.14f);
        var sunRadius = area.Height * 0.05f;
        ProgressRing.Glow(sun, sunRadius * 2.6f, SunColor, 0.8f);
        drawList.AddCircleFilled(sun, sunRadius, ImGui.GetColorU32(SunColor), 28);
        var drift = Styling.Phase(26000.0) * area.Width;
        var cloud = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f));
        for (var index = 0; index < 3; index++)
        {
            var baseX = area.Min.X + ((index * 0.42f * area.Width + drift) % (area.Width + 0.4f * area.Width)) -
                        0.2f * area.Width;
            var y = area.Min.Y + area.Height * (0.18f + index * 0.26f);
            DrawCloud(drawList, new Vector2(baseX, y), area.Height * 0.05f, cloud);
        }
    }

    private static void DrawHills(ImDrawListPtr drawList, Rect area, Vector2 shake)
    {
        var farColor = ImGui.GetColorU32(FarHill);
        var farDrift = Styling.Phase(52000.0) * area.Width;
        DrawHillLayer(drawList, area, farColor, area.Height * 0.085f, farDrift * 0.4f, 0.062f, shake * 0.4f);
        var nearColor = ImGui.GetColorU32(NearHill);
        var nearDrift = Styling.Phase(34000.0) * area.Width;
        DrawHillLayer(drawList, area, nearColor, area.Height * 0.12f, nearDrift * 0.7f, 0.035f, shake);
        var grass = ImGui.GetColorU32(GamePalette.Darken(NearHill, 0.18f));
        drawList.AddRectFilled(new Vector2(area.Min.X, area.Max.Y - area.Height * 0.018f) + shake, area.Max + shake,
            grass);
    }

    private static void DrawHillLayer(ImDrawListPtr drawList, Rect area, uint color, float bumpRadius, float drift,
        float baseFraction, Vector2 shake)
    {
        var baseTop = area.Max.Y - area.Height * baseFraction;
        drawList.AddRectFilled(new Vector2(area.Min.X, baseTop) + shake, area.Max + shake, color);
        var spacing = bumpRadius * 1.5f;
        var start = area.Min.X - spacing + (drift % spacing);
        for (var x = start; x < area.Max.X + spacing; x += spacing)
        {
            var wobble = MathF.Sin(x * 0.05f) * bumpRadius * 0.35f;
            drawList.AddCircleFilled(new Vector2(x, baseTop + wobble * 0.2f) + shake, bumpRadius + wobble, color, 24);
        }
    }

    private void DrawCloud(ImDrawListPtr drawList, Vector2 center, float radius, uint color)
    {
        drawList.AddCircleFilled(center, radius, color, 20);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.9f, radius * 0.18f), radius * 0.8f, color, 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.9f, -radius * 0.18f), radius * 0.72f, color, 20);
    }

    private void DrawPipes(ImDrawListPtr drawList, FlapBoard board, Rect area, float scale, Vector2 shake)
    {
        var width = FlapBoard.PipeWidthOf(area);
        var rounding = width * 0.18f;
        var capHeight = area.Height * 0.035f;
        var capOverhang = width * 0.12f;
        var edge = ImGui.GetColorU32(GamePalette.Darken(PipeBody, 0.28f));
        var body = ImGui.GetColorU32(PipeBody);
        var sheen = ImGui.GetColorU32(GamePalette.Lighten(PipeBody, 0.3f) with { W = 0.6f });
        for (var index = 0; index < board.PipeCount; index++)
        {
            var pipe = board.PipeAt(index);
            var left = pipe.X + shake.X;
            var right = pipe.X + width + shake.X;
            var gapTop = pipe.GapCenter - pipe.GapHalf + shake.Y;
            var gapBottom = pipe.GapCenter + pipe.GapHalf + shake.Y;
            DrawPipeSegment(drawList, new Vector2(left, area.Min.Y), new Vector2(right, gapTop), body, edge, sheen,
                rounding, scale);
            DrawPipeSegment(drawList, new Vector2(left, gapBottom), new Vector2(right, area.Max.Y), body, edge, sheen,
                rounding, scale);
            var capRounding = capHeight * 0.4f;
            drawList.AddRectFilled(new Vector2(left - capOverhang, gapTop - capHeight),
                new Vector2(right + capOverhang, gapTop), body, capRounding);
            drawList.AddRectFilled(new Vector2(left - capOverhang, gapBottom),
                new Vector2(right + capOverhang, gapBottom + capHeight), body, capRounding);
            drawList.AddRect(new Vector2(left - capOverhang, gapTop - capHeight),
                new Vector2(right + capOverhang, gapTop), edge, capRounding, ImDrawFlags.RoundCornersAll, 1.4f * scale);
            drawList.AddRect(new Vector2(left - capOverhang, gapBottom),
                new Vector2(right + capOverhang, gapBottom + capHeight), edge, capRounding, ImDrawFlags.RoundCornersAll,
                1.4f * scale);
        }
    }

    private void DrawPipeSegment(ImDrawListPtr drawList, Vector2 min, Vector2 max, uint body, uint edge, uint sheen,
        float rounding, float scale)
    {
        if (max.Y <= min.Y)
        {
            return;
        }

        drawList.AddRectFilled(min, max, body, rounding);
        drawList.AddRectFilled(new Vector2(min.X + (max.X - min.X) * 0.16f, min.Y),
            new Vector2(min.X + (max.X - min.X) * 0.34f, max.Y), sheen, rounding);
        drawList.AddRect(min, max, edge, rounding, ImDrawFlags.RoundCornersAll, 1.4f * scale);
    }

    private void DrawBird(ImDrawListPtr drawList, float x, float y, float radius, float tilt, float scale)
    {
        var center = new Vector2(x, y);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.16f), radius * 0.95f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.14f)), 24);
        ProgressRing.Glow(center, radius * 1.2f, BirdBody, 0.35f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BirdBody), 28);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.28f, radius * 0.34f), radius * 0.34f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)), 18);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Darken(BirdBody, 0.28f)), 28, 1.6f * scale);
        var wingFlap = MathF.Sin(Styling.Phase(360.0) * MathF.PI * 2f) * radius * 0.22f;
        var wing = Rotate(center, new Vector2(-radius * 0.15f, wingFlap), tilt);
        drawList.AddCircleFilled(wing, radius * 0.5f, ImGui.GetColorU32(GamePalette.Lighten(BirdBody, 0.18f)), 20);
        var eye = Rotate(center, new Vector2(radius * 0.4f, -radius * 0.32f), tilt);
        drawList.AddCircleFilled(eye, radius * 0.26f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 16);
        drawList.AddCircleFilled(Rotate(center, new Vector2(radius * 0.5f, -radius * 0.32f), tilt), radius * 0.12f,
            ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f)), 12);
        var beakColor = ImGui.GetColorU32(new Vector4(0.95f, 0.55f, 0.20f, 1f));
        var beakA = Rotate(center, new Vector2(radius * 0.9f, -radius * 0.05f), tilt);
        var beakB = Rotate(center, new Vector2(radius * 1.5f, radius * 0.08f), tilt);
        var beakC = Rotate(center, new Vector2(radius * 0.9f, radius * 0.28f), tilt);
        drawList.AddTriangleFilled(beakA, beakB, beakC, beakColor);
    }

    private static Vector2 Rotate(Vector2 center, Vector2 offset, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(center.X + offset.X * cos - offset.Y * sin, center.Y + offset.X * sin + offset.Y * cos);
    }
}
