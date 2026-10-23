namespace Aetherphone.Core.Animation;

internal sealed class Pager
{
    private const float SettleSmoothTime = 0.30f;
    private const float FlingVelocity = 320f;
    private const float VelocitySmoothing = 0.6f;
    private const float RubberLimit = 0.36f;
    private const float RubberGive = 0.62f;

    private readonly float settleTime;
    private Spring scroll;
    private int page;
    private bool dragging;
    private float dragStartX;
    private float dragStartValue;
    private int dragStartPage;
    private float velocity;

    public Pager() : this(SettleSmoothTime)
    {
    }

    public Pager(float settleTime)
    {
        this.settleTime = settleTime;
    }

    public float Value => scroll.Value;
    public int Page => page;
    public bool Dragging => dragging;

    public void Begin(float mouseX)
    {
        dragging = true;
        dragStartX = mouseX;
        dragStartValue = scroll.Value;
        dragStartPage = (int)MathF.Round(scroll.Value);
        velocity = 0f;
    }

    public void Drag(float mouseX, float width, int pageCount, float delta)
    {
        if (!dragging || width <= 0f)
        {
            return;
        }

        var next = RubberBand(dragStartValue - (mouseX - dragStartX) / width, pageCount);
        if (delta > 0f)
        {
            velocity += ((next - scroll.Value) / delta - velocity) * VelocitySmoothing;
        }

        scroll.SnapTo(next);
    }

    public void Release(float width, int pageCount)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        var displaced = scroll.Value - dragStartPage;
        var target = dragStartPage;
        if (MathF.Abs(velocity * width) > FlingVelocity)
        {
            target = dragStartPage + MathF.Sign(velocity);
        }
        else if (MathF.Abs(displaced) > 0.5f)
        {
            target = dragStartPage + MathF.Sign(displaced);
        }

        page = Math.Clamp(target, 0, Math.Max(0, pageCount - 1));
        scroll.Velocity = velocity;
    }

    public void Cancel()
    {
        dragging = false;
    }

    public void Step(float delta, int pageCount)
    {
        page = Math.Clamp(page, 0, Math.Max(0, pageCount - 1));
        if (dragging)
        {
            return;
        }

        scroll.Step(page, settleTime, delta);
    }

    public void SnapTo(int target, int pageCount)
    {
        page = Math.Clamp(target, 0, Math.Max(0, pageCount - 1));
        scroll.SnapTo(page);
    }

    public void AnimateTo(int target, int pageCount)
    {
        page = Math.Clamp(target, 0, Math.Max(0, pageCount - 1));
    }

    private static float RubberBand(float raw, int pageCount)
    {
        var last = Math.Max(0, pageCount - 1);
        if (raw < 0f)
        {
            return -Rubber(-raw);
        }

        if (raw > last)
        {
            return last + Rubber(raw - last);
        }

        return raw;
    }

    private static float Rubber(float excess) => RubberLimit * excess / (excess + RubberGive);
}
