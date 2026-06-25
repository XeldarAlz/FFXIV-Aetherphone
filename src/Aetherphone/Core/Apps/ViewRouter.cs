using System.Numerics;
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

    public ViewRouter(TView root)
    {
        stack.Add(root);
        viewIds.Add(NewId());
    }

    public TView Current => stack[stack.Count - 1];

    public int Depth => stack.Count;

    public bool IsTransitioning => transitioning;

    private string CurrentId => viewIds[viewIds.Count - 1];

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

    public bool Pop()
    {
        if (stack.Count <= 1)
        {
            return false;
        }

        BeginOutgoing(SlideDirection.Back);
        stack.RemoveAt(stack.Count - 1);
        viewIds.RemoveAt(viewIds.Count - 1);
        StartSlide();
        return true;
    }

    public void Reset()
    {
        transitioning = false;
        outgoing = default!;
        if (stack.Count > 1)
        {
            stack.RemoveRange(1, stack.Count - 1);
            viewIds.RemoveRange(1, viewIds.Count - 1);
        }
    }

    public void Draw(Rect area, Vector4 background, float deltaSeconds, RouterDraw<TView> draw)
    {
        if (transitioning)
        {
            slide.Step(1f, TransitionTiming.PushSmoothTime, MathF.Min(deltaSeconds, TransitionTiming.MaxFrameSeconds));
            if (slide.IsResting(1f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
            {
                slide.SnapTo(1f);
                transitioning = false;
                outgoing = default!;
            }
        }

        if (!transitioning)
        {
            var current = Current;
            var depth = Depth;
            SceneCompositor.DrawLayer(area, new SceneCompositor.Layer(CurrentId, Vector2.Zero, 0f, target => draw(current, target, depth), background));
            return;
        }

        var progress = slide.Value;
        var width = area.Width;
        var incoming = Current;
        var incomingDepth = Depth;
        var incomingId = CurrentId;
        var leaving = outgoing;
        var leavingDepth = outgoingDepth;
        var leavingId = outgoingId;

        SceneCompositor.Layer under;
        SceneCompositor.Layer over;
        if (direction == SlideDirection.Forward)
        {
            var underOffset = new Vector2(-TransitionTiming.UnderParallax * progress * width, 0f);
            var overOffset = new Vector2((1f - progress) * width, 0f);
            under = new SceneCompositor.Layer(leavingId, underOffset, TransitionTiming.UnderDimMax * progress, target => draw(leaving, target, leavingDepth), background, true);
            over = new SceneCompositor.Layer(incomingId, overOffset, 0f, target => draw(incoming, target, incomingDepth), background, true);
        }
        else
        {
            var underOffset = new Vector2(-TransitionTiming.UnderParallax * (1f - progress) * width, 0f);
            var overOffset = new Vector2(progress * width, 0f);
            under = new SceneCompositor.Layer(incomingId, underOffset, TransitionTiming.UnderDimMax * (1f - progress), target => draw(incoming, target, incomingDepth), background, true);
            over = new SceneCompositor.Layer(leavingId, overOffset, 0f, target => draw(leaving, target, leavingDepth), background, true);
        }

        SceneCompositor.Composite(area, under, over);
    }

    private void StartSlide()
    {
        slide.SnapTo(0f);
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
