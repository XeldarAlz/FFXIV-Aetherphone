namespace Aetherphone.Core.Activity;

internal static class ActivityGoalSteps
{
    public const float LevelsStep = 0.5f;
    public const int LevelsStepCount = 10;
    public const int MinDuties = 1;
    public const int MaxDuties = 10;

    private static readonly long[] GilSteps = { 10000, 25000, 50000, 100000, 250000, 500000, 1000000 };

    public static int Count(int ring) => ring switch
    {
        0 => LevelsStepCount,
        1 => MaxDuties - MinDuties + 1,
        _ => GilSteps.Length,
    };

    public static int IndexOf(int ring, in ActivityTargets targets) => ring switch
    {
        0 => Math.Clamp((int)MathF.Round(targets.Levels / LevelsStep) - 1, 0, LevelsStepCount - 1),
        1 => Math.Clamp(targets.Duties - MinDuties, 0, MaxDuties - MinDuties),
        _ => NearestGil(targets.Gil),
    };

    public static ActivityTargets With(int ring, int index, in ActivityTargets targets)
    {
        var clamped = Math.Clamp(index, 0, Count(ring) - 1);
        return ring switch
        {
            0 => targets with { Levels = (clamped + 1) * LevelsStep },
            1 => targets with { Duties = clamped + MinDuties },
            _ => targets with { Gil = GilSteps[clamped] },
        };
    }

    public static void Apply(Configuration configuration, in ActivityTargets targets)
    {
        configuration.ActivityGoalLevels = targets.Levels;
        configuration.ActivityGoalDuties = targets.Duties;
        configuration.ActivityGoalGil = targets.Gil;
    }

    private static int NearestGil(long gil)
    {
        var best = 0;
        for (var index = 1; index < GilSteps.Length; index++)
        {
            if (Math.Abs(GilSteps[index] - gil) < Math.Abs(GilSteps[best] - gil))
            {
                best = index;
            }
        }

        return best;
    }
}
