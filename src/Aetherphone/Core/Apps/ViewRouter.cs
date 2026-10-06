using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Apps;

internal delegate void RouterDraw<TView>(TView view, Rect area, int depth);

internal sealed class ViewRouter<TView>
{
    private readonly List<TView> stack = new();
    private readonly List<string> viewIds = new();
    private Spring slide;
    private bool transitioning;
    private int idCounter;
    private TView outgoing = default!;
    private int outgoingDepth;
    private string outgoingId = string.Empty;
    private SlideDirection direction;
    private readonly LayerPainter paintIncoming;
    private readonly LayerPainter paintLeaving;
    private RouterDraw<TView> activeDraw = null!;
    private TView paintedIncoming = default!;
    private int paintedIncomingDepth;
    private TView paintedLeaving = default!;
    private int paintedLeavingDepth;

    public ViewRouter(TView root)
    {
        stack.Add(root);
        viewIds.Add(NewId());
        paintIncoming = PaintIncoming;
        paintLeaving = PaintLeaving;
    }

    public TView Current => stack[stack.Count - 1];
    public int Depth => stack.Count;
    public bool IsTransitioning => transitioning;
    private string CurrentId => viewIds[viewIds.Count - 1];

    public bool TryGetView(int index, out TView view)
    {
        if (index < 0 || index >= stack.Count)
        {
            view = default!;
            return false;
        }

        view = stack[index];
        return true;
    }

    public void Push(TView view) => Push(view, true);

    public void Push(TView view, bool animate)
    {
        if (animate)
        {
            BeginOutgoing(SlideDirection.Forward);
        }

        stack.Add(view);
        viewIds.Add(NewId());

        if (animate)
        {
            StartSlide();
        }
    }

    public void Replace(TView view)
    {
        stack[stack.Count - 1] = view;
        viewIds[viewIds.Count - 1] = NewId();
    }

    public bool Pop() => Pop(true);

    public bool Pop(bool animate)
    {
        if (stack.Count <= 1)
        {
            return false;
        }

        if (animate)
        {
            BeginOutgoing(SlideDirection.Back);
        }

        stack.RemoveAt(stack.Count - 1);
        viewIds.RemoveAt(viewIds.Count - 1);

        if (animate)
        {
            StartSlide();
            return true;
        }

        transitioning = false;
        outgoing = default!;
        return true;
    }

    public void Reset()
    {
        transitioning = false;
        outgoing = default!;

        if (stack.Count <= 1)
        {
            return;
        }

        stack.RemoveRange(1, stack.Count - 1);
        viewIds.RemoveRange(1, viewIds.Count - 1);
    }

    public void Draw(Rect area, Vector4 background, float deltaSeconds, RouterDraw<TView> draw)
    {
        if (transitioning)
        {
            slide.Step(1f, TransitionTiming.PushSmoothTime,
                MathF.Min(deltaSeconds, TransitionTiming.MotionFrameSeconds));

            if (slide.IsResting(1f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
            {
                slide.SnapTo(1f);
                transitioning = false;
                outgoing = default!;
            }
        }

        activeDraw = draw;
        paintedIncoming = Current;
        paintedIncomingDepth = Depth;
        if (!transitioning)
        {
            paintedLeaving = default!;
            SceneCompositor.DrawLayer(area,
                new SceneCompositor.Layer(CurrentId, Vector2.Zero, 0f, paintIncoming, background));
            return;
        }

        var progress = slide.Value;
        var width = area.Width;
        var incomingId = CurrentId;
        var leavingId = outgoingId;
        paintedLeaving = outgoing;
        paintedLeavingDepth = outgoingDepth;
        SceneCompositor.Layer under;
        SceneCompositor.Layer over;

        if (direction == SlideDirection.Forward)
        {
            var underOffset = new Vector2(-TransitionTiming.UnderParallax * progress * width, 0f);
            var overOffset = new Vector2((1f - progress) * width, 0f);
            under = new SceneCompositor.Layer(leavingId, underOffset, TransitionTiming.UnderDimMax * progress,
                paintLeaving, background, true);
            over = new SceneCompositor.Layer(incomingId, overOffset, 0f, paintIncoming, background, true);
        }
        else
        {
            var underOffset = new Vector2(-TransitionTiming.UnderParallax * (1f - progress) * width, 0f);
            var overOffset = new Vector2(progress * width, 0f);
            under = new SceneCompositor.Layer(incomingId, underOffset, TransitionTiming.UnderDimMax * (1f - progress),
                paintIncoming, background, true);
            over = new SceneCompositor.Layer(leavingId, overOffset, 0f, paintLeaving, background, true);
        }

        SceneCompositor.Composite(area, under, over);
    }

    private void PaintIncoming(Rect target) => activeDraw(paintedIncoming, target, paintedIncomingDepth);

    private void PaintLeaving(Rect target) => activeDraw(paintedLeaving, target, paintedLeavingDepth);

    private void StartSlide()
    {
        slide.Launch(0f, TransitionTiming.LaunchVelocity(TransitionTiming.PushSmoothTime));
        transitioning = true;
    }

    private void BeginOutgoing(SlideDirection slideDirection)
    {
        if (transitioning)
        {
            transitioning = false;
        }

        outgoing = Current;
        outgoingDepth = stack.Count;
        outgoingId = CurrentId;
        direction = slideDirection;
    }

    private string NewId() => "view" + idCounter++;
}
