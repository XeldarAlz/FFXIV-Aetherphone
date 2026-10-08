namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoSittingDto(
    string Id = "",
    string TableId = "",
    string GameKind = "",
    int State = 0,
    long Stack = 0,
    long ChipsIn = 0,
    long ChipsOut = 0);

internal sealed record CasinoStateDto(
    bool StakesPaused = false,
    bool Draining = false,
    CasinoSittingDto? Sitting = null,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    long DailyBuyInCap = 0,
    long LossLimit = 0,
    long LossHeadroom = 0,
    long? SelfLossLimit = null,
    long? PendingRaiseLimit = null,
    long? PendingRaiseAtUnix = null,
    long NetLossToday = 0,
    long AtRisk = 0,
    long BuyInToday = 0,
    long Balance = 0,
    CasinoSittingDto? TableSitting = null,
    long Jackpot = 0,
    CasinoProgressDto? Progress = null,
    CasinoCeilingDto? Ceiling = null,
    long[]? LevelCapAnchors = null);

internal sealed record CasinoProgressDto(
    int Level = 1,
    long Xp = 0,
    long LevelStartXp = 0,
    long NextLevelXp = 0,
    long LifetimeWagered = 0,
    long LifetimeWon = 0,
    int BestMultiplierTenths = 0,
    string Title = "");

internal sealed record CasinoCeilingDto(
    long MaxBet = 0,
    long LevelCap = 0,
    long BalanceCap = 0,
    long Balance = 0,
    string Reason = "",
    long NextLevelCap = 0);

internal sealed record CasinoOpenSittingRequest(
    string ClientSittingId,
    string ClientActionId,
    int TableKind,
    long Amount);

internal sealed record CasinoTopUpRequest(string SittingId, string ClientActionId, long Amount);

internal sealed record CasinoCloseSittingRequest(string SittingId);

internal sealed record CasinoSittingResultDto(
    bool Granted = false,
    string Reason = "",
    CasinoSittingDto? Sitting = null,
    long Balance = 0);

internal sealed record CasinoLimitRequest(long? SelfLossLimit);

internal sealed record CasinoLimitsDto(
    long LossLimit = 0,
    long? SelfLossLimit = null,
    long? PendingRaiseLimit = null,
    long? PendingRaiseAtUnix = null);

internal sealed record CasinoSlotsSpinRequest(string SittingId, string ClientRoundId, long Stake);

internal sealed record CasinoSlotsLineWinDto(int Line = 0, int Symbol = 0, int Count = 0, long Pay = 0);

internal sealed record CasinoSlotsSpinResultDto(
    int[]? Grid = null,
    CasinoSlotsLineWinDto[]? LineWins = null,
    int ScatterCount = 0,
    long ScatterPay = 0,
    long Win = 0,
    int SpinsAdded = 0);

internal sealed record CasinoSlotsSpinDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Stake = 0,
    CasinoSlotsSpinResultDto? BaseSpin = null,
    CasinoSlotsSpinResultDto[]? FreeSpins = null,
    long TotalWin = 0,
    bool CapApplied = false,
    string NextSeedHash = "",
    long Stack = 0,
    long Jackpot = 0);

internal sealed record CasinoScratchBuyRequest(string SittingId, string ClientRoundId, int Tier);

internal sealed record CasinoScratchCardDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Tier = 0,
    int[]? Cells = null,
    long Prize = 0,
    string NextSeedHash = "",
    long Stack = 0);

internal sealed record CasinoBarkeepStartRequest(string SittingId, string ClientRoundId);

internal sealed record CasinoBarkeepPatronDto(int ArrivalSecond = 0, int[]? StepKinds = null);

internal sealed record CasinoBarkeepStartDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    CasinoBarkeepPatronDto[]? Patrons = null,
    int MaxScore = 0,
    long StartedAtUnix = 0,
    long ExpiresAtUnix = 0,
    string NextSeedHash = "",
    long Stack = 0);

internal sealed record CasinoBarkeepOrderRequest(int[] StepGrades);

internal sealed record CasinoBarkeepFinishRequest(string RoundId, CasinoBarkeepOrderRequest[] Orders);

internal sealed record CasinoBarkeepFinishDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Score = 0,
    long Payout = 0,
    long NetWinToday = 0,
    long Stack = 0);

internal sealed record CasinoRoundVerifyDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    string GameKind = "",
    int State = 0,
    long Stake = 0,
    long Payout = 0,
    string SeedCommitHash = "",
    string SeedRevealed = "",
    string NextSeedHash = "",
    string DrawLog = "",
    string StreamBinding = "");

internal sealed record CasinoRoundHistoryDto(
    string RoundId = "",
    string GameKind = "",
    long Stake = 0,
    long Payout = 0,
    int State = 0,
    long CreatedAtUnix = 0,
    long? SettledAtUnix = null,
    string SeedCommitHash = "",
    bool Revealed = false);

internal sealed record CasinoRoundHistoryPage(
    CasinoRoundHistoryDto[]? Items = null,
    string? NextCursor = null);

internal sealed record CasinoRoomSnapshotDto(
    string RoomId = "",
    string GameKind = "",
    int Kind = 0,
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    string GameState = "",
    int Occupancy = 0,
    bool Attached = false,
    int Epoch = 0,
    long Seq = 0,
    long ServerNowUnixMs = 0);

internal sealed record CasinoRoomEventDto(
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    string GameState = "",
    int Occupancy = 0);

internal sealed record CasinoPrivateDto(string EventKind = "", string Payload = "");

internal sealed record CasinoRoomListItemDto(
    string RoomId = "",
    string GameKind = "",
    int Kind = 0,
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    int Occupancy = 0);

internal sealed record CasinoRoomListDto(
    CasinoRoomListItemDto[]? Rooms = null,
    long ServerNowUnixMs = 0);

internal sealed record CasinoWheelSpotDto(
    int Spot = 0,
    int Multiplier = 0,
    int Segments = 0,
    int ReturnBasisPoints = 0,
    long Amount = 0,
    int Bettors = 0);

internal sealed record CasinoWheelRoomStateDto(
    long RoundIndex = 0,
    string Commit = "",
    string NextCommit = "",
    string Seed = "",
    int Segment = -1,
    int Spot = -1,
    CasinoWheelSpotDto[]? Spots = null,
    long Staked = 0,
    long Paid = 0,
    int[]? Recent = null,
    long MinBet = 0,
    long MaxBetPerSpot = 0,
    long MaxBetPerRound = 0,
    long MaxWin = 0);

internal sealed record CasinoBingoStageDto(
    int Stage = 0,
    int Ball = 0,
    long Prize = 0,
    int Winners = 0,
    long Paid = 0);

internal sealed record CasinoBingoRoomStateDto(
    long RoundIndex = 0,
    string Commit = "",
    string NextCommit = "",
    string Seed = "",
    int Cards = 0,
    int Players = 0,
    long[]? Prizes = null,
    int PrizeCardCap = 0,
    int BallIndex = 0,
    int[]? Balls = null,
    long NextBallAtUnixMs = 0,
    CasinoBingoStageDto[]? Stages = null,
    bool Ended = false,
    bool Cancelled = false,
    long CardPrice = 0,
    int MaxCards = 0,
    long MaxWin = 0,
    int BallIntervalMs = 0,
    bool EarlyBird = false);

internal sealed record CasinoWheelBetRequest(
    string RoomId,
    long RoundIndex,
    string ClientRoundId,
    string ClientBetId,
    int Spot,
    long Amount);

internal sealed record CasinoWheelBetDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    long RoundIndex = 0,
    string RoundId = "",
    int Spot = 0,
    long Amount = 0,
    long MyStake = 0,
    long Stack = 0);

internal sealed record CasinoWheelMyBetDto(int Spot = 0, long Amount = 0);

internal sealed record CasinoWheelBetsDto(
    string RoomId = "",
    long RoundIndex = 0,
    int Phase = 0,
    string RoundId = "",
    CasinoWheelMyBetDto[]? Bets = null,
    long MyStake = 0,
    long Stack = 0);

internal sealed record CasinoBlackjackHandDto(
    int[]? Cards = null,
    long Bet = 0,
    int Total = 0,
    bool Soft = false,
    bool Doubled = false,
    bool Stood = false,
    bool Busted = false,
    bool Natural = false,
    int Outcome = 0,
    long Delta = 0,
    bool SplitAces = false);

internal sealed record CasinoBlackjackSeatDto(
    int SeatIndex = -1,
    string UserId = "",
    string DisplayName = "",
    long Chips = 0,
    int State = 0,
    bool Connected = false,
    bool JoinsNextHand = false,
    bool LeaveAtHandEnd = false,
    long Committed = 0,
    long HeldUntilUnixMs = 0,
    CasinoBlackjackHandDto[]? Hands = null,
    string AvatarUrl = "",
    string FrameId = "");

internal sealed record CasinoBlackjackRoomStateDto(
    string HandId = "",
    long HandIndex = 0,
    int Phase = 0,
    string Commit = "",
    string NextCommit = "",
    string Seed = "",
    int[]? DealerCards = null,
    int DealerTotal = 0,
    bool DealerSoft = false,
    int ActiveSeat = -1,
    int ActiveHand = -1,
    int ActionCount = 0,
    long DeadlineUnixMs = 0,
    int WindowSeconds = 0,
    CasinoBlackjackSeatDto[]? Seats = null,
    long MinBet = 0,
    long MaxBet = 0,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    long MaxWin = 0);

internal sealed record CasinoBlackjackYouDto(
    string HandId = "",
    int SeatIndex = -1,
    int ActiveHand = -1,
    int ActionCount = 0,
    int ActionsMask = 0,
    long DeadlineUnixMs = 0,
    long Chips = 0,
    CasinoBlackjackHandDto[]? Hands = null);

internal sealed record CasinoBlackjackHandStateDto(
    string RoomId = "",
    int Epoch = 0,
    long Seq = 0,
    string EventKind = "",
    string Payload = "",
    long ServerNowUnixMs = 0);

internal sealed record CasinoBlackjackSitRequest(
    string RoomId,
    int SeatIndex,
    string ClientSittingId,
    string ClientActionId,
    long BuyIn);

internal sealed record CasinoBlackjackLeaveRequest(string RoomId);

internal sealed record CasinoBlackjackSeatResultDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    int SeatIndex = -1,
    CasinoSittingDto? Sitting = null,
    long Balance = 0);

internal sealed record CasinoBlackjackBetRequest(
    string RoomId,
    string ClientRoundId,
    string ClientActionId,
    long Amount);

internal sealed record CasinoBlackjackActionRequest(
    string RoomId,
    string HandId,
    int ActionCount,
    string Action,
    string ClientActionId);

internal sealed record CasinoBlackjackActionResultDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    string HandId = "",
    int SeatIndex = -1,
    int ActionCount = 0,
    long Stack = 0);

internal sealed record CasinoTableRowDto(
    string TableId = "",
    string GameKind = "",
    int Kind = 0,
    int StakeTier = 0,
    string OwnerUserId = "",
    string OwnerName = "",
    long MinBet = 0,
    long MaxBet = 0,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    int MaxSeats = 0,
    int SeatedCount = 0,
    int Occupancy = 0,
    bool Admitted = false,
    string Reason = "",
    string InviteToken = "");

internal sealed record CasinoTableListDto(
    CasinoTableRowDto[]? Tables = null,
    long ServerNowUnixMs = 0);

internal sealed record CasinoQuickSeatRequest(string GameKind, int StakeTier);

internal sealed record CasinoQuickSeatDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    string Name = "",
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    long SuggestedBuyIn = 0,
    long MinBet = 0,
    long MaxBet = 0,
    int SeatIndex = -1);

internal sealed record CasinoTableCreateRequest(string ClientTableId, int StakeTier);

internal sealed record CasinoTableResultDto(
    bool Granted = false,
    string Reason = "",
    CasinoTableRowDto? Table = null);

internal sealed record CasinoTableKnockDto(
    string UserId = "",
    string DisplayName = "",
    long CreatedAtUnixMs = 0);

internal sealed record CasinoTableSeatedDto(
    string UserId = "",
    string DisplayName = "",
    int SeatIndex = -1);

internal sealed record CasinoTableDoorDto(
    string RoomId = "",
    bool Owner = false,
    string InviteToken = "",
    CasinoTableKnockDto[]? Knocks = null,
    CasinoTableSeatedDto[]? Seated = null,
    long ServerNowUnixMs = 0);

internal sealed record CasinoTableDoorRequest(string UserId, bool Approve);

internal sealed record CasinoTableMemberRequest(string UserId);

internal sealed record CasinoTableInviteRequest(string[] UserIds);

internal sealed record CasinoTableActionDto(bool Granted = false, string Reason = "");

internal sealed record CasinoBingoCardsRequest(
    string RoomId,
    long RoundIndex,
    string ClientRoundId,
    int CardCount);

internal sealed record CasinoBingoCardsDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    long RoundIndex = 0,
    string RoundId = "",
    int[][]? Cards = null,
    long Stake = 0,
    long Payout = 0,
    int RoundState = 0,
    string SeedCommitHash = "",
    string NextSeedHash = "",
    long Stack = 0);

internal sealed record CasinoDailySpinDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Segment = -1,
    long SegmentAward = 0,
    long Amount = 0,
    long Balance = 0,
    long NextSpinAtUnix = 0,
    string SeedCommitHash = "",
    string NextSeedHash = "",
    bool Claimed = false);
