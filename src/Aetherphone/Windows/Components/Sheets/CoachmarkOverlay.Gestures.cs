using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Onboarding;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class CoachmarkOverlay
{
    private const float FingerUnits = 13f;
    private const float TapPeriodSeconds = 1.9f;
    private const float HoldPeriodSeconds = 2.8f;
    private const float SwipePeriodSeconds = 2.2f;
    private const float GestureLeadSeconds = 0.45f;
    private const float SwipeTravelUnits = 40f;
    private const int SwipeTrail = 7;
    private static readonly Vector4 FingerFill = new(1f, 1f, 1f, 0.30f);
    private static readonly Vector4 FingerEdge = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 FingerShadow = new(0f, 0f, 0f, 0.22f);

    private static void DrawGesture(ImDrawListPtr drawList, Vector2 center, GuideGesture gesture, float clock,
        float alpha, float scale)
    {
        if (alpha <= 0.001f || clock < GestureLeadSeconds)
        {
            return;
        }

        var time = clock - GestureLeadSeconds;
        switch (gesture)
        {
            case GuideGesture.Tap:
                DrawTap(drawList, center, time % TapPeriodSeconds, alpha, scale);
                break;
            case GuideGesture.Hold:
                DrawHold(drawList, center, time % HoldPeriodSeconds, alpha, scale);
                break;
            case GuideGesture.SwipeDown:
                DrawSwipe(drawList, center, time % SwipePeriodSeconds, alpha, scale, 1f);
                break;
            case GuideGesture.SwipeUp:
                DrawSwipe(drawList, center, time % SwipePeriodSeconds, alpha, scale, -1f);
                break;
        }
    }

    public static void DrawMiniCue(ImDrawListPtr drawList, Rect body, float radius, float scale)
    {
        Ring(drawList, body.Inset(-4f * scale), radius + 4f * scale, 1f, scale);
        var seconds = (float)(Environment.TickCount64 % 1_000_000L / 1000.0);
        DrawTap(drawList, body.Center, seconds % TapPeriodSeconds, 1f, scale);
    }

    private static float Bump(float seconds, float start, float end, float smoothTime) =>
        Spring.Settle(seconds - start, smoothTime) - Spring.Settle(seconds - end, smoothTime);

    private static float Presence(float seconds, float leave) =>
        Spring.Settle(seconds, 0.08f) * (1f - Spring.Settle(seconds - leave, 0.08f));

    private static void DrawTap(ImDrawListPtr drawList, Vector2 center, float seconds, float alpha, float scale)
    {
        var shown = Presence(seconds, 1.2f) * alpha;
        var press = Bump(seconds, 0.35f, 0.55f, 0.05f);
        var radius = FingerUnits * scale * (1.18f - 0.18f * Spring.Settle(seconds, 0.08f) - 0.2f * press);
        Ripple(drawList, center, FingerUnits * scale, seconds - 0.42f, alpha);
        Finger(drawList, center, radius, shown, scale);
    }

    private static void DrawHold(ImDrawListPtr drawList, Vector2 center, float seconds, float alpha, float scale)
    {
        var shown = Presence(seconds, 1.95f) * alpha;
        var press = Bump(seconds, 0.30f, 1.45f, 0.06f);
        var radius = FingerUnits * scale * (1.18f - 0.18f * Spring.Settle(seconds, 0.08f) - 0.18f * press);
        var progress = Math.Clamp((seconds - 0.4f) / 0.95f, 0f, 1f);
        if (progress > 0f && seconds < 1.5f)
        {
            var arcRadius = FingerUnits * scale * 1.75f;
            var start = -MathF.PI * 0.5f;
            drawList.AddCircle(center, arcRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f * shown)), 48,
                2.4f * scale);
            drawList.PathClear();
            drawList.PathArcTo(center, arcRadius, start, start + progress * MathF.PI * 2f, 48);
            drawList.PathStroke(ImGui.GetColorU32(BrandMark.Lilac with { W = shown }), ImDrawFlags.None,
                2.6f * scale);
        }

        Ripple(drawList, center, FingerUnits * scale * 1.4f, seconds - 1.45f, alpha);
        Finger(drawList, center, radius, shown, scale);
    }

    private static void DrawSwipe(ImDrawListPtr drawList, Vector2 center, float seconds, float alpha, float scale,
        float direction)
    {
        var shown = Presence(seconds, 1.35f) * alpha;
        var travel = SwipeTravelUnits * scale * direction;
        var start = center - new Vector2(0f, travel * 0.5f);
        var progress = Spring.Settle(seconds - 0.35f, 0.16f);
        var position = start + new Vector2(0f, travel * progress);
        var press = Bump(seconds, 0.25f, 1.05f, 0.06f);
        var radius = FingerUnits * scale * (1.18f - 0.18f * Spring.Settle(seconds, 0.08f) - 0.16f * press);
        for (var trailIndex = 0; trailIndex < SwipeTrail; trailIndex++)
        {
            var along = trailIndex / (float)SwipeTrail;
            var point = Vector2.Lerp(start, position, along);
            var weight = along * along * 0.22f * shown * press;
            drawList.AddCircleFilled(point, radius * (0.55f + 0.4f * along),
                ImGui.GetColorU32(BrandMark.Lilac with { W = weight }), 24);
        }

        Finger(drawList, position, radius, shown, scale);
    }

    private static void Ripple(ImDrawListPtr drawList, Vector2 center, float radius, float seconds, float alpha)
    {
        const float life = 0.7f;
        if (seconds <= 0f || seconds >= life)
        {
            return;
        }

        var fade = 1f - seconds / life;
        var reach = radius * (1f + 1.7f * Spring.Settle(seconds, 0.16f));
        drawList.AddCircle(center, reach, ImGui.GetColorU32(BrandMark.Lilac with { W = 0.7f * fade * fade * alpha }),
            48, 2f * UiScale.Current);
    }

    private static void Finger(ImDrawListPtr drawList, Vector2 center, float radius, float alpha, float scale)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        drawList.AddCircleFilled(center + new Vector2(0f, 2.5f * scale), radius * 1.08f,
            ImGui.GetColorU32(FingerShadow with { W = FingerShadow.W * alpha }), 32);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(FingerFill with { W = FingerFill.W * alpha }), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(FingerEdge with { W = FingerEdge.W * alpha }), 32,
            1.5f * scale);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.32f), radius * 0.28f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f * alpha)), 16);
    }
}
