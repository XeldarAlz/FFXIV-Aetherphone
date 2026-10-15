namespace Aetherphone.Apps.Casino;

internal enum CasinoTab
{
    Floor,
    Live,
    Host,
    Cashier,
}

internal enum CasinoScreen
{
    Floor,
    Cabinet,
    Tables,
    Table,
    TableDoor,
    History,
    Fairness,
    Limits,
    RoundDetail,
    DailySpin,
    HostTable,
    TableLedger,
    Pit,
    VenueRoom,
    Broadcast,
    Fame,
    GilLedger,
    HostRules,
}

internal readonly record struct CasinoRoute(
    CasinoScreen Screen,
    string GameId = "",
    string RoundId = "",
    string TableId = "")
{
    public static readonly CasinoRoute Floor = new(CasinoScreen.Floor);

    public bool Is(CasinoScreen screen, string tableId)
    {
        return Screen == screen && tableId.Length > 0 && string.Equals(TableId, tableId, StringComparison.Ordinal);
    }

    public static bool KeepsSeat(CasinoRoute leaving, CasinoRoute below)
    {
        return leaving.Screen == CasinoScreen.Table && below.Is(CasinoScreen.TableDoor, leaving.TableId);
    }

    public static bool ReturnsToTable(CasinoRoute door, CasinoRoute below)
    {
        return door.Screen == CasinoScreen.TableDoor
            && (below.Is(CasinoScreen.Table, door.TableId) || below.Is(CasinoScreen.VenueRoom, door.TableId));
    }
}
