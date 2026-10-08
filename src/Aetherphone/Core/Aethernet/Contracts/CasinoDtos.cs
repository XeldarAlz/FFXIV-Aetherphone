namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoSittingDto(
    string Id = "",
    string TableId = "",
    string GameKind = "",
    int State = 0,
    long Stack = 0,
    long ChipsIn = 0,
    long ChipsOut = 0,
    long RateChipsPerCoin = 1000);

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
    long[]? LevelCapAnchors = null,
    string[]? Features = null,
    long RateChipsPerCoin = 0,
    long[]? Ladder = null,
    CasinoCashierDto? Cashier = null,
    CasinoBonusDto[]? Bonuses = null,
    CasinoClubDto? Club = null);

internal sealed record CasinoCashierDto(
    long DailyNetCashOutCoins = 0,
    long CashOutCoinsToday = 0,
    long BuyInCoinsToday = 0,
    long AllowanceCoins = 0,
    long QueuedChips = 0);

internal sealed record CasinoBonusDto(
    string Kind = "",
    bool Ready = false,
    long Amount = 0,
    long NextAtUnix = 0,
    int StreakDay = 0,
    bool Eligible = false);

internal sealed record CasinoClubDto(
    int Tier = 0,
    string TierKey = "",
    long Points = 0,
    long TierFloor = 0,
    long NextTierPoints = 0,
    int MultiplierPercent = 100,
    int RebateBasisPoints = 0);

internal sealed record CasinoBonusClaimRequest(string ClientActionId = "");

internal sealed record CasinoBonusClaimDto(
    bool Granted = false,
    string Reason = "",
    string Kind = "",
    long Amount = 0,
    long Stack = 0,
    CasinoSittingDto? Sitting = null,
    long NextAtUnix = 0,
    int StreakDay = 0,
    int Level = 0);

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
    long Balance = 0,
    long ConvertedCoins = 0,
    long QueuedChips = 0);

internal sealed record CasinoLimitRequest(long? SelfLossLimit);

internal sealed record CasinoLimitsDto(
    long LossLimit = 0,
    long? SelfLossLimit = null,
    long? PendingRaiseLimit = null,
    long? PendingRaiseAtUnix = null);

internal sealed record CasinoSlotsSpinRequest(
    string SittingId = "",
    string ClientRoundId = "",
    long Stake = 0,
    string MachineId = "slots.bird",
    string Mode = "base");

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
    long Jackpot = 0,
    long Ceiling = 0,
    string MachineId = "slots.bird",
    long Bet = 0,
    string Mode = "base",
    CasinoSlotsStepDto[]? Steps = null,
    bool BonusTriggered = false,
    int FreeSpinsPlayed = 0,
    int Expander = -1,
    int FeatureMultiplier = 0,
    CasinoSlotsMeterDto[]? Meters = null);

internal sealed record CasinoSlotsWinDto(int Line = 0, int Symbol = 0, int Count = 0, int[]? Cells = null,
    long Pay = 0);

internal sealed record CasinoSlotsCoinDto(int Cell = -1, int Kind = 0, long Multiple = 0, long Value = 0);

internal sealed record CasinoSlotsStepDto(
    string Kind = "",
    int[]? Grid = null,
    CasinoSlotsWinDto[]? Wins = null,
    long Pay = 0,
    int Multiplier = 1,
    int SpinsAdded = 0,
    int SpinsLeft = 0,
    int Expander = -1,
    CasinoSlotsCoinDto[]? Coins = null,
    long Running = 0);

internal sealed record CasinoSlotsMeterDto(
    string Tier = "",
    long Value = 0,
    long MultipleHundredths = 0,
    long ResetHundredths = 0,
    long CeilingHundredths = 0);

internal sealed record CasinoSlotsMetersDto(string MachineId = "", long Bet = 0, CasinoSlotsMeterDto[]? Meters = null);

internal sealed record CasinoSlotsGambleRequest(
    string SittingId = "",
    string ClientRoundId = "",
    string ParentRoundId = "",
    int Pick = 0);

internal sealed record CasinoSlotsGambleDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    string ParentRoundId = "",
    int Step = 0,
    long Stake = 0,
    int Card = 0,
    bool Won = false,
    long Payout = 0,
    bool CanContinue = false,
    string NextSeedHash = "",
    long Stack = 0);

internal sealed record CasinoScratchBuyRequest(string SittingId, string ClientRoundId, int Tier);

internal sealed record CasinoScratchCardDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Tier = 0,
    int[]? Cells = null,
    long Prize = 0,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0);

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
    long ServerNowUnixMs = 0,
    bool Practice = false);

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
    int Occupancy = 0,
    bool Practice = false);

internal sealed record CasinoRoomListDto(
    CasinoRoomListItemDto[]? Rooms = null,
    long ServerNowUnixMs = 0);

internal sealed record CasinoWheelSpotDto(
    int Spot = 0,
    int Multiplier = 0,
    int Segments = 0,
    int ReturnBasisPoints = 0,
    long Capacity = 0,
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
    long MaxWin = 0);

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
    long Stack = 0,
    long Ceiling = 0);

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
    string FrameId = "",
    int TimeBankLeft = 0);

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
    long MaxWin = 0,
    bool Practice = false,
    bool Paused = false,
    int DealerMode = 0,
    string DealerUserId = "",
    string DealerName = "",
    string[]? CoDealers = null,
    bool AutoDeal = true,
    bool FaceUp = false,
    bool PracticeRebuy = false,
    long PracticeStack = 0,
    CasinoBlackjackRuleSheetDto? Rules = null,
    CasinoBlackjackTournamentDto? Tournament = null,
    string Name = "",
    int TurnSeconds = 0,
    int Currency = 0,
    long Bank = 0,
    long BankHeadroom = 0,
    long MaxPayout = 0);

internal sealed record CasinoBlackjackStandingDto(
    string UserId = "",
    string DisplayName = "",
    long Chips = 0,
    bool Eliminated = false,
    int Place = 0);

internal sealed record CasinoBlackjackTournamentDto(
    bool Live = false,
    int Hands = 0,
    int HandsPlayed = 0,
    long Stack = 0,
    string WinnerUserId = "",
    string WinnerName = "",
    CasinoBlackjackStandingDto[]? Standings = null);

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
    string InviteToken = "",
    string Name = "",
    int Listing = 0,
    bool Practice = false,
    bool Paused = false,
    CasinoTableConfigDto? Config = null,
    int Currency = 0,
    CasinoHostReputationDto? Reputation = null);

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

internal sealed record CasinoTableCreateRequest(string ClientTableId, int StakeTier = 0,
    CasinoTableConfigDto? Config = null);

internal sealed record CasinoTableConfigDto(
    string GameKind = "casino.blackjack",
    string Name = "",
    int Seats = 6,
    long MinBet = 0,
    long MaxBet = 0,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    bool Practice = false,
    long PracticeStack = 100000,
    bool PracticeRebuy = true,
    int TurnSeconds = 20,
    int TimeBankUses = 3,
    int Listing = 0,
    bool Spectators = true,
    bool FaceUp = false,
    int DealerMode = 0,
    string[]? CoDealers = null,
    bool AutoDeal = true,
    CasinoBlackjackRuleSheetDto? HouseRules = null,
    CasinoTableLocationDto? Location = null,
    CasinoPokerTableOptionsDto? Poker = null,
    CasinoDiceTableOptionsDto? Dice = null,
    CasinoDeathrollOptionsDto? Deathroll = null,
    int Currency = 0,
    long Bank = 0,
    long MaxPayout = 0);

internal sealed record CasinoBlackjackRuleSheetDto(
    int BlackjackPays = 0,
    bool DealerHitsSoft17 = false,
    int Decks = 6,
    int Splits = 2,
    int Doubles = 0,
    bool FiveCardCharlie = false,
    bool DealerPeek = true);

internal sealed record CasinoTableLocationDto(
    int World = 0,
    int Territory = 0,
    int Ward = 0,
    int Plot = 0,
    int ApartmentWing = 0,
    int Room = 0);

internal sealed record CasinoPokerTableOptionsDto(
    long SmallBlind = 50,
    long Ante = 0,
    int Straddle = 0,
    bool RunItTwice = false,
    int BombPotAnteBb = 0,
    int BombPotPercent = 0,
    long SevenDeuceBounty = 0,
    bool WaitForBigBlind = false,
    bool AutoMuck = true);

internal sealed record CasinoDiceTableOptionsDto(int Sides = 1000, bool HighestWins = false, int RoundSeconds = 60);

internal sealed record CasinoDeathrollOptionsDto(int StartAt = 1000, long Stake = 10000);

internal sealed record CasinoHostReputationDto(
    int GilTablesHosted = 0,
    int PayoutsConfirmed = 0,
    int DisputesOpen = 0,
    bool Frozen = false);

internal sealed record CasinoTableRenameRequest(string Name = "");

internal sealed record CasinoTablePauseRequest(bool Paused = true);

internal sealed record CasinoTableCoDealersRequest(string[]? UserIds = null);

internal sealed record CasinoTableTournamentRequest(int Hands = 20, long Stack = 0);

internal sealed record CasinoBlackjackRebuyRequest(string RoomId = "");

internal sealed record CasinoTableLedgerRowDto(
    string UserId = "",
    string DisplayName = "",
    int SeatIndex = -1,
    long BuyIns = 0,
    long Stack = 0,
    long Net = 0,
    int Hands = 0,
    bool Seated = false);

internal sealed record CasinoTableLedgerDto(
    string TableId = "",
    bool Owner = false,
    bool Practice = false,
    CasinoTableLedgerRowDto[]? Rows = null,
    long ServerNowUnixMs = 0,
    int Currency = 0,
    CasinoLedgerEntryDto[]? Entries = null);

internal sealed record CasinoLedgerEntryDto(
    string EntryId = "",
    string TableId = "",
    string Kind = "",
    long Amount = 0,
    string PayerUserId = "",
    string PayerName = "",
    string PayeeUserId = "",
    string PayeeName = "",
    string ProposedBy = "",
    string Source = "",
    bool PayerConfirmed = false,
    bool PayeeConfirmed = false,
    bool Settled = false,
    bool Disputed = false,
    bool DisputeResolved = false,
    long CreatedAtUnixMs = 0,
    long SettledAtUnixMs = 0);

internal sealed record CasinoLedgerProposeRequest(
    string ClientEntryId = "",
    string Kind = "buyin",
    string CounterpartyUserId = "",
    long Amount = 0,
    string Source = "manual");

internal sealed record CasinoLedgerResultDto(bool Granted = false, string Reason = "",
    CasinoLedgerEntryDto? Entry = null);

internal sealed record CasinoLedgerListDto(CasinoLedgerEntryDto[]? Entries = null, long ServerNowUnixMs = 0);

internal sealed record CasinoVenueActRequest(
    string ClientActionId = "",
    string Action = "",
    int Count = 0,
    string Title = "",
    int Winners = 0,
    int DurationSeconds = 0,
    string OpponentUserId = "",
    long TicketPrice = 0,
    long Prize = 0);

internal sealed record CasinoVenueActDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    long Seq = 0,
    long Value = 0,
    string Seed = "",
    string NextCommit = "");

internal sealed record CasinoVenueRollDto(
    long Seq = 0,
    string UserId = "",
    string DisplayName = "",
    long Bound = 0,
    long Value = 0,
    long AtUnixMs = 0,
    string Seed = "");

internal sealed record CasinoDiceRoundDto(
    long Index = 0,
    long OpenedSeq = 0,
    long EndsAtUnixMs = 0,
    string WinnerUserId = "",
    string WinnerName = "",
    long WinningValue = 0,
    bool Closed = false,
    string[]? Rollers = null);

internal sealed record CasinoDiceTableStateDto(
    string Name = "",
    int Sides = 1000,
    bool HighestWins = false,
    int RoundSeconds = 60,
    long LastSeq = 0,
    string NextCommit = "",
    CasinoVenueRollDto[]? Rolls = null,
    CasinoDiceRoundDto? Round = null,
    CasinoDiceRoundDto? LastRound = null,
    int Currency = 1);

internal sealed record CasinoDeathrollDuelDto(
    long Seq = 0,
    string ChallengerUserId = "",
    string ChallengerName = "",
    string OpponentUserId = "",
    string OpponentName = "",
    long Stake = 0,
    int Phase = 0,
    string TurnUserId = "",
    long Current = 0,
    long TurnEndsAtUnixMs = 0,
    CasinoVenueRollDto[]? Rolls = null,
    string LoserUserId = "");

internal sealed record CasinoVenueStackDto(string UserId = "", string DisplayName = "", long Chips = 0);

internal sealed record CasinoDeathrollStateDto(
    string Name = "",
    int StartAt = 1000,
    long Stake = 10000,
    long PracticeStack = 0,
    long LastSeq = 0,
    string NextCommit = "",
    CasinoDeathrollDuelDto? Duel = null,
    CasinoDeathrollDuelDto[]? Recent = null,
    CasinoVenueStackDto[]? Stacks = null,
    int Currency = 1);

internal sealed record CasinoRaffleEntrantDto(string UserId = "", string DisplayName = "", int Tickets = 0);

internal sealed record CasinoRaffleDto(
    long Seq = 0,
    string Title = "",
    int TicketsPerPerson = 0,
    int Winners = 0,
    long EndsAtUnixMs = 0,
    int Tickets = 0,
    CasinoRaffleEntrantDto[]? Entrants = null,
    bool Drawn = false,
    long DrawSeq = 0,
    string Seed = "",
    CasinoRaffleEntrantDto[]? WinnersDrawn = null,
    long TicketPrice = 0,
    long Prize = 0);

internal sealed record CasinoRaffleStateDto(
    string Name = "",
    long LastSeq = 0,
    string NextCommit = "",
    CasinoRaffleDto? Raffle = null,
    CasinoRaffleDto? Last = null,
    int Currency = 1);

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
    long Stack = 0,
    long Ceiling = 0);

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
