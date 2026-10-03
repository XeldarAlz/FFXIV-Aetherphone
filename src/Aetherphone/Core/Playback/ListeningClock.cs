namespace Aetherphone.Core.Playback;

internal struct ListeningClock
{
    private const float MaximumStepSeconds = 1f;

    private float lastPosition;
    private float seconds;
    private bool observing;

    public readonly float Seconds => seconds;

    public void Observe(float position, bool playing)
    {
        if (!playing)
        {
            observing = false;
            return;
        }

        if (observing)
        {
            var step = position - lastPosition;
            if (step > 0f && step <= MaximumStepSeconds)
            {
                seconds += step;
            }
        }

        lastPosition = position;
        observing = true;
    }

    public int Take()
    {
        var whole = (int)seconds;
        seconds = 0f;
        observing = false;
        return whole;
    }
}
