namespace Aetherphone.Core.Casino;

internal enum CasinoLaunchKind
{
    Floor,
    Table,
    Game,
    Ledger,
}

internal readonly record struct CasinoLaunch(CasinoLaunchKind Kind, string TableId = "", string GameId = "");

internal sealed class CasinoLauncher
{
    public const string LedgerGroupPrefix = "casino:ledger:";

    private CasinoLaunch? pending;

    public bool HasPending => pending is not null;

    public static string LedgerGroupKey(string tableId) => string.Concat(LedgerGroupPrefix, tableId);

    public static bool RoutesWithoutCard(string tableId) =>
        CasinoRoomIds.IsBlackjackHouse(tableId) || HoldemRules.IsHouseRoom(tableId);

    public void RequestGame(string gameId)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Game, GameId: gameId);
    }

    public void RequestTable(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Table, tableId);
    }

    public void RequestLedger(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Ledger, tableId);
    }

    public void RequestNotification(string groupKey)
    {
        if (groupKey.StartsWith(LedgerGroupPrefix, StringComparison.Ordinal))
        {
            RequestLedger(groupKey[LedgerGroupPrefix.Length..]);
            return;
        }

        if (groupKey.StartsWith(CasinoTurnNotifier.GroupPrefix, StringComparison.Ordinal))
        {
            RequestTable(groupKey[CasinoTurnNotifier.GroupPrefix.Length..]);
        }
    }

    public bool TryConsume(out CasinoLaunch launch)
    {
        if (pending is not { } held)
        {
            launch = default;
            return false;
        }

        pending = null;
        launch = held;
        return true;
    }
}
