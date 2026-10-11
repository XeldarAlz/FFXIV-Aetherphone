using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class DailySpinPlayback
{
    public const int SpinTurns = 5;
    public const float PointerSmoothSeconds = 0.09f;
    public const float PointerKick = 0.6f;
    public const float IdleTurnRate = 0.22f;

    private Spring pointer;
    private float angle;
    private float fromAngle;
    private float sweep;
    private float elapsed = WheelChoreography.SpinSeconds;
    private int segment = -1;
    private int peg;
    private int ticks;
    private long amount;
    private bool spinning;
    private bool landed;

    public float Angle => angle;

    public bool Spinning => spinning;

    public int Segment => segment;

    public long Amount => amount;

    public float PointerDeflection => pointer.Value;

    public bool Rested => !spinning && DailySpinRules.IsSegment(segment);

    public void Reset()
    {
        pointer.SnapTo(0f);
        angle = 0f;
        fromAngle = 0f;
        sweep = 0f;
        elapsed = WheelChoreography.SpinSeconds;
        segment = -1;
        ticks = 0;
        amount = 0;
        spinning = false;
        landed = false;
    }

    public void Begin(int nextSegment, long award)
    {
        amount = award;
        landed = false;
        if (!DailySpinRules.IsSegment(nextSegment))
        {
            segment = -1;
            spinning = false;
            return;
        }

        segment = nextSegment;
        fromAngle = angle;
        sweep = WheelChoreography.SweepFor(fromAngle, nextSegment, DailySpinRules.SegmentCount, SpinTurns);
        elapsed = 0f;
        peg = WheelChoreography.PegOf(angle, DailySpinRules.SegmentCount);
        spinning = true;
    }

    public void Adopt(int knownSegment, long award)
    {
        amount = award;
        spinning = false;
        landed = false;
        segment = DailySpinRules.IsSegment(knownSegment) ? knownSegment : -1;
        if (segment >= 0)
        {
            angle = WheelChoreography.RestAngleOf(segment, DailySpinRules.SegmentCount);
        }
    }

    public void Idle(float deltaSeconds)
    {
        if (spinning || segment >= 0)
        {
            return;
        }

        angle = WheelChoreography.Normalize(angle + deltaSeconds * IdleTurnRate);
    }

    public void Update(float deltaSeconds)
    {
        pointer.Step(0f, PointerSmoothSeconds, deltaSeconds);
        if (!spinning)
        {
            return;
        }

        elapsed += deltaSeconds;
        if (elapsed >= WheelChoreography.SpinSeconds)
        {
            elapsed = WheelChoreography.SpinSeconds;
            spinning = false;
            landed = true;
        }

        angle = WheelChoreography.AngleAt(fromAngle, sweep, elapsed);
        var nextPeg = WheelChoreography.PegOf(angle, DailySpinRules.SegmentCount);
        if (nextPeg == peg)
        {
            return;
        }

        ticks += Math.Abs(nextPeg - peg);
        peg = nextPeg;
        pointer.Launch(MathF.Max(-1f, pointer.Value - PointerKick), 0f);
    }

    public void Snap()
    {
        if (!spinning)
        {
            return;
        }

        elapsed = WheelChoreography.SpinSeconds;
        angle = fromAngle + sweep;
        peg = WheelChoreography.PegOf(angle, DailySpinRules.SegmentCount);
        pointer.SnapTo(0f);
        spinning = false;
        landed = true;
    }

    public int TakeTicks()
    {
        var taken = ticks;
        ticks = 0;
        return taken;
    }

    public bool TakeLanded()
    {
        var taken = landed;
        landed = false;
        return taken;
    }
}
