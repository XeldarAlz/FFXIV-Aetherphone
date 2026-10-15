using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Live;

internal sealed class HoldemRoomRows
{
    private readonly HouseRoomRow[] rows = new HouseRoomRow[HoldemRules.TierCount];

    private CasinoTableRowDto[]? source;
    private LanguageInfo? language;
    private bool royalLocked;

    public HouseRoomRow[] Rows => rows;

    public void Refresh(CasinoTableRowDto[] tables, bool locked)
    {
        if (ReferenceEquals(tables, source) && ReferenceEquals(language, Loc.Current) && locked == royalLocked)
        {
            return;
        }

        source = tables;
        language = Loc.Current;
        royalLocked = locked;
        for (var tier = 0; tier < rows.Length; tier++)
        {
            rows[tier] = RowOf(HoldemPit.HouseView(tier, LiveBoard.Find(tables, HoldemRules.HouseRooms[tier]), locked),
                tier);
        }
    }

    internal static bool Seats(in HoldemRoomView room) => !room.Locked && room.Seated < room.MaxSeats;

    internal static HouseRoomRow RowOf(in HoldemRoomView room, int tier)
    {
        var seats = Seats(room);
        return new HouseRoomRow(room.RoomId, room.Name, room.Locked ? Loc.T(L.Holdem.RoyalLocked) : room.BuyIn,
            string.Empty, room.Seated, room.MaxSeats, Loc.T(seats ? L.LivePage.Sit : L.LivePage.Watch), seats,
            tier == HoldemRules.RoyalTier, room.Locked);
    }
}
