namespace Aetherphone.Core.Casino;

internal sealed class CasinoHostDraftMemory
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public int Game { get; set; }

    public int Currency { get; set; }

    public int Seats { get; set; }

    public int Listing { get; set; }

    public bool Spectators { get; set; }

    public bool FaceUp { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MinBet { get; set; }

    public int MaxBet { get; set; }

    public int BigBlind { get; set; }

    public int Ante { get; set; }

    public int Stack { get; set; }

    public bool PracticeRebuy { get; set; }

    public int Bank { get; set; }

    public int MaxPayout { get; set; }

    public bool PayoutCapped { get; set; }

    public int TurnSeconds { get; set; }

    public bool TimeBank { get; set; }

    public int DealerMode { get; set; }

    public bool AutoDeal { get; set; }

    public int Pays { get; set; }

    public bool HitsSoft17 { get; set; }

    public int Decks { get; set; }

    public int Splits { get; set; }

    public int Doubles { get; set; }

    public bool Charlie { get; set; }

    public bool Peek { get; set; }

    public int Sides { get; set; }

    public bool HighestWins { get; set; }

    public int RoundSeconds { get; set; }

    public int StartAt { get; set; }

    public int Stake { get; set; }

    public bool PinLocation { get; set; } = true;

    public bool IsCurrent => Version == CurrentVersion;
}
