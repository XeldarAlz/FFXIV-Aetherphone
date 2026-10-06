namespace Aetherphone.Core.Games;

internal enum RankState : byte
{
    Unknown,
    Uploading,
    Ranked,
    SignedOut,
    Failed,
}

internal readonly struct GameRank
{
    public readonly int Rank;
    public readonly int Total;
    public readonly int FriendsRank;
    public readonly int WeekRank;
    public readonly RankState State;

    public GameRank(int rank, int total, int friendsRank, int weekRank, RankState state)
    {
        Rank = rank;
        Total = total;
        FriendsRank = friendsRank;
        WeekRank = weekRank;
        State = state;
    }

    public static GameRank Unknown => default;

    public bool IsRanked => State == RankState.Ranked && Rank > 0;
}
