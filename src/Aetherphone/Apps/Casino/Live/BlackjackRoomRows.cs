using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Live;

internal sealed class BlackjackRoomRows
{
    private static readonly LocString[] Names =
    {
        L.LivePage.BlackjackPit, L.LivePage.BlackjackParlour, L.LivePage.BlackjackSalon, L.LivePage.BlackjackVault,
    };

    private readonly HouseRoomRow[] rows = new HouseRoomRow[CasinoRoomIds.BlackjackHouse.Length];

    private CasinoTableRowDto[]? source;
    private LanguageInfo? language;
    private string seatedAt = string.Empty;
    private long ceiling;

    public HouseRoomRow[] Rows => rows;

    public void Refresh(CasinoTableRowDto[] tables, string seatedTableId, long playerMaxBet)
    {
        if (ReferenceEquals(tables, source) && ReferenceEquals(language, Loc.Current)
            && string.Equals(seatedTableId, seatedAt, StringComparison.Ordinal) && playerMaxBet == ceiling)
        {
            return;
        }

        source = tables;
        language = Loc.Current;
        seatedAt = seatedTableId;
        ceiling = playerMaxBet;
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = RowOf(index, LiveBoard.Find(tables, CasinoRoomIds.BlackjackHouse[index]), seatedTableId,
                playerMaxBet);
        }
    }

    internal static HouseRoomRow RowOf(int index, CasinoTableRowDto? table, string seatedTableId, long playerMaxBet)
    {
        var roomId = CasinoRoomIds.BlackjackHouse[index];
        var seated = table?.SeatedCount ?? 0;
        var maxSeats = table is { MaxSeats: > 0 } ? table.MaxSeats : BlackjackRules.SeatCount;
        var mine = string.Equals(seatedTableId, roomId, StringComparison.Ordinal);
        var reaches = mine || table is null || HouseBlackjack.Reaches(playerMaxBet, table.MinBet);
        var seats = mine || (seatedTableId.Length == 0 && reaches && seated < maxSeats);
        var action = mine ? L.Strip.ResumeAction : seats ? L.LivePage.Sit : L.LivePage.Watch;
        var stakes = table is { MinBet: > 0, MaxBet: > 0 }
            ? Loc.T(L.Casino.TableStakes, NumberText.Compact(table.MinBet), NumberText.Compact(table.MaxBet))
            : string.Empty;
        var note = reaches
            ? Loc.T(L.Casino.TableSeats, seated.ToString(Loc.Culture), maxSeats.ToString(Loc.Culture))
            : Loc.T(L.LivePage.BlackjackAboveMaxBet, NumberText.Compact(playerMaxBet));
        return new HouseRoomRow(roomId, Loc.T(Names[index]), stakes, note, seated, maxSeats, Loc.T(action), seats,
            index == Names.Length - 1, !reaches);
    }
}
