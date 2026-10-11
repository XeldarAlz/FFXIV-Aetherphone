using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class CasinoClient
{
    internal const string StatePath = "/casino";
    internal const string BuyChipsPath = "/casino/chips/buy";
    internal const string CloseSittingPath = "/casino/sittings/close";
    internal const string LimitsPath = "/casino/limits";
    internal const string SpinSlotsPath = "/casino/slots/spin";
    internal const string BuyScratchPath = "/casino/scratch/buy";
    internal const string StartBarkeepPath = "/casino/barkeep/start";
    internal const string FinishBarkeepPath = "/casino/barkeep/finish";

    internal const string RoundsPath = "/casino/rounds";
    internal const string RoomsPath = "/casino/rooms";
    internal const string DailySpinPath = "/casino/dailyspin";
    internal const string WheelBetPath = "/casino/wheel/bet";
    internal const string BingoCardsPath = "/casino/bingo/cards";
    internal const string RaceBetPath = "/casino/race/bet";
    internal const string BlackjackSitPath = "/casino/blackjack/sit";
    internal const string BlackjackLeavePath = "/casino/blackjack/leave";
    internal const string BlackjackWagerPath = "/casino/blackjack/wager";
    internal const string BlackjackBetPath = "/casino/blackjack/bet";
    internal const string BlackjackActionPath = "/casino/blackjack/act";

    internal const string TablesPath = "/casino/tables";
    internal const string QuickSeatPath = "/casino/tables/quickseat";
    internal const string NearbyTablesPath = "/casino/tables/nearby";
    internal const string JoinTablePath = "/casino/tables/join";
    internal const string BlackjackRebuyPath = "/casino/blackjack/rebuy";
    internal const string LedgerPath = "/casino/ledger";
    internal const string BonusPath = "/casino/bonus";
    internal const string VenuePath = "/casino/venue";

    internal const string RenameLeaf = "rename";
    internal const string CoDealersLeaf = "codealers";
    internal const string PauseLeaf = "pause";
    internal const string DealLeaf = "deal";
    internal const string TournamentLeaf = "tournament";
    internal const string TournamentStopLeaf = "tournament/stop";
    internal const string LedgerLeaf = "ledger";
    internal const string CloseLeaf = "close";

    internal static string BonusClaimPath(string kind)
    {
        return string.Concat(BonusPath, "/", Uri.EscapeDataString(kind), "/claim");
    }

    internal static string NearbyPath(int world, int territory, int ward)
    {
        return string.Concat(NearbyTablesPath, "?world=", world.ToString(CultureInfo.InvariantCulture),
            "&territory=", territory.ToString(CultureInfo.InvariantCulture), "&ward=",
            ward.ToString(CultureInfo.InvariantCulture));
    }

    internal static string LedgerEntryPath(string entryId, string leaf)
    {
        return string.Concat(LedgerPath, "/", Uri.EscapeDataString(entryId), "/", leaf);
    }

    internal static string VenueActPath(string roomId)
    {
        return string.Concat(VenuePath, "/", Uri.EscapeDataString(roomId), "/act");
    }

    internal static string RoomVerifyPath(string roomId, long index)
    {
        return string.Concat(RoomsPath, "/", Uri.EscapeDataString(roomId), "/verify/",
            index.ToString(CultureInfo.InvariantCulture));
    }

    internal static string RoomPath(string roomId)
    {
        return string.Concat(RoomsPath, "/", Uri.EscapeDataString(roomId));
    }

    internal static string TablesPagePath(string gameKind)
    {
        return gameKind.Length == 0
            ? TablesPath
            : string.Concat(TablesPath, "?game=", Uri.EscapeDataString(gameKind));
    }

    internal static string TablePath(string roomId, string leaf)
    {
        return string.Concat(TablesPath, "/", Uri.EscapeDataString(roomId), "/", leaf);
    }

    internal static string WheelBetsPath(string roomId)
    {
        return string.Concat("/casino/wheel/", Uri.EscapeDataString(roomId), "/bets");
    }

    internal static string RaceBetsPath(string roomId)
    {
        return string.Concat("/casino/race/", Uri.EscapeDataString(roomId), "/bets");
    }

    internal static string BingoMyCardsPath(string roomId)
    {
        return string.Concat("/casino/bingo/", Uri.EscapeDataString(roomId), "/cards");
    }

    internal static string BlackjackMyHandPath(string roomId)
    {
        return string.Concat("/casino/blackjack/", Uri.EscapeDataString(roomId), "/hand");
    }

    internal const string HoldemSitPath = "/casino/holdem/sit";
    internal const string HoldemLeavePath = "/casino/holdem/leave";
    internal const string HoldemActPath = "/casino/holdem/act";
    internal const string HoldemTopUpPath = "/casino/holdem/topup";
    internal const string HoldemTimeBankPath = "/casino/holdem/timebank";
    internal const string HoldemSitOutPath = "/casino/holdem/sitout";

    internal static string HoldemHandPath(string roomId)
    {
        return string.Concat("/casino/holdem/", Uri.EscapeDataString(roomId), "/hand");
    }

    internal static string HoldemHistoryPath(string roomId)
    {
        return string.Concat("/casino/holdem/", Uri.EscapeDataString(roomId), "/history");
    }

    internal static string VerifyRoundPath(string roundId)
    {
        return string.Concat("/casino/rounds/", roundId, "/verify");
    }

    internal static string RoundsPagePath(string? cursor)
    {
        return cursor is null || cursor.Length == 0
            ? RoundsPath
            : string.Concat(RoundsPath, "?cursor=", Uri.EscapeDataString(cursor));
    }

    private readonly AethernetTransport net;

    public CasinoClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CasinoStateDto?> GetStateAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(StatePath, AethernetJsonContext.Default.CasinoStateDto, token, null, onFailure);
    }

    public Task<CasinoBuyChipsDto?> BuyChipsAsync(long coins, string clientActionId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BuyChipsPath, new CasinoBuyChipsRequest(coins, clientActionId),
            AethernetJsonContext.Default.CasinoBuyChipsRequest, AethernetJsonContext.Default.CasinoBuyChipsDto, token,
            null, onFailure);
    }

    public Task<CasinoSittingResultDto?> CloseSittingAsync(string sittingId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(CloseSittingPath, new CasinoCloseSittingRequest(sittingId),
            AethernetJsonContext.Default.CasinoCloseSittingRequest,
            AethernetJsonContext.Default.CasinoSittingResultDto, token, null, onFailure);
    }

    public Task<CasinoLimitsDto?> SetLimitsAsync(long? selfLossLimit, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(LimitsPath, new CasinoLimitRequest(selfLossLimit),
            AethernetJsonContext.Default.CasinoLimitRequest,
            AethernetJsonContext.Default.CasinoLimitsDto, token, null, onFailure);
    }

    public Task<CasinoSlotsSpinDto?> SpinSlotsAsync(string sittingId, string clientRoundId, long bet,
        string machineId, string mode, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(SpinSlotsPath, new CasinoSlotsSpinRequest(sittingId, clientRoundId, bet, machineId, mode),
            AethernetJsonContext.Default.CasinoSlotsSpinRequest,
            AethernetJsonContext.Default.CasinoSlotsSpinDto, token, null, onFailure);
    }

    public Task<CasinoScratchCardDto?> BuyScratchAsync(string sittingId, string clientRoundId, int tier,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BuyScratchPath, new CasinoScratchBuyRequest(sittingId, clientRoundId, tier),
            AethernetJsonContext.Default.CasinoScratchBuyRequest,
            AethernetJsonContext.Default.CasinoScratchCardDto, token, null, onFailure);
    }

    public Task<CasinoBarkeepStartDto?> StartBarkeepAsync(string sittingId, string clientRoundId,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(StartBarkeepPath, new CasinoBarkeepStartRequest(sittingId, clientRoundId),
            AethernetJsonContext.Default.CasinoBarkeepStartRequest,
            AethernetJsonContext.Default.CasinoBarkeepStartDto, token, null, onFailure);
    }

    public Task<CasinoBarkeepFinishDto?> FinishBarkeepAsync(string roundId, CasinoBarkeepOrderRequest[] orders,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(FinishBarkeepPath, new CasinoBarkeepFinishRequest(roundId, orders),
            AethernetJsonContext.Default.CasinoBarkeepFinishRequest,
            AethernetJsonContext.Default.CasinoBarkeepFinishDto, token, null, onFailure);
    }

    public Task<CasinoRoundVerifyDto?> VerifyRoundAsync(string roundId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(VerifyRoundPath(roundId), AethernetJsonContext.Default.CasinoRoundVerifyDto, token, null,
            onFailure);
    }

    public Task<CasinoRoundHistoryPage?> RoundsPageAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoundsPagePath(cursor), AethernetJsonContext.Default.CasinoRoundHistoryPage, token, null,
            onFailure);
    }

    public Task<CasinoRoomListDto?> RoomsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoomsPath, AethernetJsonContext.Default.CasinoRoomListDto, token, null, onFailure);
    }

    public Task<CasinoRoomSnapshotDto?> RoomStateAsync(string roomId, Action<int> onStatus,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoomPath(roomId), AethernetJsonContext.Default.CasinoRoomSnapshotDto, token,
            onStatus, onFailure);
    }

    public Task<CasinoWheelBetDto?> PlaceWheelBetAsync(string roomId, long roundIndex, string clientRoundId,
        string clientBetId, int spot, long amount, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(WheelBetPath,
            new CasinoWheelBetRequest(roomId, roundIndex, clientRoundId, clientBetId, spot, amount),
            AethernetJsonContext.Default.CasinoWheelBetRequest,
            AethernetJsonContext.Default.CasinoWheelBetDto, token, null, onFailure);
    }

    public Task<CasinoWheelBetsDto?> MyWheelBetsAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(WheelBetsPath(roomId), AethernetJsonContext.Default.CasinoWheelBetsDto, token, null,
            onFailure);
    }

    public Task<CasinoRaceBetDto?> PlaceRaceBetAsync(CasinoRaceBetRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(RaceBetPath, request, AethernetJsonContext.Default.CasinoRaceBetRequest,
            AethernetJsonContext.Default.CasinoRaceBetDto, token, null, onFailure);
    }

    public Task<CasinoRaceBetsDto?> MyRaceBetsAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RaceBetsPath(roomId), AethernetJsonContext.Default.CasinoRaceBetsDto, token, null,
            onFailure);
    }

    public Task<CasinoBingoCardsDto?> BuyBingoCardsAsync(string roomId, long roundIndex, string clientRoundId,
        int cardCount, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BingoCardsPath,
            new CasinoBingoCardsRequest(roomId, roundIndex, clientRoundId, cardCount),
            AethernetJsonContext.Default.CasinoBingoCardsRequest,
            AethernetJsonContext.Default.CasinoBingoCardsDto, token, null, onFailure);
    }

    public Task<CasinoBlackjackActionResultDto?> PlaceBlackjackBetAsync(string roomId, string clientRoundId,
        string clientActionId, long amount, long perfectPairs, long twentyOnePlusThree, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BlackjackBetPath,
            new CasinoBlackjackBetRequest(roomId, clientRoundId, clientActionId, amount, perfectPairs,
                twentyOnePlusThree),
            AethernetJsonContext.Default.CasinoBlackjackBetRequest,
            AethernetJsonContext.Default.CasinoBlackjackActionResultDto, token, null, onFailure);
    }

    public Task<CasinoBlackjackActionResultDto?> SendBlackjackActionAsync(string roomId, string handId,
        int actionCount, string action, string clientActionId, bool isWager, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(isWager ? BlackjackWagerPath : BlackjackActionPath,
            new CasinoBlackjackActionRequest(roomId, handId, actionCount, action, clientActionId),
            AethernetJsonContext.Default.CasinoBlackjackActionRequest,
            AethernetJsonContext.Default.CasinoBlackjackActionResultDto, token, null, onFailure);
    }

    public Task<CasinoHoldemSeatResultDto?> SitHoldemAsync(CasinoHoldemSitRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HoldemSitPath, request, AethernetJsonContext.Default.CasinoHoldemSitRequest,
            AethernetJsonContext.Default.CasinoHoldemSeatResultDto, token, null, onFailure);
    }

    public Task<CasinoHoldemSeatResultDto?> LeaveHoldemAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HoldemLeavePath, new CasinoHoldemLeaveRequest(roomId),
            AethernetJsonContext.Default.CasinoHoldemLeaveRequest,
            AethernetJsonContext.Default.CasinoHoldemSeatResultDto, token, null, onFailure);
    }

    public Task<CasinoHoldemActionResultDto?> ActHoldemAsync(CasinoHoldemActRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HoldemActPath, request, AethernetJsonContext.Default.CasinoHoldemActRequest,
            AethernetJsonContext.Default.CasinoHoldemActionResultDto, token, null, onFailure);
    }

    public Task<CasinoHoldemSeatResultDto?> TopUpHoldemAsync(CasinoHoldemTopUpRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HoldemTopUpPath, request, AethernetJsonContext.Default.CasinoHoldemTopUpRequest,
            AethernetJsonContext.Default.CasinoHoldemSeatResultDto, token, null, onFailure);
    }

    public Task<CasinoHoldemActionResultDto?> HoldemTimeBankAsync(CasinoHoldemTimeBankRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HoldemTimeBankPath, request, AethernetJsonContext.Default.CasinoHoldemTimeBankRequest,
            AethernetJsonContext.Default.CasinoHoldemActionResultDto, token, null, onFailure);
    }

    public Task<CasinoHoldemSeatResultDto?> HoldemSitOutAsync(CasinoHoldemSitOutRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HoldemSitOutPath, request, AethernetJsonContext.Default.CasinoHoldemSitOutRequest,
            AethernetJsonContext.Default.CasinoHoldemSeatResultDto, token, null, onFailure);
    }

    public Task<CasinoBlackjackHandStateDto?> MyHoldemHandAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(HoldemHandPath(roomId), AethernetJsonContext.Default.CasinoBlackjackHandStateDto,
            token, null, onFailure);
    }

    public Task<CasinoHoldemHistoryDto?> HoldemHistoryAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(HoldemHistoryPath(roomId), AethernetJsonContext.Default.CasinoHoldemHistoryDto, token,
            null, onFailure);
    }

    public Task<CasinoTableListDto?> TablesAsync(string gameKind, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(TablesPagePath(gameKind), AethernetJsonContext.Default.CasinoTableListDto, token, null,
            onFailure);
    }

    public Task<CasinoQuickSeatDto?> QuickSeatAsync(string gameKind, int stakeTier, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(QuickSeatPath, new CasinoQuickSeatRequest(gameKind, stakeTier),
            AethernetJsonContext.Default.CasinoQuickSeatRequest,
            AethernetJsonContext.Default.CasinoQuickSeatDto, token, null, onFailure);
    }

    public Task<CasinoTableResultDto?> CreateTableAsync(string clientTableId, int stakeTier,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablesPath, new CasinoTableCreateRequest(clientTableId, stakeTier),
            AethernetJsonContext.Default.CasinoTableCreateRequest,
            AethernetJsonContext.Default.CasinoTableResultDto, token, null, onFailure);
    }

    public Task<CasinoTableResultDto?> HostTableAsync(string clientTableId, CasinoTableConfigDto config,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablesPath, new CasinoTableCreateRequest(clientTableId, 0, config),
            AethernetJsonContext.Default.CasinoTableCreateRequest,
            AethernetJsonContext.Default.CasinoTableResultDto, token, null, onFailure);
    }

    public Task<CasinoTableJoinDto?> JoinTableAsync(string code, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(JoinTablePath, new CasinoTableJoinRequest(code),
            AethernetJsonContext.Default.CasinoTableJoinRequest,
            AethernetJsonContext.Default.CasinoTableJoinDto, token, null, onFailure);
    }

    public Task<CasinoTableListDto?> NearbyTablesAsync(int world, int territory, int ward, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(NearbyPath(world, territory, ward), AethernetJsonContext.Default.CasinoTableListDto,
            token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> RenameTableAsync(string roomId, string name, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, RenameLeaf), new CasinoTableRenameRequest(name),
            AethernetJsonContext.Default.CasinoTableRenameRequest,
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> SetCoDealersAsync(string roomId, string[] userIds, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, CoDealersLeaf), new CasinoTableCoDealersRequest(userIds),
            AethernetJsonContext.Default.CasinoTableCoDealersRequest,
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> PauseTableAsync(string roomId, bool paused, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, PauseLeaf), new CasinoTablePauseRequest(paused),
            AethernetJsonContext.Default.CasinoTablePauseRequest,
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> DealAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, TablePath(roomId, DealLeaf),
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> StartTournamentAsync(string roomId, int hands, long stack,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, TournamentLeaf), new CasinoTableTournamentRequest(hands, stack),
            AethernetJsonContext.Default.CasinoTableTournamentRequest,
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> StopTournamentAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, TablePath(roomId, TournamentStopLeaf),
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> CloseTableAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, TablePath(roomId, CloseLeaf),
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableLedgerDto?> TableLedgerAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(TablePath(roomId, LedgerLeaf), AethernetJsonContext.Default.CasinoTableLedgerDto, token,
            null, onFailure);
    }

    public Task<CasinoLedgerResultDto?> ProposeLedgerEntryAsync(string roomId, CasinoLedgerProposeRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, LedgerLeaf), request,
            AethernetJsonContext.Default.CasinoLedgerProposeRequest,
            AethernetJsonContext.Default.CasinoLedgerResultDto, token, null, onFailure);
    }

    public Task<CasinoLedgerResultDto?> ConfirmLedgerEntryAsync(string entryId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, LedgerEntryPath(entryId, "confirm"),
            AethernetJsonContext.Default.CasinoLedgerResultDto, token, null, onFailure);
    }

    public Task<CasinoLedgerResultDto?> DisputeLedgerEntryAsync(string entryId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, LedgerEntryPath(entryId, "dispute"),
            AethernetJsonContext.Default.CasinoLedgerResultDto, token, null, onFailure);
    }

    public Task<CasinoLedgerListDto?> MyLedgerAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(LedgerPath, AethernetJsonContext.Default.CasinoLedgerListDto, token, null, onFailure);
    }

    public Task<CasinoBlackjackSeatResultDto?> RebuyAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BlackjackRebuyPath, new CasinoBlackjackRebuyRequest(roomId),
            AethernetJsonContext.Default.CasinoBlackjackRebuyRequest,
            AethernetJsonContext.Default.CasinoBlackjackSeatResultDto, token, null, onFailure);
    }

    public Task<CasinoVenueActDto?> VenueActAsync(string roomId, CasinoVenueActRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(VenueActPath(roomId), request, AethernetJsonContext.Default.CasinoVenueActRequest,
            AethernetJsonContext.Default.CasinoVenueActDto, token, null, onFailure);
    }

    public Task<CasinoRoundVerifyDto?> VerifyRoomIndexAsync(string roomId, long index, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(RoomVerifyPath(roomId, index), AethernetJsonContext.Default.CasinoRoundVerifyDto, token,
            null, onFailure);
    }

    public Task<CasinoBonusClaimDto?> ClaimBonusAsync(string kind, string clientActionId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BonusClaimPath(kind), new CasinoBonusClaimRequest(clientActionId),
            AethernetJsonContext.Default.CasinoBonusClaimRequest,
            AethernetJsonContext.Default.CasinoBonusClaimDto, token, null, onFailure);
    }

    public Task<CasinoTableRowDto?> TableAsync(string roomId, Action<int> onStatus, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(string.Concat(TablesPath, "/", Uri.EscapeDataString(roomId)),
            AethernetJsonContext.Default.CasinoTableRowDto, token, onStatus, onFailure);
    }

    public Task<CasinoTableDoorDto?> TableDoorAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(TablePath(roomId, "door"), AethernetJsonContext.Default.CasinoTableDoorDto, token, null,
            onFailure);
    }

    public Task<CasinoTableActionDto?> KnockAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, TablePath(roomId, "knock"),
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> AnswerKnockAsync(string roomId, string userId, bool approve,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, "door"), new CasinoTableDoorRequest(userId, approve),
            AethernetJsonContext.Default.CasinoTableDoorRequest,
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> KickAsync(string roomId, string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, "kick"), new CasinoTableMemberRequest(userId),
            AethernetJsonContext.Default.CasinoTableMemberRequest,
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoTableActionDto?> InviteAsync(string roomId, string[] userIds, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(TablePath(roomId, "invites"), new CasinoTableInviteRequest(userIds),
            AethernetJsonContext.Default.CasinoTableInviteRequest,
            AethernetJsonContext.Default.CasinoTableActionDto, token, null, onFailure);
    }

    public Task<CasinoBlackjackSeatResultDto?> SitAsync(string roomId, int seatIndex, string clientSittingId,
        string clientActionId, long buyIn, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BlackjackSitPath,
            new CasinoBlackjackSitRequest(roomId, seatIndex, clientSittingId, clientActionId, buyIn),
            AethernetJsonContext.Default.CasinoBlackjackSitRequest,
            AethernetJsonContext.Default.CasinoBlackjackSeatResultDto, token, null, onFailure);
    }

    public Task<CasinoBlackjackSeatResultDto?> StandAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(BlackjackLeavePath, new CasinoBlackjackLeaveRequest(roomId),
            AethernetJsonContext.Default.CasinoBlackjackLeaveRequest,
            AethernetJsonContext.Default.CasinoBlackjackSeatResultDto, token, null, onFailure);
    }

    public Task<CasinoBingoCardsDto?> MyBingoCardsAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(BingoMyCardsPath(roomId), AethernetJsonContext.Default.CasinoBingoCardsDto, token, null,
            onFailure);
    }

    public Task<CasinoBlackjackHandStateDto?> MyBlackjackHandAsync(string roomId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(BlackjackMyHandPath(roomId), AethernetJsonContext.Default.CasinoBlackjackHandStateDto,
            token, null, onFailure);
    }

    public Task<CasinoDailySpinDto?> DailySpinStatusAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(DailySpinPath, AethernetJsonContext.Default.CasinoDailySpinDto, token, null, onFailure);
    }

    public Task<CasinoDailySpinDto?> ClaimDailySpinAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, DailySpinPath,
            AethernetJsonContext.Default.CasinoDailySpinDto, token, null, onFailure);
    }
}
