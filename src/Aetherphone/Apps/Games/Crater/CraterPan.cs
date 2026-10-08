using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crater;

internal struct CraterPan
{
    private const float MinZoom = 0.0001f;

    private Vector2 origin;
    private ImGuiMouseButton button;
    private bool dragging;

    public void Track(in Camera2D camera, bool allowed, bool leftPans, bool leftPressed, bool rightPressed)
    {
        if (!allowed || (dragging && button == ImGuiMouseButton.Left && !leftPans))
        {
            dragging = false;
            return;
        }

        if (!dragging)
        {
            var left = leftPans && leftPressed;
            if (!left && !rightPressed)
            {
                return;
            }

            dragging = true;
            button = left ? ImGuiMouseButton.Left : ImGuiMouseButton.Right;
            origin = camera.Origin;
            return;
        }

        if (!ImGui.IsMouseDown(button))
        {
            dragging = false;
            return;
        }

        origin -= ImGui.GetIO().MouseDelta / MathF.Max(MinZoom, camera.EffectiveZoom);
        ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
    }

    public void Release()
    {
        dragging = false;
    }

    public bool Steer(ref Camera2D camera, float waterLevel)
    {
        if (!dragging)
        {
            return false;
        }

        origin = CraterFocus.KeepInView(in camera, origin, false, waterLevel);
        camera.Place(origin);
        return true;
    }
}
