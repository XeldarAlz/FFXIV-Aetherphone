using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class ScreenFx
{
    private const float FlashDecay = 3.2f;
    private const float PlatePunchSeconds = 0.2f;
    private const float EdgeGlowDecay = 2.4f;
    private const float EdgeBandFraction = 0.18f;
    private const float EdgeGlowAlpha = 0.34f;
    private const int CameraGraceFrames = 2;

    private readonly StageBackdrop backdrop;
    private Vector4 flashColor;
    private float flashAlpha;
    private Vector4 vignetteColor;
    private float vignetteStrength;
    private float vignetteSeconds;
    private float vignetteTotal;
    private float edgeGlow;
    private float edgeGlowTarget;
    private float slowFactor = 1f;
    private float slowSeconds;
    private float platePunch;
    private float platePunchElapsed = PlatePunchSeconds;
    private float pendingPunch;
    private int cameraFrames;

    public ScreenFx(StageBackdrop backdrop)
    {
        this.backdrop = backdrop;
    }

    public float TimeScale => slowSeconds > 0f ? slowFactor : 1f;

    public float PlateScale =>
        platePunchElapsed >= PlatePunchSeconds ? 1f : 1f + platePunch * (1f - platePunchElapsed / PlatePunchSeconds);

    public float EdgeHeat => edgeGlow;

    public void Clear()
    {
        flashAlpha = 0f;
        vignetteSeconds = 0f;
        vignetteStrength = 0f;
        edgeGlow = 0f;
        edgeGlowTarget = 0f;
        slowSeconds = 0f;
        slowFactor = 1f;
        platePunchElapsed = PlatePunchSeconds;
        pendingPunch = 0f;
        cameraFrames = 0;
    }

    public void Flash(Vector4 color, float alpha)
    {
        flashColor = color;
        flashAlpha = MathF.Max(flashAlpha, alpha);
    }

    public void Vignette(Vector4 color, float strength, float seconds)
    {
        vignetteColor = color;
        vignetteStrength = strength;
        vignetteSeconds = seconds;
        vignetteTotal = MathF.Max(0.0001f, seconds);
    }

    public void EdgeGlow(float strength)
    {
        edgeGlowTarget = MathF.Max(edgeGlowTarget, Math.Clamp(strength, 0f, 1f));
    }

    public void SlowMo(float factor, float seconds)
    {
        slowFactor = Math.Clamp(factor, 0.05f, 1f);
        slowSeconds = MathF.Max(slowSeconds, seconds);
    }

    public void Punch(float amount)
    {
        if (cameraFrames > 0)
        {
            pendingPunch += amount;
            return;
        }

        platePunch = MathF.Max(amount, PlateScale - 1f);
        platePunchElapsed = 0f;
    }

    public void Sweep()
    {
        backdrop.Sweep(1f);
    }

    public void ApplyTo(ref Camera2D camera)
    {
        cameraFrames = CameraGraceFrames;
        if (pendingPunch <= 0f)
        {
            return;
        }

        camera.Punch(pendingPunch);
        pendingPunch = 0f;
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        cameraFrames = Math.Max(0, cameraFrames - 1);
        flashAlpha = MathF.Max(0f, flashAlpha - deltaSeconds * FlashDecay);
        vignetteSeconds = MathF.Max(0f, vignetteSeconds - deltaSeconds);
        slowSeconds = MathF.Max(0f, slowSeconds - deltaSeconds);
        platePunchElapsed = MathF.Min(PlatePunchSeconds, platePunchElapsed + deltaSeconds);
        edgeGlow = edgeGlowTarget > edgeGlow
            ? edgeGlowTarget
            : MathF.Max(edgeGlowTarget, edgeGlow - deltaSeconds * EdgeGlowDecay);
        edgeGlowTarget = 0f;
    }

    public void Draw(ImDrawListPtr drawList, Rect full, Vector4 accent)
    {
        if (edgeGlow > 0.01f)
        {
            DrawEdges(drawList, full, accent, edgeGlow * EdgeGlowAlpha);
        }

        if (vignetteSeconds > 0f && vignetteStrength > 0f)
        {
            var remaining = vignetteSeconds / vignetteTotal;
            DrawEdges(drawList, full, vignetteColor, vignetteStrength * remaining);
        }

        if (flashAlpha <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(full.Min, full.Max, ImGui.GetColorU32(flashColor with { W = flashColor.W * flashAlpha }));
    }

    private static void DrawEdges(ImDrawListPtr drawList, Rect full, Vector4 color, float alpha)
    {
        var lit = ImGui.GetColorU32(color with { W = alpha });
        var clear = ImGui.GetColorU32(color with { W = 0f });
        var bandX = full.Width * EdgeBandFraction;
        var bandY = full.Height * EdgeBandFraction;
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Min.X + bandX, full.Max.Y), lit, clear, clear, lit);
        drawList.AddRectFilledMultiColor(new Vector2(full.Max.X - bandX, full.Min.Y), full.Max, clear, lit, lit, clear);
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Max.X, full.Min.Y + bandY), lit, lit, clear, clear);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, full.Max.Y - bandY), full.Max, clear, clear, lit, lit);
    }
}
