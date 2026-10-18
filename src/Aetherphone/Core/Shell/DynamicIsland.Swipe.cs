using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed partial class DynamicIsland
{
    private const float SwipeSlop = 10f;
    private const float SwipeFadeDistance = 120f;
    private const float SwipeReturnRate = 18f;
    private const float SwipeExitSpeed = 900f;

    private bool swiping;
    private bool swipeMoved;
    private bool swipedAway;
    private bool pcMediaSwipedAway;
    private float swipeStartX;
    private float swipeLastX;
    private float swipeVelocity;
    private float swipeOffset;
    private IslandActivity swipedKind = IslandActivity.None;

    private bool SwipeableNow() =>
        IslandActivities.Swipeable(shownKind,
            shownKind == IslandActivity.PcMedia ? pcSnapshot.IsPlaying : playback.IsPlaying);

    private void StepSwipe(float delta, float scale)
    {
        if (swipedAway)
        {
            if (shownKind != swipedKind || !SwipeableNow())
            {
                ResetSwipe();
                return;
            }

            swipeOffset += SwipeExitSpeed * scale * delta;
            return;
        }

        if (!swiping)
        {
            swipeOffset += (0f - swipeOffset) * MathF.Min(1f, delta * SwipeReturnRate);
        }
    }

    private float SwipeFade(float scale) =>
        1f - Math.Clamp(swipeOffset / (SwipeFadeDistance * scale), 0f, 1f);

    private static Rect Shift(Rect rect, float offset) =>
        offset == 0f ? rect : new Rect(rect.Min + new Vector2(offset, 0f), rect.Max + new Vector2(offset, 0f));

    private bool HandleSwipe(bool hovered, float scale)
    {
        if (!swiping)
        {
            if (hovered && !swipedAway && SwipeableNow() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                swiping = true;
                swipeMoved = false;
                swipeStartX = ImGui.GetMousePos().X;
                swipeLastX = swipeStartX;
                swipeVelocity = 0f;
                UiInteract.ReportDragSurface();
            }

            return false;
        }

        UiInteract.ReportDragSurface();
        var mouseX = ImGui.GetMousePos().X;
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var frame = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
            if (frame > 0f)
            {
                swipeVelocity = (mouseX - swipeLastX) / frame;
            }

            swipeLastX = mouseX;
            var moved = mouseX - swipeStartX;
            if (!swipeMoved && MathF.Abs(moved) >= SwipeSlop * scale)
            {
                swipeMoved = true;
                UiInteract.CancelPendingTap();
            }

            swipeOffset = swipeMoved ? MathF.Max(0f, moved) : 0f;
            return swipeMoved;
        }

        swiping = false;
        if (!swipeMoved)
        {
            return false;
        }

        swipeMoved = false;
        if (IslandActivities.SwipeDismisses(swipeOffset, swipeVelocity, scale) && SwipeableNow())
        {
            SwipeAway();
        }

        return true;
    }

    private void SwipeAway()
    {
        swipedAway = true;
        swipedKind = shownKind;
        UiFeedback.Play(UiSound.IslandCollapse);
        if (shownKind == IslandActivity.PcMedia)
        {
            pcMediaSwipedAway = true;
            return;
        }

        playback.Stop();
    }

    private void ResetSwipe()
    {
        swiping = false;
        swipeMoved = false;
        swipedAway = false;
        swipeOffset = 0f;
        swipedKind = IslandActivity.None;
    }
}
