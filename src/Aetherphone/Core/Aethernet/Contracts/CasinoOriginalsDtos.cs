namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoMinesStartRequest(string SittingId, string ClientRoundId, long Stake, int Mines);

internal sealed record CasinoMinesRevealRequest(string RoundId, int Tile);

internal sealed record CasinoOriginalsRoundRequest(string RoundId);

internal sealed record CasinoMinesDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Stake = 0,
    int Mines = 0,
    int Phase = 0,
    int[]? Revealed = null,
    int[]? MineTiles = null,
    long MultiplierTenThousandths = 0,
    long NextMultiplierTenThousandths = 0,
    long Payout = 0,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0,
    bool Capped = false);

internal sealed record CasinoDiceRollRequest(string SittingId, string ClientRoundId, long Stake, int Target,
    bool Over);

internal sealed record CasinoDiceDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Stake = 0,
    int Target = 0,
    bool Over = false,
    int Chance = 0,
    int Roll = 0,
    bool Won = false,
    long MultiplierTenThousandths = 0,
    long Payout = 0,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0,
    bool Capped = false);

internal sealed record CasinoLimboPlayRequest(string SittingId, string ClientRoundId, long Stake, int Target);

internal sealed record CasinoLimboDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Stake = 0,
    int Target = 0,
    int Result = 0,
    bool Won = false,
    long Payout = 0,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0,
    bool Capped = false);

internal sealed record CasinoKenoDrawRequest(string SittingId, string ClientRoundId, long Stake, int Risk,
    int[] Picks);

internal sealed record CasinoKenoDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Stake = 0,
    int Risk = 0,
    int[]? Picks = null,
    int[]? Drawn = null,
    int Hits = 0,
    long MultiplierTenThousandths = 0,
    long Payout = 0,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0,
    bool Capped = false);

internal sealed record CasinoHiLoStartRequest(string SittingId, string ClientRoundId, long Stake);

internal sealed record CasinoHiLoGuessRequest(string RoundId, int Step, string Call);

internal sealed record CasinoHiLoSkipRequest(string RoundId, int Step);

internal sealed record CasinoHiLoCallDto(string Call = "", int ChanceBasisPoints = 0, long MultiplierTenThousandths = 0);

internal sealed record CasinoHiLoDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Stake = 0,
    int Phase = 0,
    int Step = 0,
    int[]? Cards = null,
    string[]? Moves = null,
    int Card = 0,
    long MultiplierTenThousandths = 0,
    CasinoHiLoCallDto[]? Calls = null,
    long Payout = 0,
    string NextSeedHash = "",
    long Stack = 0,
    long Ceiling = 0,
    bool Capped = false);

internal sealed record CasinoOriginalsOpenDto(CasinoMinesDto? Mines = null, CasinoHiLoDto? HiLo = null);
