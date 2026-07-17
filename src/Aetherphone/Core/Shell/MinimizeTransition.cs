using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Shell;

internal enum MinimizePhase : byte
{
    None,
    Collapsing,
    Minimized,
    Expanding,
}

internal sealed class MinimizeTransition
{
    public static readonly Vector2 MinimizedSize = new(78f, 152f);

    private const float CollapseSmoothTime = 0.26f;
    private const float ExpandSmoothTime = 0.23f;

    private Spring progress;
    private MinimizePhase phase = MinimizePhase.None;

    public MinimizePhase Phase => phase;
    public bool MorphActive => phase is MinimizePhase.Collapsing or MinimizePhase.Expanding;
    public bool MinimizedResting => phase == MinimizePhase.Minimized;
    public float EasedProgress => Easing.EaseInOutCubic(Math.Clamp(progress.Value, 0f, 1f));

    public void BeginCollapse()
    {
        if (phase is MinimizePhase.None or MinimizePhase.Expanding)
        {
            phase = MinimizePhase.Collapsing;
        }
    }

    public void BeginExpand()
    {
        if (phase is MinimizePhase.Minimized or MinimizePhase.Collapsing)
        {
            phase = MinimizePhase.Expanding;
        }
    }

    public void SnapFull()
    {
        phase = MinimizePhase.None;
        progress.SnapTo(0f);
    }

    public void SnapMinimized()
    {
        phase = MinimizePhase.Minimized;
        progress.SnapTo(1f);
    }

    public void Advance(float delta)
    {
        switch (phase)
        {
            case MinimizePhase.None:
                progress.SnapTo(0f);
                break;
            case MinimizePhase.Minimized:
                progress.SnapTo(1f);
                break;
            case MinimizePhase.Collapsing:
                progress.Step(1f, CollapseSmoothTime, delta);
                if (progress.IsResting(1f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
                {
                    progress.SnapTo(1f);
                    phase = MinimizePhase.Minimized;
                }

                break;
            case MinimizePhase.Expanding:
                progress.Step(0f, ExpandSmoothTime, delta);
                if (progress.IsResting(0f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
                {
                    progress.SnapTo(0f);
                    phase = MinimizePhase.None;
                }

                break;
        }
    }

}
