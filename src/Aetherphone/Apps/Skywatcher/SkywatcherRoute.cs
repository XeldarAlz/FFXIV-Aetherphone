namespace Aetherphone.Apps.Skywatcher;

internal enum SkywatcherScreen
{
    Browse,
    Detail,
}

internal readonly record struct SkywatcherRoute(SkywatcherScreen Screen, uint TerritoryId = 0)
{
    public static readonly SkywatcherRoute Browse = new(SkywatcherScreen.Browse);

    public static SkywatcherRoute Detail(uint territoryId) => new(SkywatcherScreen.Detail, territoryId);
}
