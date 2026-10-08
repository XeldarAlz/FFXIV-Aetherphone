namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoPlinkoDropRequest(
    string SittingId = "",
    string ClientRoundId = "",
    int Rows = 0,
    int Risk = 0,
    long Stake = 0);

internal sealed record CasinoPlinkoDropDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Rows = 0,
    int Risk = 0,
    long Stake = 0,
    int[]? Path = null,
    int Slot = 0,
    int MultiplierTenths = 0,
    long Payout = 0,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0);
