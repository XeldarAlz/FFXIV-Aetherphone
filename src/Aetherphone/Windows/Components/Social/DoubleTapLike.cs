using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class DoubleTapLike
{
    private const float BurstDuration = 0.9f;
    private const float BurstHold = 0.55f;
    private const float BurstSize = 84f;
    private const float PopDuration = 0.22f;
    private const float RiseSpeed = 46f;
    private const float BackOvershoot = 2.70158f;
    private const float GrowDuration = 0.12f;
    private const float BeatWidth = 0.06f;
    private const float DropLife = 0.9f;
    private const float DropGravity = 220f;

    private static readonly Vector4 CrimsonInk = new(0.91f, 0.125f, 0.235f, 1f);
    private static readonly Vector4 CrimsonGlow = new(1f, 0.157f, 0.275f, 0.55f);
    private static readonly Vector4 DropInk = new(0.784f, 0.063f, 0.157f, 1f);
    private static readonly float[] DropOffsets = { -24f, -12f, -4f, 6f, 14f, 22f, 30f };
    private static readonly float[] DropDelays = { 0.05f, 0.18f, 0.0f, 0.24f, 0.1f, 0.3f, 0.14f };
    private static readonly float[] DropSpeeds = { 50f, 80f, 40f, 95f, 60f, 70f, 110f };
    private static readonly float[] DropSizes = { 2.6f, 2.1f, 3.2f, 2.4f, 2.9f, 2.2f, 2.5f };

    private string burstPostId = string.Empty;
    private string swallowPostId = string.Empty;
    private double burstStart;

    public bool Crimson { get; set; }

    public bool Tapped(Rect rect, string postId)
    {
        if (UiInteract.Hover(rect.Min, rect.Max) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            swallowPostId = postId;
            burstPostId = postId;
            burstStart = ImGui.GetTime();
            return true;
        }

        if (swallowPostId == postId && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            swallowPostId = string.Empty;
        }

        return false;
    }

    public bool SwallowedTap(string postId)
    {
        if (swallowPostId != postId)
        {
            return false;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            swallowPostId = string.Empty;
        }

        return true;
    }

    public void DrawBurst(ImDrawListPtr drawList, Rect rect, string postId)
    {
        if (burstPostId != postId)
        {
            return;
        }

        var elapsed = (float)(ImGui.GetTime() - burstStart);
        if (elapsed >= BurstDuration)
        {
            burstPostId = string.Empty;
            return;
        }

        var scale = UiScale.Current;
        if (Crimson)
        {
            DrawCrimson(drawList, rect, elapsed, scale);
            return;
        }

        var appear = Math.Clamp(elapsed / PopDuration, 0f, 1f);
        var back = appear - 1f;
        var pop = MathF.Max(1f + back * back * (BackOvershoot * back + BackOvershoot - 1f), 0.05f);
        var alpha = elapsed < BurstHold ? 1f : 1f - (elapsed - BurstHold) / (BurstDuration - BurstHold);
        var rise = elapsed < BurstHold ? 0f : (elapsed - BurstHold) * RiseSpeed * scale;
        var center = new Vector2(rect.Center.X, rect.Center.Y - rise);
        var size = BurstSize * scale * pop;
        PhoneIcon.Draw(drawList, center + new Vector2(0f, 2f * scale), PhoneIcons.HeartFilled,
            new Vector4(0f, 0f, 0f, 0.35f * alpha), size);
        PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, new Vector4(1f, 1f, 1f, alpha), size);
    }

    private static void DrawCrimson(ImDrawListPtr drawList, Rect rect, float elapsed, float scale)
    {
        var grow = Math.Clamp(elapsed / GrowDuration, 0f, 1f);
        var pulse = 1f + 0.18f * Beat(elapsed, 0.06f) + 0.12f * Beat(elapsed, 0.27f);
        var alpha = elapsed < BurstHold ? 1f : 1f - (elapsed - BurstHold) / (BurstDuration - BurstHold);
        var center = rect.Center;
        var size = BurstSize * scale * grow * pulse;
        NightScene.Glow(drawList, center, size * 0.95f, CrimsonGlow with { W = CrimsonGlow.W * alpha }, 10);
        PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, CrimsonInk with { W = alpha }, size);
        for (var index = 0; index < DropOffsets.Length; index++)
        {
            var age = elapsed - DropDelays[index];
            if (age <= 0f || age >= DropLife)
            {
                continue;
            }

            var dropSize = DropSizes[index] * scale;
            var drop = new Vector2(center.X + DropOffsets[index] * scale,
                center.Y + 26f * scale + (DropSpeeds[index] * age + DropGravity * age * age) * scale);
            var ink = ImGui.GetColorU32(DropInk with { W = 1f - age / DropLife });
            drawList.AddCircleFilled(drop, dropSize, ink, 12);
            drawList.AddTriangleFilled(drop + new Vector2(-dropSize, 0f), drop + new Vector2(0f, -dropSize * 2.6f),
                drop + new Vector2(dropSize, 0f), ink);
        }
    }

    private static float Beat(float elapsed, float at)
    {
        var offset = (elapsed - at) / BeatWidth;
        return MathF.Exp(-offset * offset);
    }
}
