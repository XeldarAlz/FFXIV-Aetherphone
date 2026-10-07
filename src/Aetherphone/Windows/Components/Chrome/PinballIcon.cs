using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PinballIcon
{
    private const float PivotRadius = 0.17f;
    private const float TipRadius = 0.1f;
    private static readonly Vector2 LeftPivot = new(-0.86f, 0.5f);
    private static readonly Vector2 LeftTip = new(-0.2f, 0.86f);
    private static readonly Vector2 Bumper = new(-0.38f, -0.42f);
    private static readonly Vector2 Ball = new(0.42f, -0.38f);

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        Flipper(drawList, center + LeftPivot * extent, center + LeftTip * extent, extent, ink);
        Flipper(drawList, center + new Vector2(-LeftPivot.X, LeftPivot.Y) * extent,
            center + new Vector2(-LeftTip.X, LeftTip.Y) * extent, extent, ink);
        var bumper = center + Bumper * extent;
        drawList.AddCircleFilled(bumper, extent * 0.36f, ink, 28);
        drawList.AddCircleFilled(bumper, extent * 0.22f, hole, 24);
        drawList.AddCircleFilled(bumper, extent * 0.12f, ink, 16);
        var ball = center + Ball * extent;
        drawList.AddCircleFilled(ball, extent * 0.3f, ink, 24);
        drawList.AddCircleFilled(ball - new Vector2(extent * 0.1f, extent * 0.1f), extent * 0.09f, hole, 12);
        for (var dot = 0; dot < 3; dot++)
        {
            var offset = new Vector2(0.2f + dot * 0.17f, 0.24f + dot * 0.2f);
            drawList.AddCircleFilled(ball - new Vector2(offset.X * extent, -offset.Y * extent),
                extent * (0.09f - dot * 0.022f), ink, 10);
        }
    }

    private static void Flipper(ImDrawListPtr drawList, Vector2 pivot, Vector2 tip, float extent, uint ink)
    {
        var direction = Vector2.Normalize(tip - pivot);
        var side = new Vector2(-direction.Y, direction.X);
        var pivotRadius = extent * PivotRadius;
        var tipRadius = extent * TipRadius;
        drawList.AddCircleFilled(pivot, pivotRadius, ink, 18);
        drawList.AddCircleFilled(tip, tipRadius, ink, 14);
        drawList.AddQuadFilled(pivot + side * pivotRadius, tip + side * tipRadius, tip - side * tipRadius,
            pivot - side * pivotRadius, ink);
    }
}
