using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.NowPlaying;

internal sealed class PaneScroll
{
    private const float WheelStep = 56f;

    private readonly KineticScroller scroller = new();
    private bool pressed;

    public float Offset => scroller.Offset;

    public bool Dragging => scroller.IsDragging;

    public void Reset()
    {
        scroller.Reset();
        pressed = false;
    }

    public void Sync(float offset) => scroller.SyncOffset(offset);

    public bool Update(Rect area, float contentHeight, float maximum, bool interactive, bool blockPress, float delta)
    {
        var scale = UiScale.Current;
        scroller.Scale = scale;
        scroller.SetBounds(MathF.Max(0f, maximum));
        var userScrolled = false;
        var hovered = interactive && UiInteract.Hover(area.Min, area.Max);
        var wheel = hovered ? ImGui.GetIO().MouseWheel : 0f;
        if (wheel != 0f && contentHeight > area.Height)
        {
            scroller.CancelMomentum();
            scroller.SyncOffset(scroller.Offset - wheel * WheelStep * scale);
            userScrolled = true;
        }

        if (!pressed && hovered && !blockPress && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            scroller.Press(ImGui.GetMousePos().Y);
            pressed = true;
        }

        if (pressed)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                scroller.Move(ImGui.GetMousePos().Y, delta);
                if (scroller.IsDragging)
                {
                    UiInteract.CancelPendingTap();
                    userScrolled = true;
                }
            }
            else
            {
                scroller.Release();
                pressed = false;
            }
        }

        scroller.Tick(delta);
        return userScrolled || scroller.IsControlling;
    }
}
