using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Games;

internal sealed class LeaderboardBoard
{
    public static readonly LeaderboardBoard Empty = new(null, false, AepFailure.None, 0, 0);

    public readonly GameLeaderboardDto? Data;
    public readonly bool Loading;
    public readonly AepFailure Failure;
    public readonly long LoadedAtTick;
    public readonly long AttemptedAtTick;

    public LeaderboardBoard(GameLeaderboardDto? data, bool loading, AepFailure failure, long loadedAtTick,
        long attemptedAtTick)
    {
        Data = data;
        Loading = loading;
        Failure = failure;
        LoadedAtTick = loadedAtTick;
        AttemptedAtTick = attemptedAtTick;
    }

    public bool Loaded => Data is not null;

    public bool Failed => Failure.Failed && !Loading;
}
