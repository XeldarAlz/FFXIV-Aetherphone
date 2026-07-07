using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Shell.Home;

internal sealed class HomePager
{
    private const float SettleSmoothTime = 0.30f;
    private const float FlingVelocity = 320f;
    private const float RubberLimit = 0.36f;
    private const float RubberGive = 0.62f;

    private Spring scroll;
    private int page;
    private bool dragging;
    private float dragStartX;
    private int dragStartPage;
    private float lastX;
    private float velocityX;

    public float Value => scroll.Value;
    public int Page => page;
    public bool Dragging => dragging;

    public void Begin(float mouseX)
    {
        dragging = true;
        dragStartX = mouseX;
        dragStartPage = page;
        lastX = mouseX;
        velocityX = 0f;
    }

    public void Drag(float mouseX, float width, int pageCount, float delta)
    {
        if (!dragging || width <= 0f)
        {
            return;
        }

        if (delta > 0f)
        {
            velocityX = (mouseX - lastX) / delta;
        }

        lastX = mouseX;
        var raw = dragStartPage - (mouseX - dragStartX) / width;
        scroll.SnapTo(RubberBand(raw, pageCount));
    }

    public void Release(float width, int pageCount)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        var pageVelocity = width > 0f ? -velocityX / width : 0f;
        var displaced = scroll.Value - dragStartPage;
        var target = dragStartPage;
        if (MathF.Abs(velocityX) > FlingVelocity)
        {
            target = dragStartPage + MathF.Sign(-velocityX);
        }
        else if (MathF.Abs(displaced) > 0.5f)
        {
            target = dragStartPage + MathF.Sign(displaced);
        }

        page = Math.Clamp(target, 0, Math.Max(0, pageCount - 1));
        scroll.Velocity = pageVelocity;
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

        scroll.Step(page, SettleSmoothTime, delta);
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
