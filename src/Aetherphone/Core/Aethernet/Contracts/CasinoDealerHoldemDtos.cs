namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoDealerHoldemStartRequest(
    string SittingId = "",
    string ClientRoundId = "",
    long Ante = 0,
    long Trips = 0);

internal sealed record CasinoDealerHoldemDecideRequest(
    string RoundId = "",
    int Step = 0,
    string Action = "",
    int Multiple = 0);

internal sealed record CasinoDealerHoldemDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Ante = 0,
    long Blind = 0,
    long Trips = 0,
    long Play = 0,
    int PlayMultiple = 0,
    long Stake = 0,
    int Phase = 0,
    int Step = 0,
    string[]? Actions = null,
    int[]? Multiples = null,
    int[]? PlayerCards = null,
    int[]? Board = null,
    int[]? DealerCards = null,
    int PlayerHand = -1,
    int DealerHand = -1,
    int[]? PlayerBest = null,
    int[]? DealerBest = null,
    bool DealerQualifies = false,
    bool Folded = false,
    int Outcome = 0,
    long AntePayout = 0,
    long BlindPayout = 0,
    long PlayPayout = 0,
    long TripsPayout = 0,
    long Payout = 0,
    bool Capped = false,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0);

internal sealed record CasinoDealerHoldemOpenDto(CasinoDealerHoldemDto? Round = null);
