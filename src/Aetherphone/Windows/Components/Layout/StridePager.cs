using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class StridePager
{
    private const float SwipeSlop = 10f;
    private const int LoopCopies = 3;
    private const float SettleEpsilon = 0.001f;
    private const float ManualPauseSeconds = 4f;

    private readonly Pager pager = new();
    private readonly bool loop;
    private bool pressed;
    private Vector2 pressPosition;
    private float wheelCarry;
    private float quiet;

    public StridePager(bool loop = false)
    {
        this.loop = loop;
    }

    public float Value => pager.Value;
    public int Page => pager.Page;
    public bool Dragging => pager.Dragging;
    public bool Owning => pressed || pager.Dragging;

    public static int CardIndex(int virtualIndex, int count) =>
        count <= 0 ? 0 : (virtualIndex % count + count) % count;

    public static int WrapPage(int page, int count) => count <= 0 ? 0 : count + CardIndex(page, count);

    public void Reset()
    {
        pager.SnapTo(0, 1);
        pressed = false;
        wheelCarry = 0f;
        quiet = 0f;
    }

    public void Step(float delta, int count)
    {
        pager.Step(delta, Pages(count));
        if (loop)
        {
            Wrap(count);
        }
    }

    public void AnimateTo(int page, int count) => pager.AnimateTo(page, Pages(count));

    public void AutoAdvance(Rect row, int count, float delta, float interval)
    {
        if (count <= 1)
        {
            quiet = 0f;
            return;
        }

        if (Owning || UiInteract.Hover(row.Min, row.Max))
        {
            quiet = MathF.Min(quiet, 0f);
            return;
        }

        quiet += delta;
        if (quiet < interval)
        {
            return;
        }

        quiet = 0f;
        pager.AnimateTo(pager.Page + 1, Pages(count));
    }

    public void Drive(string id, Rect row, float stride, int count, float delta, float scale)
    {
        var activated = SwipeClaim.Claim(id, row);
        Wheel(row, count);
        var mouse = ImGui.GetMousePos();
        if (count > 1 && !pressed && activated && !UiInteract.InputBlocked)
        {
            pressed = true;
            pressPosition = mouse;
        }

        if (!pressed)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (pager.Dragging)
            {
                pager.Release(stride, Pages(count));
                quiet = -ManualPauseSeconds;
                UiInteract.BlockThisFrame();
            }

            pressed = false;
            return;
        }

        var move = mouse - pressPosition;
        if (!pager.Dragging && MathF.Abs(move.X) > SwipeSlop * scale && MathF.Abs(move.X) > MathF.Abs(move.Y))
        {
            pager.Begin(pressPosition.X);
            UiInteract.CancelPendingTap();
        }

        if (!pager.Dragging)
        {
            return;
        }

        pager.Drag(mouse.X, stride, Pages(count), delta);
        UiInteract.BlockThisFrame();
    }

    private int Pages(int count) => loop && count > 1 ? count * LoopCopies : count;

    private void Wrap(int count)
    {
        if (count <= 1 || Owning || MathF.Abs(pager.Value - pager.Page) > SettleEpsilon)
        {
            return;
        }

        var wrapped = WrapPage(pager.Page, count);
        if (wrapped != pager.Page)
        {
            pager.SnapTo(wrapped, Pages(count));
        }
    }

    private void Wheel(Rect row, int count)
    {
        if (count <= 1 || pager.Dragging || !UiInteract.Hover(row.Min, row.Max))
        {
            wheelCarry = 0f;
            return;
        }

        var io = ImGui.GetIO();
        wheelCarry += io.MouseWheelH != 0f ? -io.MouseWheelH : io.KeyShift ? -io.MouseWheel : 0f;
        if (MathF.Abs(wheelCarry) < 1f)
        {
            return;
        }

        var step = MathF.Sign(wheelCarry);
        wheelCarry -= step;
        quiet = -ManualPauseSeconds;
        pager.AnimateTo(pager.Page + step, Pages(count));
    }
}
