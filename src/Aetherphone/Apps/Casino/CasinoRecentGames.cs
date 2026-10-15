namespace Aetherphone.Apps.Casino;

internal static class CasinoRecentGames
{
    private const string KindPrefix = "casino.";

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> GameIds =
        new(StringComparer.Ordinal);

    public static string ClientGameId(string wireKind) =>
        wireKind.StartsWith(KindPrefix, StringComparison.Ordinal)
            ? GameIds.GetOrAdd(wireKind, static kind => kind[KindPrefix.Length..])
            : wireKind;
}
