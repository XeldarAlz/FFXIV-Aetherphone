namespace Aetherphone.Core.Game;

internal static class GameSheets
{
    private static readonly string[] SheetBoundApps =
    {
        "skywatcher", "dailies", "wallet", "jobs", "inventory", "market", "hunts", "maps", "housing", "collections",
    };

    public static bool Available { get; private set; } = true;

    public static void MarkUnavailable() => Available = false;

    public static bool Supports(string appId)
    {
        if (Available)
        {
            return true;
        }

        for (var index = 0; index < SheetBoundApps.Length; index++)
        {
            if (string.Equals(appId, SheetBoundApps[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
