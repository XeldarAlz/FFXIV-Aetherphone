using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SceneDrift
{
    private const float LiftPerScroll = 0.12f;
    private const float MaxLift = 44f;

    private static Spring lift;
    private static Spring swayX;
    private static Spring swayY;
    private static int steppedFrame = -1;

    public static void Sample(Rect frame, out float liftPixels, out Vector2 sway)
    {
        var frameCount = ImGui.GetFrameCount();
        if (frameCount != steppedFrame)
        {
            steppedFrame = frameCount;
            Step(frame);
        }

        liftPixels = lift.Value;
        sway = new Vector2(swayX.Value, swayY.Value);
    }

    private static void Step(Rect frame)
    {
        var deltaSeconds = ImGui.GetIO().DeltaTime;
        var liftTarget = MathF.Min(AppSurface.OuterScrollY * LiftPerScroll, MaxLift * UiScale.Current);
        lift.Step(liftTarget, Motion.PageSettle, deltaSeconds);
        var pointer = ImGui.GetIO().MousePos;
        var inside = pointer.X >= frame.Min.X && pointer.X <= frame.Max.X && pointer.Y >= frame.Min.Y &&
                     pointer.Y <= frame.Max.Y;
        var targetX = inside ? Math.Clamp((pointer.X - frame.Center.X) / (frame.Width * 0.5f), -1f, 1f) : 0f;
        var targetY = inside ? Math.Clamp((pointer.Y - frame.Center.Y) / (frame.Height * 0.5f), -1f, 1f) : 0f;
        swayX.Step(targetX, Motion.Sheet, deltaSeconds);
        swayY.Step(targetY, Motion.Sheet, deltaSeconds);
    }
}
