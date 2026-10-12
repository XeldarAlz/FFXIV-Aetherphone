using Aetherphone.Core.Animation;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class FeedVirtualizer
{
    public const int DefaultRowCap = 400;

    private const float ParkedAtTop = 1f;
    private const float StepEpsilon = 1f;
    private const float GlideRestEpsilon = 0.5f;

    private readonly Dictionary<string, (float Height, int Revision)> rows = new();
    private readonly List<float> rowTops = new();
    private readonly float cullMargin;
    private readonly int rowCap;
    private float rowWidth;
    private int fontGeneration;
    private float windowTop;
    private float windowBottom;
    private float rowStartY;
    private float contentTop;
    private bool arrowSteps;
    private int rowTopsFrame = -2;
    private Spring glide;
    private float glideTarget;
    private bool gliding;

    public FeedVirtualizer(float cullMargin, int rowCap = DefaultRowCap)
    {
        this.cullMargin = cullMargin;
        this.rowCap = rowCap;
    }

    public void BeginFrame(ITrimmable? source = null, bool arrowSteps = false)
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (rowWidth != width || fontGeneration != Plugin.Fonts.Generation)
        {
            rows.Clear();
            rowWidth = width;
            fontGeneration = Plugin.Fonts.Generation;
        }

        if (source is not null && ImGui.GetScrollY() < ParkedAtTop)
        {
            source.Trim(rowCap);
        }

        windowTop = ImGui.GetWindowPos().Y;
        windowBottom = windowTop + ImGui.GetWindowSize().Y;
        contentTop = windowTop - ImGui.GetScrollY();
        this.arrowSteps = arrowSteps;
        var frame = ImGui.GetFrameCount();
        if (arrowSteps && rowTopsFrame == frame - 1)
        {
            StepWithArrows();
        }
        else
        {
            gliding = false;
        }

        rowTops.Clear();
        rowTopsFrame = frame;
    }

    public bool Skip(string rowId, int revision = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        rowStartY = cursor.Y;
        if (arrowSteps)
        {
            rowTops.Add(cursor.Y - contentTop);
        }

        var margin = cullMargin * UiScale.Current;
        if (rows.TryGetValue(rowId, out var row) && row.Revision == revision
            && (cursor.Y > windowBottom + margin || cursor.Y + row.Height < windowTop - margin))
        {
            ImGui.SetCursorScreenPos(new Vector2(cursor.X, cursor.Y + row.Height));
            return true;
        }

        return false;
    }

    public void Record(string rowId, int revision = 0)
    {
        rows[rowId] = (ImGui.GetCursorScreenPos().Y - rowStartY, revision);
    }

    private void StepWithArrows()
    {
        var io = ImGui.GetIO();
        var scrollY = ImGui.GetScrollY();
        if (gliding && (io.MouseWheel != 0f || ImGui.IsMouseDown(ImGuiMouseButton.Left)))
        {
            gliding = false;
        }

        var direction = ArrowKeyCapture.Take();
        if (direction != 0)
        {
            DragScrollHost.CancelMomentum();
            var from = gliding ? glideTarget : scrollY;
            var target = direction > 0 ? NextStop(from) : PreviousStop(from);
            if (MathF.Abs(target - from) > StepEpsilon)
            {
                if (!gliding)
                {
                    glide.SnapTo(scrollY);
                }

                glideTarget = target;
                gliding = true;
            }
        }

        if (!gliding)
        {
            return;
        }

        var next = glide.Step(glideTarget, Motion.PageSettle,
            MathF.Min(io.DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (MathF.Abs(next - glideTarget) <= GlideRestEpsilon)
        {
            next = glideTarget;
            gliding = false;
        }

        ImGui.SetScrollY(next);
    }

    private float NextStop(float from)
    {
        var maxY = ImGui.GetScrollMaxY();
        var nearTop = Metrics.Space.Xxl * UiScale.Current;
        var count = rowTops.Count + 2;
        for (var index = 0; index < count; index++)
        {
            var stop = Stop(index, maxY);
            if (stop - from > nearTop || index + 1 == count)
            {
                return stop;
            }
        }

        return maxY;
    }

    private float PreviousStop(float from)
    {
        var maxY = ImGui.GetScrollMaxY();
        var nearTop = Metrics.Space.Xxl * UiScale.Current;
        for (var index = rowTops.Count + 1; index >= 0; index--)
        {
            var stop = Stop(index, maxY);
            if (from - stop > nearTop || index == 0)
            {
                return stop;
            }
        }

        return 0f;
    }

    private float Stop(int index, float maxY) =>
        index == 0 ? 0f : index > rowTops.Count ? maxY : MathF.Min(rowTops[index - 1], maxY);
}
