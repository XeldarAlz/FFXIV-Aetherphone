using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class PanRail
{
    private readonly KineticScroller scroller = new();
    private bool pressed;
    private Rect row;

    public float Offset => scroller.Offset;

    public bool Dragging => scroller.IsDragging;

    public void Begin(Rect rail, float contentWidth)
    {
        row = rail;
        scroller.Scale = UiScale.Current;
        scroller.SetBounds(contentWidth - rail.Width);
        var io = ImGui.GetIO();
        var pointerX = io.MousePos.X;
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (pressed)
        {
            if (down)
            {
                var wasDragging = scroller.IsDragging;
                scroller.Move(pointerX, io.DeltaTime);
                if (!wasDragging && scroller.IsDragging)
                {
                    UiInteract.CancelPendingTap();
                }
            }
            else
            {
                scroller.Release();
                pressed = false;
                scroller.Tick(io.DeltaTime);
            }
        }
        else if (down && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(rail.Min, rail.Max))
        {
            scroller.Press(pointerX);
            pressed = true;
        }
        else
        {
            scroller.Tick(io.DeltaTime);
        }

        ImGui.GetWindowDrawList().PushClipRect(rail.Min, rail.Max, true);
    }

    public void End() => ImGui.GetWindowDrawList().PopClipRect();

    public bool Hover(Vector2 min, Vector2 max) =>
        !scroller.IsDragging && row.Contains(ImGui.GetMousePos()) && UiInteract.Hover(min, max);

    public bool Tapped(Vector2 min, Vector2 max, bool hovered) =>
        !scroller.IsDragging && UiInteract.Click(min, max, hovered);

    public void Reset()
    {
        scroller.Reset();
        pressed = false;
    }
}
