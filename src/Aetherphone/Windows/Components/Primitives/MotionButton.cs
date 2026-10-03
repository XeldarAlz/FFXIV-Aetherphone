using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct ButtonPose(Rect Face, float Hover, float Press, float Sheen);

internal static class MotionButton
{
    public static readonly Vector4 BrandTop = new(0.66f, 0.50f, 1f, 1f);
    public static readonly Vector4 BrandBottom = new(0.49f, 0.32f, 0.96f, 1f);
    public static readonly Vector4 BrandHoverTop = new(0.72f, 0.58f, 1f, 1f);
    public static readonly Vector4 BrandHoverBottom = new(0.56f, 0.40f, 1f, 1f);

    private const int MotionSlots = 16;
    private const int StaleFrames = 30;
    private const float HoverLiftScale = 0.03f;
    private const float PressDepthScale = 0.045f;
    private const float SheenSeconds = 0.65f;
    private const float RimReachUnits = 90f;
    private const int HaloLayers = 7;
    private static readonly Vector4 LabelInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 1f);

    private struct MotionState
    {
        public int Id;
        public int LastFrame;
        public Spring Hover;
        public Spring Press;
        public float SheenClock;
        public bool WasHovered;
    }

    private static readonly MotionState[] States = new MotionState[MotionSlots];

    public static ButtonPose Animate(Rect rect, string key, bool hovered, bool pressed)
    {
        var frame = ImGui.GetFrameCount();
        var delta = ImGui.GetIO().DeltaTime;
        ref var state = ref StateFor(key.GetHashCode(), frame);
        if (state.LastFrame != frame - 1 && state.LastFrame != frame)
        {
            state.Hover.SnapTo(0f);
            state.Press.SnapTo(0f);
            state.SheenClock = float.MaxValue;
            state.WasHovered = false;
        }

        state.LastFrame = frame;
        if (hovered && !state.WasHovered)
        {
            state.SheenClock = 0f;
        }

        state.WasHovered = hovered;
        state.SheenClock = state.SheenClock >= float.MaxValue ? float.MaxValue : state.SheenClock + delta;
        var hover = Math.Clamp(state.Hover.Step(hovered ? 1f : 0f, Motion.HoverLift, delta), 0f, 1f);
        var press = Math.Clamp(state.Press.Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta),
            0f, 1f);
        var grow = 1f + HoverLiftScale * hover - PressDepthScale * press;
        var center = rect.Center;
        var half = rect.Size * 0.5f * grow;
        var sheen = state.SheenClock / SheenSeconds;
        return new ButtonPose(new Rect(center - half, center + half), hover, press, sheen <= 1f ? sheen : -1f);
    }

    public static ButtonPose Brand(ImDrawListPtr drawList, Rect rect, string key, string label, float alpha,
        bool hovered, bool enabled, Vector4 disabledFill, Vector4 disabledInk)
    {
        var scale = UiScale.Current;
        var pose = Animate(rect, key, hovered, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var face = pose.Face;
        var radius = face.Height * 0.5f;
        if (enabled)
        {
            Halo(drawList, face, radius, BrandMark.Violet, (0.10f + 0.32f * pose.Hover) * alpha);
            Squircle.FillVerticalGradient(drawList, face.Min, face.Max, radius,
                ImGui.GetColorU32(Fade(Vector4.Lerp(BrandTop, BrandHoverTop, pose.Hover), alpha)),
                ImGui.GetColorU32(Fade(Vector4.Lerp(BrandBottom, BrandHoverBottom, pose.Hover), alpha)));
            BrandMark.Sheen(drawList, face.Min, face.Max, radius, pose.Sheen, alpha);
            Squircle.StrokeDirectional(drawList, face.Min, face.Max, radius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (0.35f + 0.2f * pose.Hover) * alpha)), 1.2f * scale,
                new Vector2(0f, -1f), 2f);
            RimLight(drawList, face, radius, pose.Hover * alpha);
        }
        else
        {
            Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(Fade(disabledFill, alpha)));
        }

        Typography.DrawCentered(drawList, face.Center,
            Typography.FitText(label, face.Width - Metrics.Space.Xl * scale, TextStyles.Headline),
            enabled ? Fade(LabelInk, alpha) : Fade(disabledInk, alpha), TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return pose;
    }

    public static ButtonPose Glass(ImDrawListPtr drawList, Rect rect, string key, string label, Vector4 ink,
        GlassTone tone, float alpha, bool hovered)
    {
        var scale = UiScale.Current;
        var pose = Animate(rect, key, hovered, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var face = pose.Face;
        var radius = face.Height * 0.5f;
        Halo(drawList, face, radius, Shadow, 0.18f * pose.Hover * alpha);
        Material.LiquidGlass(drawList, face.Min, face.Max, radius, scale, tone, 0f, alpha);
        BrandMark.Sheen(drawList, face.Min, face.Max, radius, pose.Sheen, 0.8f * alpha);
        RimLight(drawList, face, radius, pose.Hover * alpha);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, face.Center,
            Typography.FitText(label, face.Width - Metrics.Space.Xl * scale, TextStyles.Headline), Fade(ink, alpha),
            TextStyles.Headline);
        return pose;
    }

    public static void Halo(ImDrawListPtr drawList, Rect face, float radius, Vector4 tint, float strength)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        var scale = UiScale.Current;
        var color = ImGui.GetColorU32(tint with { W = tint.W * strength / HaloLayers });
        for (var layerIndex = 0; layerIndex < HaloLayers; layerIndex++)
        {
            var spread = (layerIndex + 1) * 2.2f * scale;
            var drop = new Vector2(0f, spread * 0.35f);
            Squircle.Fill(drawList, face.Min - new Vector2(spread, spread) + drop,
                face.Max + new Vector2(spread, spread) + drop, radius + spread, color);
        }
    }

    public static void RimLight(ImDrawListPtr drawList, Rect face, float radius, float strength)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        var scale = UiScale.Current;
        Squircle.StrokeNear(drawList, face.Min, face.Max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.75f * strength)), 1.4f * scale, ImGui.GetMousePos(),
            RimReachUnits * scale);
    }

    private static ref MotionState StateFor(int id, int frame)
    {
        var free = -1;
        for (var slotIndex = 0; slotIndex < States.Length; slotIndex++)
        {
            if (States[slotIndex].Id == id && States[slotIndex].LastFrame != 0)
            {
                return ref States[slotIndex];
            }

            if (free < 0 && frame - States[slotIndex].LastFrame > StaleFrames)
            {
                free = slotIndex;
            }
        }

        var slot = free >= 0 ? free : 0;
        States[slot] = new MotionState { Id = id, LastFrame = -2, SheenClock = float.MaxValue };
        return ref States[slot];
    }

    private static Vector4 Fade(Vector4 color, float alpha) => color with { W = color.W * alpha };
}
