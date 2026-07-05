using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Input;

internal sealed class DragTracker
{
    private bool active;
    private Vector2 origin;
    private Vector2 last;
    private float velocityY;
    public bool Active => active;
    public Vector2 Origin => origin;
    public Vector2 Delta => active ? ImGui.GetMousePos() - origin : Vector2.Zero;
    public float VelocityY => velocityY;

    public bool Begin(Rect startZone)
    {
        if (active || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        var position = ImGui.GetMousePos();
        if (!startZone.Contains(position))
        {
            return false;
        }

        active = true;
        origin = position;
        last = position;
        velocityY = 0f;
        return true;
    }

    public void Track(float deltaSeconds)
    {
        if (!active)
        {
            return;
        }

        var position = ImGui.GetMousePos();
        if (deltaSeconds > 0f)
        {
            velocityY = (position.Y - last.Y) / deltaSeconds;
        }

        last = position;
    }

    public bool Released(out Vector2 totalDelta, out float velocity)
    {
        totalDelta = Delta;
        velocity = velocityY;
        if (!active)
        {
            return false;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            active = false;
            return true;
        }

        return false;
    }

    public void Cancel() => active = false;
}
