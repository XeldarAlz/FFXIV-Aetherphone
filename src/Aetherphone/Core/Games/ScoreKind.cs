namespace Aetherphone.Core.Games;

internal enum ScoreKind : byte
{
    Score,
    Time,
    Level,
    Streak,
    Count,
}

internal static class ScoreKinds
{
    public static bool LowerIsBetter(ScoreKind kind) => kind is ScoreKind.Time or ScoreKind.Count;

    public static ScoreKind Wire(ScoreKind kind) => kind == ScoreKind.Count ? ScoreKind.Time : kind;
}
