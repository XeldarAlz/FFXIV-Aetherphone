namespace Aetherphone.Core.Shell;

internal readonly record struct ScrubFrame(bool Scrubbing, float Cover, Vector2 Drift);

internal readonly record struct ScrubRelease(bool WasScrubbing, bool ToHome, float CoverVelocity);

internal sealed class HomeIndicatorGesture
{
    private const float DeadZoneUnits = 6f;
    private const float HoldSeconds = 0.45f;
    private const float RangeFraction = 0.38f;
    private const float HeldCover = 0.45f;
    private const float OverflowGain = 0.2f;
    private const float FollowFactor = 0.5f;
    private const float FlickUnitsPerSecond = 450f;
    private const float ReturnCover = 0.78f;
    private const float VelocityBlend = 0.5f;

    private Vector2 pressPosition;
    private Vector2 lastPosition;
    private float velocityUp;
    private float heldSeconds;
    private bool holdFired;
    private bool holdCancelled;
    private bool pressed;
    private bool scrubbing;

    public bool Pressed => pressed;
    public bool Scrubbing => scrubbing;

    public void Press(Vector2 position)
    {
        pressPosition = position;
        lastPosition = position;
        velocityUp = 0f;
        heldSeconds = 0f;
        holdFired = false;
        holdCancelled = false;
        pressed = true;
        scrubbing = false;
    }

    public bool TrackHold(Vector2 position, float deltaSeconds, float scale)
    {
        if (!pressed || scrubbing || holdFired || holdCancelled)
        {
            return false;
        }

        if ((position - pressPosition).Length() > DeadZoneUnits * scale)
        {
            holdCancelled = true;
            return false;
        }

        heldSeconds += deltaSeconds;
        if (heldSeconds < HoldSeconds)
        {
            return false;
        }

        holdFired = true;
        return true;
    }

    public void Cancel()
    {
        pressed = false;
        scrubbing = false;
    }

    public ScrubFrame Track(Vector2 position, float deltaSeconds, float screenHeight, float scale)
    {
        if (!pressed)
        {
            return new ScrubFrame(false, 1f, Vector2.Zero);
        }

        if (deltaSeconds > 0f)
        {
            var instantaneous = (lastPosition.Y - position.Y) / deltaSeconds;
            velocityUp += (instantaneous - velocityUp) * VelocityBlend;
        }

        lastPosition = position;
        var distanceUp = pressPosition.Y - position.Y;
        if (!scrubbing && distanceUp < DeadZoneUnits * scale)
        {
            return new ScrubFrame(false, 1f, Vector2.Zero);
        }

        scrubbing = true;
        var cover = CoverFor(distanceUp, Range(screenHeight));
        var drift = (position - pressPosition) * FollowFactor;
        return new ScrubFrame(true, cover, drift);
    }

    public ScrubRelease Release(float screenHeight, float scale)
    {
        var wasScrubbing = scrubbing;
        var distanceUp = pressPosition.Y - lastPosition.Y;
        var range = Range(screenHeight);
        var cover = CoverFor(distanceUp, range);
        var flick = FlickUnitsPerSecond * scale;
        var toHome = velocityUp > flick || (cover <= ReturnCover && velocityUp > -flick);
        var coverVelocity = -(1f - HeldCover) * velocityUp / range;
        pressed = false;
        scrubbing = false;
        return new ScrubRelease(wasScrubbing, toHome, coverVelocity);
    }

    public static float CoverFor(float distanceUp, float range)
    {
        var travel = MathF.Max(0f, distanceUp) / MathF.Max(1f, range);
        var progress = travel <= 1f ? travel : 1f + (1f - 1f / travel) * OverflowGain;
        return 1f - (1f - HeldCover) * progress;
    }

    private static float Range(float screenHeight) => MathF.Max(1f, screenHeight * RangeFraction);
}
