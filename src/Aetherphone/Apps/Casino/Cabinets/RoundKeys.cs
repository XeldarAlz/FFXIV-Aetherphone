using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class RoundKeys
{
    private const int Slots = 4;

    private static readonly string[] RoomIds = new string[Slots];
    private static readonly long[] Rounds = new long[Slots];
    private static readonly string[] Keys = new string[Slots];
    private static int next;

    public static string Of(CasinoRoomSnapshotDto snapshot) => Of(snapshot.RoomId, snapshot.RoundIndex);

    public static string Of(string roomId, long roundIndex)
    {
        for (var slot = 0; slot < Slots; slot++)
        {
            if (Keys[slot] is not null && Rounds[slot] == roundIndex
                && string.Equals(RoomIds[slot], roomId, StringComparison.Ordinal))
            {
                return Keys[slot];
            }
        }

        var key = string.Concat(roomId, "#", roundIndex.ToString(CultureInfo.InvariantCulture));
        RoomIds[next] = roomId;
        Rounds[next] = roundIndex;
        Keys[next] = key;
        next = (next + 1) % Slots;
        return key;
    }
}
