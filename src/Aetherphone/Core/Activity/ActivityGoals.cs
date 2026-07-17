namespace Aetherphone.Core.Activity;

internal static class ActivityGoals
{
    public const float ClosedThreshold = 0.999f;

    public static float ProgressFraction(Configuration configuration, ActivityDay day) =>
        configuration.ActivityGoalLevels <= 0f ? 0f : day.LevelUnitsGained / configuration.ActivityGoalLevels;

    public static float AdventureFraction(Configuration configuration, ActivityDay day) =>
        configuration.ActivityGoalDuties <= 0 ? 0f : day.DutiesCompleted / (float)configuration.ActivityGoalDuties;

    public static float FortuneFraction(Configuration configuration, ActivityDay day) =>
        configuration.ActivityGoalGil <= 0 ? 0f : day.GilEarned / (float)configuration.ActivityGoalGil;

    public static bool AllClosed(Configuration configuration, ActivityDay day) =>
        ProgressFraction(configuration, day) >= ClosedThreshold &&
        AdventureFraction(configuration, day) >= ClosedThreshold &&
        FortuneFraction(configuration, day) >= ClosedThreshold;
}
