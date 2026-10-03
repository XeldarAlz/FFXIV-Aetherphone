using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal readonly record struct MapCameraInput(bool Hovered, bool Activated, bool Active, bool DoubleClicked);

internal sealed class MapCamera
{
    private const float DragSlop = 6f;
    private const float WheelStep = 0.2f;
    private const float DoubleClickZoom = 2f;
    private const float SettleTime = Motion.PageSettle;

    private Spring centerU = new(0.5f);
    private Spring centerV = new(0.5f);
    private Spring size = new(1f);
    private float targetU = 0.5f;
    private float targetV = 0.5f;
    private float targetSize = 1f;
    private bool initialized;
    private bool pressed;
    private float travel;
    private Vector2 lastMouse;
    private Rect screen;
    private Vector2 focus;

    public bool Following { get; private set; } = true;

    public bool Dragging { get; private set; }

    public float Size => size.Value;

    public void Reset()
    {
        initialized = false;
        Following = true;
        pressed = false;
        Dragging = false;
    }

    public void Recenter()
    {
        Following = true;
    }

    public void FocusOn(float u, float v, float minimumSize)
    {
        Following = false;
        targetU = u;
        targetV = v;
        targetSize = MathF.Max(targetSize, minimumSize);
    }

    public void Overview(float overviewSize)
    {
        Following = false;
        targetU = 0.5f;
        targetV = 0.5f;
        targetSize = overviewSize;
    }

    public bool Update(Rect stage, Rect visible, in MapCameraInput input, float minimumSize, float maximumSize,
        float defaultSize, bool hasTarget, float followU, float followV, float delta)
    {
        screen = stage;
        focus = visible.Center;
        if (!initialized)
        {
            initialized = true;
            targetSize = defaultSize;
            targetU = hasTarget ? followU : 0.5f;
            targetV = hasTarget ? followV : 0.5f;
            Following = hasTarget;
            targetSize = Math.Clamp(targetSize, minimumSize, maximumSize);
            ClampTargets();
            centerU.SnapTo(targetU);
            centerV.SnapTo(targetV);
            size.SnapTo(targetSize);
        }

        if (Following && hasTarget)
        {
            targetU = followU;
            targetV = followV;
        }

        var tapped = HandlePointer(in input, minimumSize, maximumSize);
        targetSize = Math.Clamp(targetSize, minimumSize, maximumSize);
        ClampTargets();
        if (Dragging)
        {
            centerU.SnapTo(targetU);
            centerV.SnapTo(targetV);
            size.SnapTo(targetSize);
            return tapped;
        }

        size.Step(targetSize, SettleTime, delta);
        centerU.Step(targetU, SettleTime, delta);
        centerV.Step(targetV, SettleTime, delta);
        KeepCovered();
        return tapped;
    }

    private void KeepCovered()
    {
        var clampedU = ClampAxis(centerU.Value, focus.X - screen.Min.X, screen.Max.X - focus.X, size.Value);
        var clampedV = ClampAxis(centerV.Value, focus.Y - screen.Min.Y, screen.Max.Y - focus.Y, size.Value);
        if (clampedU != centerU.Value)
        {
            centerU.SnapTo(clampedU);
        }

        if (clampedV != centerV.Value)
        {
            centerV.SnapTo(clampedV);
        }
    }

    public Vector2 ToScreen(float u, float v) =>
        new(focus.X + (u - centerU.Value) * size.Value, focus.Y + (v - centerV.Value) * size.Value);

    public Vector2 UvAt(Vector2 point) =>
        new(centerU.Value + (point.X - focus.X) / size.Value, centerV.Value + (point.Y - focus.Y) / size.Value);

    private bool HandlePointer(in MapCameraInput input, float minimumSize, float maximumSize)
    {
        var mouse = ImGui.GetMousePos();
        if (input.Hovered)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                ZoomAround(mouse, targetSize * (1f + wheel * WheelStep), minimumSize, maximumSize);
            }

            if (input.DoubleClicked)
            {
                ZoomAround(mouse, targetSize * DoubleClickZoom, minimumSize, maximumSize);
            }
        }

        if (input.Activated)
        {
            pressed = true;
            travel = 0f;
            lastMouse = mouse;
        }

        if (!pressed)
        {
            return false;
        }

        if (input.Active && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var step = mouse - lastMouse;
            lastMouse = mouse;
            travel += step.Length();
            if (!Dragging && travel > DragSlop * UiScale.Current)
            {
                Dragging = true;
                Following = false;
                UiInteract.CancelPendingTap();
            }

            if (Dragging)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                targetU -= step.X / MathF.Max(targetSize, 1f);
                targetV -= step.Y / MathF.Max(targetSize, 1f);
            }

            return false;
        }

        pressed = false;
        var wasDragging = Dragging;
        Dragging = false;
        return !wasDragging;
    }

    private void ZoomAround(Vector2 point, float wanted, float minimumSize, float maximumSize)
    {
        var next = Math.Clamp(wanted, minimumSize, maximumSize);
        if (MathF.Abs(next - targetSize) < 0.01f)
        {
            return;
        }

        if (!Following)
        {
            var offset = point - focus;
            var anchorU = targetU + offset.X / targetSize;
            var anchorV = targetV + offset.Y / targetSize;
            targetU = anchorU - offset.X / next;
            targetV = anchorV - offset.Y / next;
        }

        targetSize = next;
    }

    private void ClampTargets()
    {
        targetU = ClampAxis(targetU, focus.X - screen.Min.X, screen.Max.X - focus.X, targetSize);
        targetV = ClampAxis(targetV, focus.Y - screen.Min.Y, screen.Max.Y - focus.Y, targetSize);
    }

    private static float ClampAxis(float center, float before, float after, float drawnSize)
    {
        var lower = before / MathF.Max(drawnSize, 1f);
        var upper = 1f - after / MathF.Max(drawnSize, 1f);
        return lower > upper ? (lower + upper) * 0.5f : Math.Clamp(center, lower, upper);
    }
}
