namespace Aetherphone.Core.Games;

internal enum LeaderboardScope : byte
{
    Global,
    Friends,
}

internal enum LeaderboardSpan : byte
{
    All,
    Week,
}

internal readonly record struct LeaderboardKey(string StatId, LeaderboardScope Scope, LeaderboardSpan Span);
