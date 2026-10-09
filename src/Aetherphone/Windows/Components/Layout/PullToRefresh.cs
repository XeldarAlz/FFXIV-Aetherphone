using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class PullToRefresh
{
    private const float ArmThreshold = 64f;
    private const float MinSpinnerSeconds = 0.5f;
    private const float MaxSpinnerSeconds = 20f;
    private const float MoonRadius = 11f;
    private const float MoonCycleSpeed = 0.9f;
    private const float BatDrop = 28f;
    private const float BatSize = 1.1f;

    private static readonly Vector4 MoonLight = new(0.933f, 0.949f, 1f, 1f);
    private static readonly Vector4 MoonShadow = new(0.039f, 0.063f, 0.188f, 1f);
    private static readonly Vector4 MoonOutline = new(0.84f, 0.88f, 1f, 0.35f);
    private static readonly Vector4 MoonGlow = new(0.84f, 0.88f, 1f, 0.45f);
    private static readonly Vector4 BatInk = new(1f, 0.29f, 0.373f, 1f);
    private static readonly Vector4 ThreadInk = new(1f, 0.47f, 0.51f, 0.6f);

    private bool wasDragging;
    private bool armed;
    private bool refreshing;
    private float spinnerElapsed;

    public PullStyle Style { get; set; }

    public void Draw(Rect area, float pull, bool dragging, bool loading, Vector4 ink, Action onRefresh)
    {
        var scale = UiScale.Current;
        var deltaSeconds = ImGui.GetIO().DeltaTime;

        if (refreshing)
        {
            spinnerElapsed += deltaSeconds;
            if ((!loading && spinnerElapsed >= MinSpinnerSeconds) || spinnerElapsed >= MaxSpinnerSeconds)
            {
                refreshing = false;
            }
        }

        if (dragging)
        {
            wasDragging = true;
            armed = pull >= ArmThreshold * scale;
        }
        else if (wasDragging)
        {
            wasDragging = false;
            if (armed && !refreshing && !loading)
            {
                refreshing = true;
                spinnerElapsed = 0f;
                UiFeedback.Play(UiSound.Refresh);
                onRefresh();
            }

            armed = false;
        }

        DrawIndicator(area, pull, scale, ink);
    }

    private void DrawIndicator(Rect area, float pull, float scale, Vector4 ink)
    {
        var progress = refreshing ? 1f : Math.Clamp(pull / (ArmThreshold * scale), 0f, 1f);
        if (progress <= 0f)
        {
            return;
        }

        var centerX = area.Center.X;
        var centerY = area.Min.Y + 20f * scale;
        var drawList = ImGui.GetWindowDrawList();
        if (Style == PullStyle.Moon)
        {
            DrawMoon(drawList, new Vector2(centerX, centerY + 4f * scale), progress, scale);
            return;
        }

        if (Style == PullStyle.Bat)
        {
            DrawBat(drawList, new Vector2(centerX, area.Min.Y), progress, scale);
            return;
        }

        var dotRadius = 2.8f * scale;
        var dotGap = 6f * scale;
        var baseX = centerX - (dotRadius * 2f + dotGap);
        var phase = (float)ImGui.GetTime();
        for (var dot = 0; dot < 3; dot++)
        {
            float alpha;
            if (refreshing)
            {
                var wave = MathF.Max(0f, MathF.Sin(phase * 6f - dot * 0.9f));
                alpha = 0.30f + 0.55f * wave;
            }
            else
            {
                alpha = progress * (dot < progress * 3f ? 0.85f : 0.25f);
            }

            var center = new Vector2(baseX + dot * (dotRadius * 2f + dotGap), centerY);
            drawList.AddCircleFilled(center, dotRadius, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 16);
        }
    }

    private void DrawMoon(ImDrawListPtr drawList, Vector2 center, float progress, float scale)
    {
        var radius = MoonRadius * scale;
        var sweep = radius * 2.1f;
        float shadowOffset;
        if (refreshing)
        {
            var phase = (float)ImGui.GetTime() * MoonCycleSpeed % 1f;
            shadowOffset = phase < 0.5f ? -phase * 2f * sweep : (1f - (phase - 0.5f) * 2f) * sweep;
        }
        else
        {
            shadowOffset = -progress * sweep;
        }

        if (!refreshing && progress >= 1f)
        {
            NightScene.Glow(drawList, center, radius * 2.6f, MoonGlow, 8);
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(MoonOutline with { W = MoonOutline.W * progress }), 32,
            scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MoonLight with { W = progress }), 32);
        drawList.AddCircleFilled(center + new Vector2(shadowOffset, 0f), radius * 1.04f,
            ImGui.GetColorU32(MoonShadow), 32);
    }

    private void DrawBat(ImDrawListPtr drawList, Vector2 anchor, float progress, float scale)
    {
        var ink = ImGui.GetColorU32(BatInk with { W = progress });
        var time = (float)ImGui.GetTime();
        if (refreshing)
        {
            var angle = time * 5f;
            var circling = anchor + new Vector2(MathF.Cos(angle) * 16f, 22f + MathF.Sin(angle) * 8f) * scale;
            NightScene.DrawBat(drawList, circling, BatSize * scale, MathF.Sin(time * 18f), ink);
            return;
        }

        var hang = anchor + new Vector2(0f, (6f + progress * BatDrop) * scale);
        drawList.AddLine(anchor, hang, ImGui.GetColorU32(ThreadInk with { W = ThreadInk.W * progress }), scale);
        NightScene.DrawBat(drawList, hang + new Vector2(0f, 5f * scale), -BatSize * scale,
            progress >= 1f ? MathF.Sin(time * 18f) : 0.4f, ink);
    }
}
