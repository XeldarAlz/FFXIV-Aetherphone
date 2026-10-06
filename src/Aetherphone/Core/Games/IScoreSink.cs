namespace Aetherphone.Core.Games;

internal readonly struct ScoreSubmission
{
    public readonly string StatId;
    public readonly int Value;
    public readonly ScoreKind Kind;
    public readonly ulong Seed;
    public readonly bool Daily;
    public readonly string GameId;

    public ScoreSubmission(string statId, int value, ScoreKind kind, ulong seed, bool daily, string gameId)
    {
        StatId = statId;
        Value = value;
        Kind = kind;
        Seed = seed;
        Daily = daily;
        GameId = gameId;
    }
}

internal interface IScoreSink
{
    void Submit(in ScoreSubmission submission);
}
