using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class PhotoZoomView
{
    private const float MinZoom = 1f;
    private const float MaxZoom = 4f;
    private const float DoubleTapZoom = 2.5f;
    private const float ButtonStep = 1.5f;
    private const float WheelStep = 0.16f;
    private const float SmoothTime = 0.12f;
    private const float ButtonRadiusUnits = 17f;

    private Spring zoom = new(1f);
    private Spring panX;
    private Spring panY;
    private float targetZoom = 1f;
    private Vector2 targetPan;
    private bool dragging;
    private Vector2 lastDrag;

    public bool IsZoomed => targetZoom > 1.01f;

    public void Reset()
    {
        targetZoom = 1f;
        targetPan = Vector2.Zero;
        zoom.SnapTo(1f);
        panX.SnapTo(0f);
        panY.SnapTo(0f);
        dragging = false;
    }

    public void Draw(Rect stage, IDalamudTextureWrap texture, PhoneTheme theme, float rounding,
        bool showButtons = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var size = texture.Size;
        HandleInput(stage, size);
        zoom.Step(targetZoom, SmoothTime, delta);
        panX.Step(targetPan.X, SmoothTime, delta);
        panY.Step(targetPan.Y, SmoothTime, delta);
        var fit = FitScale(stage, size);
        var drawn = size * fit * zoom.Value;
        var center = stage.Center + new Vector2(panX.Value, panY.Value);
        var min = center - drawn * 0.5f;
        var max = center + drawn * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(stage.Min, stage.Max, true);
        var restRounding = rounding * Math.Clamp(2f - zoom.Value, 0f, 1f);
        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
            MathF.Max(restRounding, 0.5f), ImDrawFlags.RoundCornersAll);
        drawList.PopClipRect();
        if (showButtons)
        {
            DrawButtons(stage, size, theme, scale);
        }
    }

    private void HandleInput(Rect stage, Vector2 size)
    {
        var mouse = ImGui.GetMousePos();
        var hovering = ImGui.IsMouseHoveringRect(stage.Min, stage.Max);
        if (hovering)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                ZoomAround(stage, mouse, targetZoom * (1f + wheel * WheelStep), size);
            }

            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                if (targetZoom > 1.5f)
                {
                    targetZoom = MinZoom;
                    targetPan = Vector2.Zero;
                }
                else
                {
                    ZoomAround(stage, mouse, DoubleTapZoom, size);
                }
            }
        }

        if (hovering && IsZoomed && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            lastDrag = mouse;
        }

        if (!dragging)
        {
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            targetPan += mouse - lastDrag;
            lastDrag = mouse;
            ClampPan(stage, size);
            return;
        }

        dragging = false;
    }

    private void ZoomAround(Rect stage, Vector2 point, float newZoom, Vector2 size)
    {
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (MathF.Abs(newZoom - targetZoom) < 0.0001f)
        {
            return;
        }

        var anchor = point - stage.Center;
        var ratio = newZoom / targetZoom;
        targetPan = (targetPan - anchor) * ratio + anchor;
        targetZoom = newZoom;
        ClampPan(stage, size);
    }

    private void ClampPan(Rect stage, Vector2 size)
    {
        var drawn = size * FitScale(stage, size) * targetZoom;
        var maxPanX = MathF.Max(0f, (drawn.X - stage.Width) * 0.5f);
        var maxPanY = MathF.Max(0f, (drawn.Y - stage.Height) * 0.5f);
        targetPan = new Vector2(Math.Clamp(targetPan.X, -maxPanX, maxPanX),
            Math.Clamp(targetPan.Y, -maxPanY, maxPanY));
    }

    private static float FitScale(Rect stage, Vector2 size) =>
        MathF.Min(stage.Width / MathF.Max(size.X, 1f), stage.Height / MathF.Max(size.Y, 1f));

    private void DrawButtons(Rect stage, Vector2 size, PhoneTheme theme, float scale)
    {
        var radius = ButtonRadiusUnits * scale;
        var gap = 10f * scale;
        var centerX = stage.Max.X - radius - 12f * scale;
        var outCenter = new Vector2(centerX, stage.Max.Y - radius - 12f * scale);
        var inCenter = new Vector2(centerX, outCenter.Y - radius * 2f - gap);
        if (ZoomButton(inCenter, radius, true, targetZoom < MaxZoom - 0.01f, theme, scale))
        {
            ZoomAround(stage, stage.Center, targetZoom * ButtonStep, size);
        }

        if (ZoomButton(outCenter, radius, false, targetZoom > MinZoom + 0.01f, theme, scale))
        {
            ZoomAround(stage, stage.Center, targetZoom / ButtonStep, size);
        }
    }

    private static bool ZoomButton(Vector2 center, float radius, bool zoomIn, bool enabled, PhoneTheme theme,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled &&
                      ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius),
                          center + new Vector2(radius, radius));
        var alpha = enabled ? hovered ? 0.30f : 0.20f : 0.10f;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)), 32, 1f * scale);
        var arm = radius * 0.44f;
        var ink = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, enabled ? 0.95f : 0.4f));
        drawList.AddLine(center - new Vector2(arm, 0f), center + new Vector2(arm, 0f), ink, 2f * scale);
        if (zoomIn)
        {
            drawList.AddLine(center - new Vector2(0f, arm), center + new Vector2(0f, arm), ink, 2f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
