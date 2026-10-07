using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal readonly struct OutcomeStat
{
    public readonly LocString Label;
    public readonly string Value;

    public OutcomeStat(LocString label, string value)
    {
        Label = label;
        Value = value;
    }
}

internal readonly struct GameOutcome
{
    public const int MaxStats = 4;
    public const int NoStars = -1;
    public const int NoSeat = -1;

    public GameOutcome(int value, ScoreKind kind, string statId, bool won = true)
    {
        Value = value;
        Kind = kind;
        StatId = statId;
        Won = won;
        SecondaryStatId = string.Empty;
    }

    public int Value { get; private init; }

    public ScoreKind Kind { get; private init; }

    public bool Won { get; private init; }

    public bool IsDraw { get; private init; }

    public bool QuietBest { get; private init; }

    public string StatId { get; private init; }

    public LocString? ContinueLabel { get; private init; }

    public int SecondaryValue { get; private init; }

    public string SecondaryStatId { get; private init; }

    public ScoreKind SecondaryKind { get; private init; }

    public int StatCount { get; private init; }

    public int Stars => StarsPlusOne - 1;

    public bool IsUnranked { get; private init; }

    public int WinnerSeat => WinnerSeatPlusOne - 1;

    private int StarsPlusOne { get; init; }

    private int WinnerSeatPlusOne { get; init; }

    private OutcomeStat FirstStat { get; init; }

    private OutcomeStat SecondStat { get; init; }

    private OutcomeStat ThirdStat { get; init; }

    private OutcomeStat FourthStat { get; init; }

    public bool HasSecondary => SecondaryStatId.Length > 0;

    public bool HasStars => Stars >= 0;

    public bool HasWinner => WinnerSeat >= 0;

    public static GameOutcome Drawn(string statId) =>
        new GameOutcome(0, ScoreKind.Streak, statId, false) with { IsDraw = true };

    public static GameOutcome Unranked(bool won = true) =>
        new GameOutcome(0, ScoreKind.Score, string.Empty, won) with { IsUnranked = true };

    public OutcomeStat Stat(int index) => index switch
    {
        0 => FirstStat,
        1 => SecondStat,
        2 => ThirdStat,
        _ => FourthStat,
    };

    public GameOutcome WithStat(LocString label, string value)
    {
        var stat = new OutcomeStat(label, value);
        return StatCount switch
        {
            0 => this with { FirstStat = stat, StatCount = 1 },
            1 => this with { SecondStat = stat, StatCount = 2 },
            2 => this with { ThirdStat = stat, StatCount = 3 },
            3 => this with { FourthStat = stat, StatCount = MaxStats },
            _ => this,
        };
    }

    public GameOutcome WithSecondary(string statId, int value, ScoreKind kind = ScoreKind.Score) =>
        this with { SecondaryStatId = statId, SecondaryValue = value, SecondaryKind = kind };

    public GameOutcome WithContinueLabel(LocString label) => this with { ContinueLabel = label };

    public GameOutcome WithQuietBest() => this with { QuietBest = true };

    public GameOutcome WithStars(int stars) =>
        this with { StarsPlusOne = Math.Clamp(stars, 0, GameStatsStore.MaxStars) + 1 };

    public GameOutcome WithWinner(int seat) =>
        this with { WinnerSeatPlusOne = Math.Clamp(seat, NoSeat, GameSeats.Max - 1) + 1 };
}
