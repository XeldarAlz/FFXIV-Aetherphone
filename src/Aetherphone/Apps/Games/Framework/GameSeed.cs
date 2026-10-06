using System.Diagnostics;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameSeed
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public static ulong Fresh() => (ulong)Environment.TickCount64 ^ (ulong)Stopwatch.GetTimestamp();

    public static ulong Daily(string gameId, int dayIndex)
    {
        var hash = FnvOffset;
        for (var index = 0; index < gameId.Length; index++)
        {
            hash ^= gameId[index];
            hash *= FnvPrime;
        }

        hash ^= (ulong)(uint)dayIndex;
        hash *= FnvPrime;
        hash ^= hash >> 29;
        hash *= 0x9E3779B97F4A7C15UL;
        return hash ^ (hash >> 32);
    }
}
