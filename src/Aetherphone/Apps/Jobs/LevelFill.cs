using Aetherphone.Core.Animation;
using Aetherphone.Core.Jobs;

namespace Aetherphone.Apps.Jobs;

internal struct LevelFill
{
    private const float FullTurn = 0.995f;

    private Spring spring;
    private uint jobId;
    private int level;

    public readonly float Value => spring.Value;

    public float Step(JobRow job, float deltaSeconds)
    {
        if (job.ClassJobId != jobId || job.Level < level)
        {
            jobId = job.ClassJobId;
            level = job.Level;
        }

        if (job.Level > level)
        {
            if (spring.Value < FullTurn)
            {
                return spring.Step(1f, Motion.Sheet, deltaSeconds);
            }

            level = job.Level;
            if (!job.IsCapped)
            {
                spring.SnapTo(0f);
            }
        }

        return spring.Step(job.Fraction, Motion.Sheet, deltaSeconds);
    }

    public void SnapTo(float value)
    {
        spring.SnapTo(value);
        jobId = 0;
    }
}
