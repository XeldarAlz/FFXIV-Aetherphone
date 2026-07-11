namespace Aetherphone.Core.Onboarding;

internal static class TourHolds
{
    private static readonly HashSet<string> Held = new();

    public static void Hold(string appId) => Held.Add(appId);

    public static void Release(string appId) => Held.Remove(appId);

    public static bool IsHeld(string appId) => Held.Contains(appId);
}
