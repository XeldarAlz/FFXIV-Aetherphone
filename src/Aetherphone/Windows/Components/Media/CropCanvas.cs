using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal sealed class CropCanvas
{
    private const float SmoothTime = 0.10f;
    private const float RatioEpsilon = 0.0001f;
    private const float RestEpsilon = 0.001f;
    private const float WheelZoomStep = 0.12f;
    private const float MaxDeltaSeconds = 0.1f;

    private Spring zoomSpring = new(WallpaperCrop.MinZoom);
    private Spring centerXSpring = new(0.5f);
    private Spring centerYSpring = new(0.5f);
    private float targetZoom = WallpaperCrop.MinZoom;
    private float targetCenterX = 0.5f;
    private float targetCenterY = 0.5f;
    private bool dragging;
    private Vector2 lastDrag;
    private float? framedForRatio;

    public WallpaperCrop Target => new(targetZoom, targetCenterX, targetCenterY);

    public void Load(WallpaperCrop crop)
    {
        targetZoom = crop.Zoom;
        targetCenterX = crop.CenterX;
        targetCenterY = crop.CenterY;
        zoomSpring.SnapTo(crop.Zoom);
        centerXSpring.SnapTo(crop.CenterX);
        centerYSpring.SnapTo(crop.CenterY);
        dragging = false;
    }

    public void Reset()
    {
        Load(WallpaperCrop.Cover);
        framedForRatio = null;
    }

    public static float MinZoom(Vector2 imageSize, float aspect, bool allowReveal)
    {
        return allowReveal ? WallpaperCrop.MinZoomToReveal(imageSize, aspect) : WallpaperCrop.MinZoom;
    }

    public bool IsAtRest(Vector2 imageSize, float aspect, bool allowReveal)
    {
        var minZoom = MinZoom(imageSize, aspect, allowReveal);
        var clamped = Target.Clamped(imageSize, aspect, minZoom);
        return MathF.Abs(clamped.Zoom - minZoom) < RestEpsilon
            && MathF.Abs(clamped.CenterX - 0.5f) < RestEpsilon
            && MathF.Abs(clamped.CenterY - 0.5f) < RestEpsilon;
    }

    public float ZoomFraction(Vector2 imageSize, float aspect, bool allowReveal)
    {
        var minZoom = MinZoom(imageSize, aspect, allowReveal);
        var range = WallpaperCrop.MaxZoom - minZoom;
        return range > 0f ? Math.Clamp((targetZoom - minZoom) / range, 0f, 1f) : 0f;
    }

    public void SetZoomFraction(float fraction, Vector2 imageSize, float aspect, bool allowReveal)
    {
        var minZoom = MinZoom(imageSize, aspect, allowReveal);
        targetZoom = minZoom + (Math.Clamp(fraction, 0f, 1f) * (WallpaperCrop.MaxZoom - minZoom));
        ClampTargets(imageSize, aspect, minZoom);
    }

    public Rect Draw(Rect stage, IDalamudTextureWrap texture, float aspect, bool allowReveal, float rounding,
        bool interactive)
    {
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, MaxDeltaSeconds);
        var drawList = ImGui.GetWindowDrawList();
        var preview = ImageFit.CenteredRect(stage, aspect);
        var size = texture.Size;
        var minZoom = MinZoom(size, aspect, allowReveal);
        ReframeOnAspectChange(aspect, minZoom);
        var zoom = zoomSpring.Step(targetZoom, SmoothTime, deltaSeconds);
        var centerX = centerXSpring.Step(targetCenterX, SmoothTime, deltaSeconds);
        var centerY = centerYSpring.Step(targetCenterY, SmoothTime, deltaSeconds);
        var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, aspect, minZoom);
        var (uv0, uv1) = crop.ComputeUv(size, aspect, minZoom);
        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        if (interactive)
        {
            HandleGestures(preview, size, uv1 - uv0, aspect, minZoom);
        }

        return preview;
    }

    private void ReframeOnAspectChange(float aspect, float minZoom)
    {
        if (framedForRatio is { } prior && MathF.Abs(prior - aspect) < RatioEpsilon)
        {
            return;
        }

        var firstFrame = framedForRatio is null;
        framedForRatio = aspect;
        if (firstFrame)
        {
            return;
        }

        targetZoom = minZoom;
        targetCenterX = 0.5f;
        targetCenterY = 0.5f;
        zoomSpring.SnapTo(targetZoom);
        centerXSpring.SnapTo(targetCenterX);
        centerYSpring.SnapTo(targetCenterY);
    }

    private void HandleGestures(Rect preview, Vector2 size, Vector2 visible, float aspect, float minZoom)
    {
        var hovering = UiInteract.Hover(preview.Min, preview.Max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                targetZoom = Math.Clamp(targetZoom * (1f + (wheel * WheelZoomStep)), minZoom, WallpaperCrop.MaxZoom);
            }
        }

        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            lastDrag = ImGui.GetMousePos();
        }

        if (dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var position = ImGui.GetMousePos();
                var delta = position - lastDrag;
                lastDrag = position;
                if (preview.Width > 0f && preview.Height > 0f)
                {
                    targetCenterX -= delta.X * visible.X / preview.Width;
                    targetCenterY -= delta.Y * visible.Y / preview.Height;
                }
            }
            else
            {
                dragging = false;
            }
        }

        ClampTargets(size, aspect, minZoom);
    }

    private void ClampTargets(Vector2 size, float aspect, float minZoom)
    {
        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, aspect, minZoom);
        targetZoom = clamped.Zoom;
        targetCenterX = clamped.CenterX;
        targetCenterY = clamped.CenterY;
    }
}
