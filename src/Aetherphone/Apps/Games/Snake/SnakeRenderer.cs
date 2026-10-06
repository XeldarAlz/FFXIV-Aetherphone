using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Snake;

internal static class SnakeRenderer
{
    public const float SegmentRadius = 0.42f;
    private const float FruitRadius = 0.36f;
    private const float FloorRadius = 0.4f;
    private const float TimerRingRadius = 0.56f;
    public static readonly Vector4 HeadColor = new(0.42f, 0.84f, 0.48f, 1f);
    public static readonly Vector4 TailColor = new(0.20f, 0.52f, 0.32f, 1f);
    public static readonly Vector4 AppleColor = new(0.96f, 0.42f, 0.44f, 1f);
    public static readonly Vector4 GoldColor = new(1f, 0.82f, 0.32f, 1f);
    public static readonly Vector4 BombColor = new(0.22f, 0.22f, 0.28f, 1f);
    private static readonly Vector4 LeafColor = new(0.44f, 0.78f, 0.42f, 1f);
    private static readonly Vector4 FloorFill = new(0.05f, 0.12f, 0.06f, 0.42f);
    private static readonly Vector4 FloorCheck = new(1f, 1f, 1f, 0.035f);
    private static readonly Vector4 WallRim = new(0.95f, 1f, 0.9f, 0.35f);
    private static readonly Vector4 WrapRim = new(0.95f, 1f, 0.9f, 0.12f);
    private static readonly Vector4 BombShine = new(0.5f, 0.52f, 0.58f, 0.7f);
    private static readonly Vector4 Fuse = new(0.7f, 0.6f, 0.4f, 1f);
    private static readonly Vector4 FuseSpark = new(1f, 0.7f, 0.25f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Pupil = new(0.1f, 0.12f, 0.12f, 1f);

    public static void DrawFloor(ImDrawListPtr drawList, in Camera2D camera, bool wrap, float scale)
    {
        var min = camera.ToScreen(Vector2.Zero);
        var max = camera.ToScreen(new Vector2(SnakeBoard.Columns, SnakeBoard.Rows));
        var radius = camera.Px(FloorRadius);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(FloorFill));
        var check = ImGui.GetColorU32(FloorCheck);
        var cell = camera.Px(1f);
        for (var row = 0; row < SnakeBoard.Rows; row++)
        {
            for (var column = row & 1; column < SnakeBoard.Columns; column += 2)
            {
                var cellMin = min + new Vector2(column * cell, row * cell);
                drawList.AddRectFilled(cellMin, cellMin + new Vector2(cell, cell), check);
            }
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(wrap ? WrapRim : WallRim),
            (wrap ? 1f : 2f) * scale);
    }

    public static void DrawFruit(ImDrawListPtr drawList, in Camera2D camera, SnakeBoard board, float scale)
    {
        if (board.FruitCell >= 0)
        {
            DrawApple(drawList, camera.ToScreen(SnakeBoard.CellCenter(board.FruitCell)), camera.Px(FruitRadius));
        }

        if (board.SpecialKind == FruitKind.None || board.SpecialCell < 0)
        {
            return;
        }

        var center = camera.ToScreen(SnakeBoard.CellCenter(board.SpecialCell));
        var radius = camera.Px(FruitRadius);
        var ringRadius = camera.Px(TimerRingRadius);
        var thickness = MathF.Max(1f, 1.5f * scale);
        var color = board.SpecialKind == FruitKind.Gold ? GoldColor : FuseSpark;
        ProgressRing.Track(drawList, center, ringRadius, thickness, color with { W = 0.22f });
        ProgressRing.Fill(drawList, center, ringRadius, thickness, board.SpecialFraction, color);
        if (board.SpecialKind == FruitKind.Gold)
        {
            DrawGold(drawList, center, radius);
            return;
        }

        DrawBomb(drawList, center, radius, scale);
    }

    public static void DrawSnake(ImDrawListPtr drawList, in Camera2D camera, SnakeBoard board, float eatPulse)
    {
        var length = board.Length;
        var radius = camera.Px(SegmentRadius);
        var outlineWidth = MathF.Max(1f, camera.Px(0.06f));
        var outline = ImGui.GetColorU32(GamePalette.Darken(TailColor, 0.55f));
        for (var segment = length - 1; segment >= 1; segment--)
        {
            var fraction = 1f - segment / (float)length;
            var segmentRadius = radius * (0.6f + 0.4f * fraction);
            drawList.AddCircleFilled(camera.ToScreen(board.SegmentWorld(segment)), segmentRadius + outlineWidth,
                outline, 20);
        }

        for (var segment = length - 1; segment >= 1; segment--)
        {
            var fraction = 1f - segment / (float)length;
            var segmentRadius = radius * (0.6f + 0.4f * fraction);
            var position = camera.ToScreen(board.SegmentWorld(segment));
            drawList.AddCircleFilled(position, segmentRadius, ImGui.GetColorU32(Vector4.Lerp(TailColor, HeadColor, fraction)), 20);
            drawList.AddCircleFilled(position - new Vector2(segmentRadius * 0.25f, segmentRadius * 0.3f),
                segmentRadius * 0.32f, ImGui.GetColorU32(White with { W = 0.10f + 0.08f * fraction }), 12);
        }

        DrawHead(drawList, camera.ToScreen(board.HeadWorld), radius, board.Heading, eatPulse);
    }

    private static void DrawHead(ImDrawListPtr drawList, Vector2 head, float radius, Vector2 forward, float eatPulse)
    {
        var headRadius = radius * (1.18f + 0.30f * eatPulse);
        ProgressRing.Glow(head, headRadius * 1.3f, HeadColor, 0.35f + 0.5f * eatPulse);
        drawList.AddCircleFilled(head, headRadius, ImGui.GetColorU32(HeadColor), 24);
        drawList.AddCircle(head, headRadius, ImGui.GetColorU32(GamePalette.Darken(HeadColor, 0.3f)), 24, 1.4f);
        var side = new Vector2(-forward.Y, forward.X);
        var eyeBase = head + forward * headRadius * 0.4f;
        var leftEye = eyeBase + side * headRadius * 0.42f;
        var rightEye = eyeBase - side * headRadius * 0.42f;
        var white = ImGui.GetColorU32(White);
        var pupil = ImGui.GetColorU32(Pupil);
        drawList.AddCircleFilled(leftEye, headRadius * 0.3f, white, 14);
        drawList.AddCircleFilled(rightEye, headRadius * 0.3f, white, 14);
        drawList.AddCircleFilled(leftEye + forward * headRadius * 0.1f, headRadius * 0.15f, pupil, 10);
        drawList.AddCircleFilled(rightEye + forward * headRadius * 0.1f, headRadius * 0.15f, pupil, 10);
    }

    private static void DrawApple(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        var pulse = 0.85f + 0.15f * Pulse.Wave(Pulse.Fast);
        ProgressRing.Glow(center, radius * 2f, AppleColor, 0.7f);
        drawList.AddCircleFilled(center, radius * pulse, ImGui.GetColorU32(AppleColor), 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.34f,
            ImGui.GetColorU32(White with { W = 0.6f }), 16);
        var leafBase = center + new Vector2(radius * 0.15f, -radius * pulse);
        drawList.AddTriangleFilled(leafBase, leafBase + new Vector2(radius * 0.55f, -radius * 0.65f),
            leafBase + new Vector2(radius * 0.05f, -radius * 0.5f), ImGui.GetColorU32(LeafColor));
    }

    private static void DrawGold(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        var pulse = 0.9f + 0.1f * Pulse.Wave(Pulse.Fast);
        ProgressRing.Glow(center, radius * 2.4f, GoldColor, 0.9f);
        drawList.AddCircleFilled(center, radius * pulse, ImGui.GetColorU32(GoldColor), 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.3f,
            ImGui.GetColorU32(White with { W = 0.7f }), 16);
        var spin = Pulse.Phase(Pulse.Orbit) * MathF.Tau;
        var arm = radius * 1.5f;
        var sparkle = ImGui.GetColorU32(White with { W = 0.85f });
        var axisA = new Vector2(MathF.Cos(spin), MathF.Sin(spin)) * arm;
        var axisB = new Vector2(-axisA.Y, axisA.X);
        drawList.AddLine(center - axisA, center + axisA, sparkle, 1.5f);
        drawList.AddLine(center - axisB, center + axisB, sparkle, 1.5f);
    }

    private static void DrawBomb(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BombColor), 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.28f,
            ImGui.GetColorU32(BombShine), 16);
        var fuseStart = center + new Vector2(radius * 0.4f, -radius * 0.8f);
        var fuseEnd = center + new Vector2(radius * 0.8f, -radius * 1.3f);
        drawList.AddLine(fuseStart, fuseEnd, ImGui.GetColorU32(Fuse), 2f * scale);
        var spark = 0.6f + 0.4f * Pulse.Wave(Pulse.Fast);
        drawList.AddCircleFilled(fuseEnd, radius * 0.22f * spark, ImGui.GetColorU32(FuseSpark), 12);
    }
}
