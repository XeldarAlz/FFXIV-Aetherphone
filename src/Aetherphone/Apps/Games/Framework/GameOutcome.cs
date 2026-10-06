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
    public readonly bool IsDraw;
    public readonly bool QuietBest;
    public readonly string StatId;
    public readonly LocString? ContinueLabel;
    public readonly int SecondaryValue;
    public readonly string SecondaryStatId;
    public readonly ScoreKind SecondaryKind;
    public readonly int StatCount;
    private readonly OutcomeStat stat0;
    private readonly OutcomeStat stat1;
    private readonly OutcomeStat stat2;
    private readonly OutcomeStat stat3;

    public GameOutcome(int value, ScoreKind kind, string statId, bool won = true)
        : this(value, kind, won, false, false, statId, null, 0, string.Empty, ScoreKind.Score, 0, default, default,
            default, default)
    {
    }

    private GameOutcome(int value, ScoreKind kind, bool won, bool isDraw, bool quietBest, string statId,
        LocString? continueLabel, int secondaryValue, string secondaryStatId, ScoreKind secondaryKind, int statCount,
        OutcomeStat stat0, OutcomeStat stat1, OutcomeStat stat2, OutcomeStat stat3)
    {
        Value = value;
        Kind = kind;
        Won = won;
        IsDraw = isDraw;
        QuietBest = quietBest;
        StatId = statId;
        ContinueLabel = continueLabel;
        SecondaryValue = secondaryValue;
        SecondaryStatId = secondaryStatId;
        SecondaryKind = secondaryKind;
        StatCount = statCount;
        this.stat0 = stat0;
        this.stat1 = stat1;
        this.stat2 = stat2;
        this.stat3 = stat3;
    }

    public static GameOutcome Drawn(string statId) =>
        new(0, ScoreKind.Streak, false, true, false, statId, null, 0, string.Empty, ScoreKind.Score, 0, default,
            default, default, default);

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
            0 => Copy(1, stat, stat1, stat2, stat3),
            1 => Copy(2, stat0, stat, stat2, stat3),
            2 => Copy(3, stat0, stat1, stat, stat3),
            _ => Copy(4, stat0, stat1, stat2, stat),
        };
    }

    public GameOutcome WithSecondary(string statId, int value, ScoreKind kind = ScoreKind.Score) =>
        new(Value, Kind, Won, IsDraw, QuietBest, StatId, ContinueLabel, value, statId, kind, StatCount, stat0, stat1,
            stat2, stat3);

    public GameOutcome WithContinueLabel(LocString label) =>
        new(Value, Kind, Won, IsDraw, QuietBest, StatId, label, SecondaryValue, SecondaryStatId, SecondaryKind,
            StatCount, stat0, stat1, stat2, stat3);

    public GameOutcome WithQuietBest() =>
        new(Value, Kind, Won, IsDraw, true, StatId, ContinueLabel, SecondaryValue, SecondaryStatId, SecondaryKind,
            StatCount, stat0, stat1, stat2, stat3);

    public bool HasSecondary => SecondaryStatId.Length > 0;

    private GameOutcome Copy(int statCount, OutcomeStat first, OutcomeStat second, OutcomeStat third,
        OutcomeStat fourth) =>
        new(Value, Kind, Won, IsDraw, QuietBest, StatId, ContinueLabel, SecondaryValue, SecondaryStatId, SecondaryKind,
            statCount, first, second, third, fourth);
}
