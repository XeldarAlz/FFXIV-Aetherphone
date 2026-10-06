namespace Aetherphone.Core.Games;

internal sealed class NullRankSource : IRankSource
{
    public static readonly NullRankSource Instance = new();

    public bool TryGetRank(string statId, out GameRank rank)
    {
        rank = GameRank.Unknown;
        return false;
    }
}
