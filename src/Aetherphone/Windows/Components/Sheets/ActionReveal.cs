using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class ActionReveal<TPanel> where TPanel : struct, Enum
{
    private const float ClosedEpsilon = 0.01f;

    private string? targetId;
    private TPanel current;
    private bool closing;
    private Spring progress;
    private int openedFrame;

    public string? TargetId => targetId;
    public TPanel Current => current;
    public float Progress => Math.Clamp(progress.Value, 0f, 1f);
    public bool Closing => closing;
    public int OpenedFrame => openedFrame;
    public bool IsOpen => !EqualityComparer<TPanel>.Default.Equals(current, default);

    public bool IsShowing(string id, TPanel panel) =>
        EqualityComparer<TPanel>.Default.Equals(current, panel) && targetId == id;

    public void Open(string id, TPanel panel)
    {
        if (targetId != id || !EqualityComparer<TPanel>.Default.Equals(current, panel))
        {
            progress.SnapTo(0f);
        }

        targetId = id;
        current = panel;
        closing = false;
        openedFrame = ImGui.GetFrameCount();
    }

    public void Dismiss()
    {
        if (IsOpen)
        {
            closing = true;
        }
    }

    public void Reset()
    {
        targetId = null;
        current = default;
        closing = false;
        progress.SnapTo(0f);
    }

    public void Tick(float deltaSeconds)
    {
        if (!IsOpen)
        {
            return;
        }

        if (closing)
        {
            progress.Step(0f, Motion.Appear, deltaSeconds);
            if (progress.Value <= ClosedEpsilon)
            {
                Reset();
            }

            return;
        }

        progress.Step(1f, Motion.Appear, deltaSeconds);
    }

    public void DismissOnOutsideClick(Vector2 min, Vector2 max)
    {
        if (closing || ImGui.GetFrameCount() == openedFrame)
        {
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !UiInteract.HoverWindowOnly(min, max, false))
        {
            Dismiss();
        }
    }
}
