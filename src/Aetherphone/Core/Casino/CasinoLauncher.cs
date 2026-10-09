namespace Aetherphone.Core.Casino;

internal enum CasinoLaunchKind
{
    Floor,
    Tables,
    Table,
    Game,
}

internal readonly record struct CasinoLaunch(CasinoLaunchKind Kind, string TableId = "", string GameId = "");

internal sealed class CasinoLauncher
{
    private CasinoLaunch? pending;

    public bool HasPending => pending is not null;

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

    public void RequestTables()
    {
        pending = new CasinoLaunch(CasinoLaunchKind.Tables);
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
