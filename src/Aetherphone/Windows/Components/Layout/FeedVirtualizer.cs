using Aetherphone.Core.Animation;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class FeedVirtualizer
{
    public const int DefaultRowCap = 400;
    public const int MinAutoAdvanceSeconds = 3;
    public const int MaxAutoAdvanceSeconds = 30;
    public const double AutoAdvanceIdleLimitSeconds = 300d;

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
    private Configuration? stepping;
    private int rowTopsFrame = -2;
    private Spring glide;
    private float glideTarget;
    private bool gliding;
    private double idleSince;
    private double lastInputAt;

    public FeedVirtualizer(float cullMargin, int rowCap = DefaultRowCap)
    {
        this.cullMargin = cullMargin;
        this.rowCap = rowCap;
    }

    public void BeginFrame(ITrimmable? source = null, Configuration? stepping = null)
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
        this.stepping = stepping is { ArrowKeysStepPosts: true } or { FeedAutoAdvance: true } ? stepping : null;
        var frame = ImGui.GetFrameCount();
        if (this.stepping is not null && rowTopsFrame == frame - 1)
        {
            Step(this.stepping);
        }
        else
        {
            gliding = false;
            idleSince = ImGui.GetTime();
            lastInputAt = idleSince;
        }

        rowTops.Clear();
        rowTopsFrame = frame;
    }

    public bool Skip(string rowId, int revision = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        rowStartY = cursor.Y;
        if (stepping is not null)
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

    internal static bool AutoAdvanceDue(double now, double idleSince, double lastInputAt, int seconds) =>
        now - idleSince >= Math.Clamp(seconds, MinAutoAdvanceSeconds, MaxAutoAdvanceSeconds)
        && now - lastInputAt < AutoAdvanceIdleLimitSeconds;

    private void Step(Configuration settings)
    {
        var io = ImGui.GetIO();
        var scrollY = ImGui.GetScrollY();
        if (gliding && (io.MouseWheel != 0f || ImGui.IsMouseDown(ImGuiMouseButton.Left)))
        {
            gliding = false;
        }

        var direction = settings.ArrowKeysStepPosts ? ArrowKeyCapture.Take() : 0;
        if (direction != 0)
        {
            idleSince = ImGui.GetTime();
            lastInputAt = idleSince;
        }
        else if (settings.FeedAutoAdvance)
        {
            direction = AutoAdvance(settings.FeedAutoAdvanceSeconds);
        }

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

    private int AutoAdvance(int seconds)
    {
        var io = ImGui.GetIO();
        var now = ImGui.GetTime();
        var touched = UiInteract.WindowHovered &&
            (io.MouseDelta != Vector2.Zero || io.MouseWheel != 0f || ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var present = touched || io.WantTextInput;
        if (present)
        {
            lastInputAt = now;
        }

        if (present || UiInteract.InputBlocked || InputShield.Active)
        {
            idleSince = now;
            return 0;
        }

        if (!AutoAdvanceDue(now, idleSince, lastInputAt, seconds))
        {
            return 0;
        }

        idleSince = now;
        return 1;
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
