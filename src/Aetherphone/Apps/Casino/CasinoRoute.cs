namespace Aetherphone.Apps.Casino;

internal enum CasinoTab
{
    Lobby,
    Games,
    Live,
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
}

internal readonly record struct CasinoRoute(
    CasinoScreen Screen,
    string GameId = "",
    string RoundId = "",
    string TableId = "")
{
    public static readonly CasinoRoute Floor = new(CasinoScreen.Floor);
}
