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

    public readonly int Value;
    public readonly ScoreKind Kind;
    public readonly bool Won;
    public readonly string StatId;
    public readonly int SecondaryValue;
    public readonly string SecondaryStatId;
    public readonly ScoreKind SecondaryKind;
    public readonly int StatCount;
    private readonly OutcomeStat stat0;
    private readonly OutcomeStat stat1;
    private readonly OutcomeStat stat2;
    private readonly OutcomeStat stat3;

    public GameOutcome(int value, ScoreKind kind, string statId, bool won = true)
        : this(value, kind, won, statId, 0, string.Empty, ScoreKind.Score, 0, default, default, default, default)
    {
    }

    private GameOutcome(int value, ScoreKind kind, bool won, string statId, int secondaryValue,
        string secondaryStatId, ScoreKind secondaryKind, int statCount, OutcomeStat stat0, OutcomeStat stat1,
        OutcomeStat stat2, OutcomeStat stat3)
    {
        Value = value;
        Kind = kind;
        Won = won;
        StatId = statId;
        SecondaryValue = secondaryValue;
        SecondaryStatId = secondaryStatId;
        SecondaryKind = secondaryKind;
        StatCount = statCount;
        this.stat0 = stat0;
        this.stat1 = stat1;
        this.stat2 = stat2;
        this.stat3 = stat3;
    }

    public OutcomeStat Stat(int index) => index switch
    {
        0 => stat0,
        1 => stat1,
        2 => stat2,
        _ => stat3,
    };

    public GameOutcome WithStat(LocString label, string value)
    {
        if (StatCount >= MaxStats)
        {
            return this;
        }

        var stat = new OutcomeStat(label, value);
        return StatCount switch
        {
            0 => new GameOutcome(Value, Kind, Won, StatId, SecondaryValue, SecondaryStatId, SecondaryKind, 1, stat,
                stat1, stat2, stat3),
            1 => new GameOutcome(Value, Kind, Won, StatId, SecondaryValue, SecondaryStatId, SecondaryKind, 2, stat0,
                stat, stat2, stat3),
            2 => new GameOutcome(Value, Kind, Won, StatId, SecondaryValue, SecondaryStatId, SecondaryKind, 3, stat0,
                stat1, stat, stat3),
            _ => new GameOutcome(Value, Kind, Won, StatId, SecondaryValue, SecondaryStatId, SecondaryKind, 4, stat0,
                stat1, stat2, stat),
        };
    }

    public GameOutcome WithSecondary(string statId, int value, ScoreKind kind = ScoreKind.Score) =>
        new(Value, Kind, Won, StatId, value, statId, kind, StatCount, stat0, stat1, stat2, stat3);

    public bool HasSecondary => SecondaryStatId.Length > 0;
}
