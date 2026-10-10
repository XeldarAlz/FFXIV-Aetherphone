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
    private const float MoonCycleSpeed = 0.55f;
    private const float MoonFadeSeconds = 0.3f;
    private const float BatDrop = 28f;
    private const float BatSize = 1.1f;

    private static readonly Vector4 MoonLight = new(0.933f, 0.949f, 1f, 1f);
    private static readonly Vector4 MoonEarthshine = new(0.84f, 0.88f, 1f, 0.14f);
    private static readonly Vector4 MoonOutline = new(0.84f, 0.88f, 1f, 0.35f);
    private static readonly Vector4 MoonGlow = new(0.84f, 0.88f, 1f, 0.45f);
    private static readonly Vector4 BatInk = new(1f, 0.29f, 0.373f, 1f);
    private static readonly Vector4 ThreadInk = new(1f, 0.47f, 0.51f, 0.6f);

    private bool wasDragging;
    private bool armed;
    private bool refreshing;
    private float spinnerElapsed;
    private float moonPhase;
    private float moonFade;

    public PullStyle Style { get; set; }

    public UiSound RefreshSound { get; set; } = UiSound.Refresh;

    public UiSound? ArmSound { get; set; }

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
                moonFade = 1f;
            }
        }

        if (dragging)
        {
            wasDragging = true;
            var reached = pull >= ArmThreshold * scale;
            if (reached && !armed && ArmSound is { } armSound)
            {
                UiFeedback.Play(armSound);
            }

            armed = reached;
        }
        else if (wasDragging)
        {
            wasDragging = false;
            if (armed && !refreshing && !loading)
            {
                refreshing = true;
                spinnerElapsed = 0f;
                UiFeedback.Play(RefreshSound);
                onRefresh();
            }

            armed = false;
        }

        DrawIndicator(area, pull, scale, ink);
    }

    private void DrawIndicator(Rect area, float pull, float scale, Vector4 ink)
    {
        var progress = refreshing ? 1f : Math.Clamp(pull / (ArmThreshold * scale), 0f, 1f);
        var centerX = area.Center.X;
        var centerY = area.Min.Y + 20f * scale;
        var drawList = ImGui.GetWindowDrawList();
        if (Style == PullStyle.Moon)
        {
            DrawMoon(drawList, new Vector2(centerX, centerY + 4f * scale), progress, scale);
            return;
        }

        if (progress <= 0f)
        {
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
        var deltaSeconds = ImGui.GetIO().DeltaTime;
        float alpha;
        if (progress > 0f)
        {
            moonFade = 0f;
            alpha = progress;
            moonPhase = refreshing ? (moonPhase + deltaSeconds * MoonCycleSpeed) % 1f : progress * 0.5f;
        }
        else if (moonFade > 0f)
        {
            moonFade = MathF.Max(0f, moonFade - deltaSeconds / MoonFadeSeconds);
            alpha = moonFade;
            moonPhase = (moonPhase + deltaSeconds * MoonCycleSpeed) % 1f;
        }
        else
        {
            return;
        }

        var radius = MoonRadius * scale;
        if (!refreshing && progress >= 1f)
        {
            NightScene.Glow(drawList, center, radius * 2.6f, MoonGlow, 8);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MoonEarthshine with { W = MoonEarthshine.W * alpha }),
            32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(MoonOutline with { W = MoonOutline.W * alpha }), 32, scale);
        MoonPhase.Draw(drawList, center, radius, moonPhase, ImGui.GetColorU32(MoonLight with { W = alpha }));
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
