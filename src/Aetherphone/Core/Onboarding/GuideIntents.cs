namespace Aetherphone.Core.Onboarding;

internal static class GuideIntents
{
    private static string? pending;

    public static void Post(string intent) => pending = intent;

    public static bool Consume(string intent)
    {
        if (pending != intent)
        {
            return false;
        }

        pending = null;
        return true;
    }

    public static void Clear() => pending = null;
}
