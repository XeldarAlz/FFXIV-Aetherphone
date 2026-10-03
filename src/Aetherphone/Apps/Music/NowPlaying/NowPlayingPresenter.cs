using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.NowPlaying;

internal sealed class NowPlayingPresenter
{
    private const float DismissFraction = 0.22f;
    private const float FlingFractionPerSecond = 1.6f;
    private const float DragThreshold = 6f;
    private const float RestEpsilon = 0.0015f;
    private const float RestVelocity = 0.02f;
    private const float VelocitySmoothing = 0.6f;

    private Spring hidden = new(1f);
    private bool open;
    private bool pressed;
    private bool dragging;
    private float pressMouseY;
    private float pressHidden;
    private float lastMouseY;
    private float velocity;
    private int openedFrame = -1;

    public bool IsOpen => open;

    public bool IsDragging => dragging;

    public bool OpenedThisFrame => openedFrame == ImGui.GetFrameCount();

    public bool CapturesPointer => open || !hidden.IsResting(1f, RestEpsilon, RestVelocity);

    public float Openness => 1f - Math.Clamp(hidden.Value, 0f, 1f);

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        pressed = false;
        dragging = false;
        openedFrame = ImGui.GetFrameCount();
        UiFeedback.Play(UiSound.SheetPresent);
    }

    public void Close()
    {
        if (!open)
        {
            return;
        }

        open = false;
        pressed = false;
        dragging = false;
        UiFeedback.Play(UiSound.SheetDismiss);
    }

    public void CloseImmediately()
    {
        open = false;
        pressed = false;
        dragging = false;
        hidden.SnapTo(1f);
    }

    public void ReleasePress()
    {
        if (dragging)
        {
            return;
        }

        pressed = false;
    }

    public void Step(float travel, bool pressInDragZone, float delta, float scale)
    {
        if (open)
        {
            TrackDrag(travel, pressInDragZone, delta, scale);
        }

        if (dragging)
        {
            return;
        }

        hidden.Step(open ? 0f : 1f, Motion.Sheet, delta);
        if (!open && hidden.IsResting(1f, RestEpsilon, RestVelocity))
        {
            hidden.SnapTo(1f);
        }
    }

    private void TrackDrag(float travel, bool pressInDragZone, float delta, float scale)
    {
        var mouseY = ImGui.GetMousePos().Y;
        if (!pressed)
        {
            if (!pressInDragZone || OpenedThisFrame || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                return;
            }

            pressed = true;
            dragging = false;
            pressMouseY = mouseY;
            pressHidden = hidden.Value;
            lastMouseY = mouseY;
            velocity = 0f;
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var moved = mouseY - pressMouseY;
            if (!dragging && moved >= DragThreshold * scale)
            {
                dragging = true;
                UiInteract.CancelPendingTap();
            }

            if (!dragging)
            {
                return;
            }

            UiInteract.BlockThisFrame();
            var span = MathF.Max(1f, travel);
            var instant = delta > 0f ? (mouseY - lastMouseY) / span / delta : 0f;
            velocity += (instant - velocity) * VelocitySmoothing;
            lastMouseY = mouseY;
            hidden.SnapTo(Math.Clamp(pressHidden + moved / span, 0f, 1f));
            return;
        }

        pressed = false;
        if (!dragging)
        {
            return;
        }

        dragging = false;
        hidden.Velocity = velocity;
        if (hidden.Value > DismissFraction || velocity > FlingFractionPerSecond)
        {
            Close();
        }
    }
}
