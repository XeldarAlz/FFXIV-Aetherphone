using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class StripLightClock
{
    private int lastFrame = -1;

    public float Phase { get; private set; }

    public void Advance(int frame, float deltaSeconds)
    {
        if (frame == lastFrame)
        {
            return;
        }

        lastFrame = frame;
        Phase += Math.Clamp(deltaSeconds, 0f, TransitionTiming.MaxFrameSeconds);
    }
}
