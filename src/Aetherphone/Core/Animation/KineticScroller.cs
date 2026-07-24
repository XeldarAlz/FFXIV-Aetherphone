namespace Aetherphone.Core.Animation;

/// <summary>Pointer-driven kinetic scroll model: drag offset, tap-vs-drag latch, momentum, and top pull. Pure, no ImGui.</summary>
internal sealed class KineticScroller
{
    private const float DragThreshold = 6f;
    private const float MinFlingSpeed = 40f;
    private const float FlingDecayRate = 6f;
    private const float PullRubberFactor = 0.5f;
    private const float PullSpringRate = 10f;
    private const float VelocitySmoothingRate = 22f;

    private float offset;
    private float maxOffset;
    private bool pressed;
    private bool dragging;
    private bool flinging;
    private float pressStartY;
    private float lastPointerY;
    private float velocity;
    private float pullDistance;

    public float Scale { get; set; } = 1f;
    public float Offset => offset;
    public bool IsDragging => dragging;
    public bool IsControlling => dragging || flinging || pullDistance > 0f;
    public float PullDistance => pullDistance;

    public void SetBounds(float max)
    {
        maxOffset = MathF.Max(0f, max);
        if (offset > maxOffset)
        {
            offset = maxOffset;
        }
    }

    public void SyncOffset(float external)
    {
        if (!dragging && !flinging)
        {
            offset = Math.Clamp(external, 0f, maxOffset);
        }
    }

    public void Press(float pointerY)
    {
        pressed = true;
        dragging = false;
        flinging = false;
        velocity = 0f;
        pullDistance = 0f;
        pressStartY = pointerY;
        lastPointerY = pointerY;
    }

    public void Move(float pointerY, float deltaSeconds)
    {
        if (!pressed)
        {
            return;
        }

        var deltaY = pointerY - lastPointerY;
        lastPointerY = pointerY;
        if (!dragging)
        {
            if (MathF.Abs(pointerY - pressStartY) < DragThreshold * Scale)
            {
                return;
            }

            dragging = true;
        }

        if (pullDistance > 0f && deltaY < 0f)
        {
            var consume = MathF.Min(pullDistance, -deltaY);
            pullDistance -= consume;
            deltaY += consume;
        }

        var target = offset - deltaY;
        if (target <= 0f)
        {
            offset = 0f;
            if (deltaY > 0f)
            {
                pullDistance += -target * PullRubberFactor;
            }
        }
        else
        {
            offset = MathF.Min(target, maxOffset);
        }

        if (deltaSeconds > 0f)
        {
            var instant = -deltaY / deltaSeconds;
            var smoothing = MathF.Exp(-VelocitySmoothingRate * deltaSeconds);
            velocity = velocity * smoothing + instant * (1f - smoothing);
        }
    }

    public void Release()
    {
        if (dragging)
        {
            flinging = MathF.Abs(velocity) >= MinFlingSpeed * Scale;
        }

        pressed = false;
        dragging = false;
        if (!flinging)
        {
            velocity = 0f;
        }
    }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        if (!dragging && pullDistance > 0f)
        {
            pullDistance *= MathF.Exp(-PullSpringRate * deltaSeconds);
            if (pullDistance < 0.5f)
            {
                pullDistance = 0f;
            }
        }

        if (!flinging)
        {
            return;
        }

        offset += velocity * deltaSeconds;
        if (offset <= 0f)
        {
            offset = 0f;
            Stop();
            return;
        }

        if (offset >= maxOffset)
        {
            offset = maxOffset;
            Stop();
            return;
        }

        velocity *= MathF.Exp(-FlingDecayRate * deltaSeconds);
        if (MathF.Abs(velocity) < MinFlingSpeed * Scale)
        {
            Stop();
        }
    }

    public void Reset()
    {
        offset = 0f;
        pressed = false;
        dragging = false;
        flinging = false;
        velocity = 0f;
        pullDistance = 0f;
    }

    /// <summary>Abandons the in-flight gesture without moving the surface, for a widget that claims the drag.</summary>
    public void CancelGesture()
    {
        pressed = false;
        dragging = false;
        flinging = false;
        velocity = 0f;
        pullDistance = 0f;
    }

    public void CancelMomentum()
    {
        flinging = false;
        velocity = 0f;
        pullDistance = 0f;
    }

    private void Stop()
    {
        flinging = false;
        velocity = 0f;
    }
}
