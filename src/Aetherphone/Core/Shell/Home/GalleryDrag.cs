using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal enum GalleryDragPhase : byte
{
    Idle,
    Dragging,
    Released,
}

internal sealed class GalleryDrag
{
    private const float SlopUnits = 6f;
    private const float VelocitySmoothing = 0.6f;

    private bool pressed;
    private bool claimed;
    private Vector2 pressMouse;
    private float lastMouseX;

    public float Velocity { get; private set; }

    public bool Claimed => claimed;

    public void Reset()
    {
        pressed = false;
        claimed = false;
        Velocity = 0f;
    }

    public GalleryDragPhase Track(Rect area, bool interactive, float scale, float delta, out float deltaX)
    {
        deltaX = 0f;
        var mouse = ImGui.GetMousePos();
        if (!pressed)
        {
            if (!interactive || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(area.Min, area.Max))
            {
                return GalleryDragPhase.Idle;
            }

            pressed = true;
            claimed = false;
            pressMouse = mouse;
            lastMouseX = mouse.X;
            Velocity = 0f;
            return GalleryDragPhase.Idle;
        }

        if (!interactive || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var wasClaimed = claimed;
            pressed = false;
            claimed = false;
            return wasClaimed ? GalleryDragPhase.Released : GalleryDragPhase.Idle;
        }

        if (!claimed && !TryClaim(mouse, scale))
        {
            return GalleryDragPhase.Idle;
        }

        deltaX = mouse.X - lastMouseX;
        lastMouseX = mouse.X;
        var instant = delta > 0f ? deltaX / delta : 0f;
        Velocity += (instant - Velocity) * VelocitySmoothing;
        return GalleryDragPhase.Dragging;
    }

    private bool TryClaim(Vector2 mouse, float scale)
    {
        var travel = mouse - pressMouse;
        var slop = SlopUnits * scale;
        var horizontal = MathF.Abs(travel.X);
        var vertical = MathF.Abs(travel.Y);
        if (vertical >= slop && vertical > horizontal)
        {
            pressed = false;
            return false;
        }

        if (horizontal < slop)
        {
            return false;
        }

        claimed = true;
        lastMouseX = pressMouse.X;
        UiInteract.CancelPendingTap();
        return true;
    }
}
