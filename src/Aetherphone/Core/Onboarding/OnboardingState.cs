namespace Aetherphone.Core.Onboarding;

internal static class OnboardingState
{
    private static bool replayWelcomeRequested;
    public static bool Enabled => Plugin.Cfg.TutorialsEnabled;

    public static bool HasCompleted(string id, int version) =>
        Plugin.Cfg.OnboardingCompleted.TryGetValue(id, out var stored) && stored >= version;

    public static void SetEnabled(bool enabled)
    {
        if (Plugin.Cfg.TutorialsEnabled == enabled)
        {
            return;
        }

        Plugin.Cfg.TutorialsEnabled = enabled;
        Plugin.Cfg.Save();
    }

    public static void MarkCompleted(string id, int version)
    {
        Plugin.Cfg.OnboardingCompleted[id] = version;
        Plugin.Cfg.Save();
    }

    public static void ResetAll()
    {
        if (Plugin.Cfg.OnboardingCompleted.Count == 0)
        {
            return;
        }

        Plugin.Cfg.OnboardingCompleted.Clear();
        Plugin.Cfg.Save();
    }

    public static void RequestReplayWelcome() => replayWelcomeRequested = true;

    public static bool ConsumeReplayWelcome()
    {
        if (!replayWelcomeRequested)
        {
            return false;
        }

        replayWelcomeRequested = false;
        return true;
    }
}
