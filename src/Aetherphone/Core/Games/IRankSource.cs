namespace Aetherphone.Core.Games;

internal interface IRankSource
{
    bool TryGetRank(string statId, out GameRank rank);
}
