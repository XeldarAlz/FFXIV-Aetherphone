using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crater;

internal struct CraterPan
{
    private const float MinZoom = 0.0001f;

    private Vector2 origin;
    private bool dragging;

    public void Track(in Camera2D camera, bool allowed, bool pressed)
    {
        if (!allowed || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
            return;
        }

        if (!dragging)
        {
            dragging = pressed;
            origin = camera.Origin;
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
