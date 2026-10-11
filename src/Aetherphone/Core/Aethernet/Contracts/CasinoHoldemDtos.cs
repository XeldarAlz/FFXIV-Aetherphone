namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoHoldemSeatDto(
    int SeatIndex = -1,
    string UserId = "",
    string DisplayName = "",
    string AvatarUrl = "",
    string FrameId = "",
    string Title = "",
    long Stack = 0,
    long Bet = 0,
    long Committed = 0,
    int State = 0,
    bool ActedThisStreet = false,
    string LastAction = "",
    int TimeBankLeft = 0,
    bool Connected = false,
    bool Leaving = false,
    int[]? Cards = null,
    bool Shown = false,
    int HandRank = -1,
    int[]? Best5 = null,
    long Won = 0,
    long SittingOutEndsAtUnixMs = 0);

internal sealed record CasinoHoldemPotDto(long Amount = 0, int[]? Eligible = null);

internal sealed record CasinoHoldemWinnerDto(
    int SeatIndex = -1,
    int PotIndex = 0,
    long Amount = 0,
    int Strength = 0,
    int[]? Best5 = null);

internal sealed record CasinoHoldemRoomStateDto(
    string HandId = "",
    long HandIndex = 0,
    int Phase = 0,
    int Button = -1,
    long SmallBlind = 0,
    long BigBlind = 0,
    long Ante = 0,
    CasinoHoldemSeatDto[]? Seats = null,
    int[]? Board = null,
    CasinoHoldemPotDto[]? Pots = null,
    long PotTotal = 0,
    long ToCall = 0,
    long MinRaiseTo = 0,
    int CursorSeat = -1,
    int ActionCount = 0,
    long DeadlineUnixMs = 0,
    int WindowSeconds = 0,
    string Commit = "",
    string NextCommit = "",
    string Seed = "",
    long Rake = 0,
    int RakeBasisPoints = 0,
    long RakeCap = 0,
    CasinoHoldemWinnerDto[]? Winners = null,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    int MaxSeats = 0,
    int TurnSeconds = 0,
    int TimeBankSeconds = 0,
    bool Practice = false,
    string Name = "",
    int StakeTier = 0,
    bool FaceUp = false,
    bool PracticeRebuy = false,
    long PracticeStack = 0,
    bool Paused = false);

internal sealed record CasinoHoldemPromptDto(
    int Actions = 0,
    long ToCall = 0,
    long MinRaiseTo = 0,
    long MaxRaiseTo = 0,
    long PotTotal = 0,
    long DeadlineUnixMs = 0,
    int TimeBankLeft = 0);

internal sealed record CasinoHoldemYouDto(
    string HandId = "",
    int SeatIndex = -1,
    int[]? Cards = null,
    int ActionCount = 0,
    long Stack = 0,
    int Strength = -1,
    int WinChance = -1,
    CasinoHoldemPromptDto? Prompt = null);

internal sealed record CasinoHoldemSitRequest(
    string RoomId = "",
    int SeatIndex = 0,
    string ClientSittingId = "",
    string ClientActionId = "",
    long BuyIn = 0,
    bool PostBigBlind = false);

internal sealed record CasinoHoldemLeaveRequest(string RoomId = "");

internal sealed record CasinoHoldemActRequest(
    string RoomId = "",
    string HandId = "",
    int ActionCount = 0,
    string ClientActionId = "",
    string Action = "",
    long Amount = 0);

internal sealed record CasinoHoldemTopUpRequest(string RoomId = "", string ClientActionId = "", long Amount = 0);

internal sealed record CasinoHoldemTimeBankRequest(string RoomId = "", string HandId = "", int ActionCount = 0);

internal sealed record CasinoHoldemSitOutRequest(string RoomId = "", bool SitOut = true, bool PostBigBlind = false);

internal sealed record CasinoHoldemSeatResultDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    int SeatIndex = -1,
    CasinoSittingDto? Sitting = null,
    long Balance = 0,
    long Stack = 0);

internal sealed record CasinoHoldemActionResultDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    string HandId = "",
    int SeatIndex = -1,
    int ActionCount = 0,
    long Stack = 0);

internal sealed record CasinoHoldemHistorySeatDto(
    int SeatIndex = -1,
    string UserId = "",
    string DisplayName = "",
    int[]? Cards = null,
    long Committed = 0,
    long Won = 0,
    bool Folded = false,
    bool Shown = false,
    int Strength = -1);

internal sealed record CasinoHoldemHistoryHandDto(
    string HandId = "",
    long HandIndex = 0,
    long StartedAtUnixMs = 0,
    int Button = -1,
    long SmallBlind = 0,
    long BigBlind = 0,
    int[]? Board = null,
    long Rake = 0,
    long PotTotal = 0,
    string Commit = "",
    string Seed = "",
    CasinoHoldemHistorySeatDto[]? Seats = null,
    long Net = 0);

internal sealed record CasinoHoldemHistoryDto(string RoomId = "", CasinoHoldemHistoryHandDto[]? Hands = null);
