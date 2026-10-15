using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoTableLedgerTests
{
    [Fact]
    public void AnEntryReadsRecordedOnceSettledAndOwedUntilThen()
    {
        var owed = new CasinoLedgerEntryDto(EntryId: "e1", Kind: CasinoLedgerKinds.CashOut, Amount: 500_000,
            PayerUserId: "h", PayerName: "Rhan", PayeeUserId: "p", PayeeName: "Mira",
            ProposedBy: CasinoGilLedger.ServerProposer);

        Assert.Equal(Loc.T(L.Tables.EntryOwed), TableLedger.StateText(owed));
        Assert.Equal(Loc.T(L.Tables.EntryRecorded), TableLedger.StateText(owed with { Settled = true }));
        Assert.Equal(Loc.T(L.Tables.EntryRecorded),
            TableLedger.StateText(owed with { Settled = true, Guest = true, GuestName = "Mira" }));
    }

    [Fact]
    public void TheOwedNoteCountsTheLinesStillOwed()
    {
        var entries = new[]
        {
            new CasinoLedgerEntryDto("e1", Settled: true),
            new CasinoLedgerEntryDto("e2"),
            new CasinoLedgerEntryDto("e3"),
        };

        Assert.Equal(Loc.T(L.Tables.OwedNote, 2.ToString(Loc.Culture)), TableLedger.OwedNoteFor(entries));
        Assert.Equal(string.Empty, TableLedger.OwedNoteFor(new[] { entries[0] }));
        Assert.Equal(string.Empty, TableLedger.OwedNoteFor(null));
    }

    [Fact]
    public void EachHostActionHasItsOwnLabel()
    {
        Assert.Equal(Loc.T(L.Tables.MarkPaid), LedgerEntryRows.OptionLabel(LedgerEntryOption.MarkPaid));
        Assert.Equal(Loc.T(L.Tables.MarkUnpaid), LedgerEntryRows.OptionLabel(LedgerEntryOption.MarkUnpaid));
        Assert.Equal(Loc.T(L.Tables.RemoveEntry), LedgerEntryRows.OptionLabel(LedgerEntryOption.Remove));
        Assert.Equal(string.Empty, LedgerEntryRows.OptionLabel(LedgerEntryOption.None));
    }

    [Fact]
    public void GilAmountsReadAsGilWithTheFullNumber()
    {
        Assert.Equal(Loc.T(L.Tables.GilAmount, NumberText.Group(20_000_000)),
            TableLedger.AmountText(20_000_000, CasinoCurrencies.Gil));
        Assert.Equal(NumberText.Group(5_000), TableLedger.AmountText(5_000, CasinoCurrencies.Practice));
    }

    [Fact]
    public void EveryLedgerKindHasItsOwnLabel()
    {
        var kinds = new[]
        {
            CasinoLedgerKinds.BuyIn, CasinoLedgerKinds.Rebuy, CasinoLedgerKinds.Payout, CasinoLedgerKinds.CashOut,
            CasinoLedgerKinds.Stake, CasinoLedgerKinds.Ticket, CasinoLedgerKinds.Prize,
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < kinds.Length; index++)
        {
            Assert.True(seen.Add(CasinoGilLedger.KindLabel(kinds[index]).Key), kinds[index]);
        }

        Assert.Equal(L.Tables.KindOther.Key, CasinoGilLedger.KindLabel("future").Key);
    }

    [Fact]
    public void AGuestEntryIsCopiedWithItsNameMarkedAsAGuest()
    {
        var buyIn = new CasinoLedgerEntryDto("g1", "t1", CasinoLedgerKinds.BuyIn, 50_000, string.Empty, "Chea Thorne",
            "h", "Ciri", "host", CasinoLedgerKinds.Trade, true, Guest: true, GuestName: "Chea Thorne");
        var payout = buyIn with
        {
            Kind = CasinoLedgerKinds.Payout, PayerUserId = "h", PayerName = "Ciri", PayeeUserId = string.Empty,
            PayeeName = "Chea Thorne",
        };
        var registered = new CasinoLedgerEntryDto("e1", "t1", CasinoLedgerKinds.BuyIn, 50_000, "p", "Mira", "h",
            "Ciri", Settled: true);

        Assert.Equal(Loc.T(L.Tables.CopyEntry, Loc.T(L.Tables.KindBuyIn), "Chea Thorne (guest)", "Ciri",
                TableLedger.AmountText(50_000, CasinoCurrencies.Gil), Loc.T(L.Tables.EntryRecorded)),
            TableLedger.EntryLine(buyIn, CasinoCurrencies.Gil));
        Assert.Contains("Ciri to Chea Thorne (guest)", TableLedger.EntryLine(payout, CasinoCurrencies.Gil),
            StringComparison.Ordinal);
        Assert.DoesNotContain("(guest)", TableLedger.EntryLine(registered, CasinoCurrencies.Gil),
            StringComparison.Ordinal);
    }

    [Fact]
    public void NightTotalsCountSettledEntriesOnBothSidesIncludingGuests()
    {
        var entries = new[]
        {
            new CasinoLedgerEntryDto("g1", "t1", CasinoLedgerKinds.BuyIn, 50_000, string.Empty, "Chea Thorne", "h",
                "Ciri", Settled: true, Guest: true, GuestName: "Chea Thorne"),
            new CasinoLedgerEntryDto("g2", "t1", CasinoLedgerKinds.Payout, 20_000, "h", "Ciri", string.Empty,
                "Chea Thorne", Settled: true, Guest: true, GuestName: "Chea Thorne"),
            new CasinoLedgerEntryDto("e1", "t1", CasinoLedgerKinds.BuyIn, 100_000, "p", "Mira", "h", "Ciri",
                Settled: true),
            new CasinoLedgerEntryDto("e2", "t1", CasinoLedgerKinds.Payout, 70_000, "h", "Ciri", "p", "Mira"),
        };

        var host = GilLedgerTotals.Of(entries, "h");
        Assert.Equal(150_000, host.In);
        Assert.Equal(20_000, host.Out);
        Assert.Equal(3, host.Settled);

        var player = GilLedgerTotals.Of(entries, "p");
        Assert.Equal(0, player.In);
        Assert.Equal(100_000, player.Out);
        Assert.Equal(GilLedgerTotals.Empty, GilLedgerTotals.Of(entries, string.Empty));
        Assert.Equal(GilLedgerTotals.Empty, GilLedgerTotals.Of(null, "h"));
        Assert.Equal(Loc.T(L.Book.NightTotals, NumberText.Group(150_000), NumberText.Group(20_000)),
            TableLedger.TotalsText(host));
    }

    [Fact]
    public void APatronTheTableHasNotAdmittedSeesTheDoorInsteadOfTheLedger()
    {
        var card = new CasinoTableRowDto(TableId: "t1", GameKind: CasinoWire.BookKind, Admitted: false,
            Reason: CasinoReasons.InviteOnly);

        Assert.True(TableLedger.BarsReader(card, false));
        Assert.False(TableLedger.BarsReader(card, true));
        Assert.False(TableLedger.BarsReader(card with { Admitted = true, Reason = string.Empty }, false));
        Assert.Equal(TableDoorAction.Knock, TableDoorCard.ContentFor(card.Reason, CasinoKnockStatus.Idle,
            L.Casino.BlackjackClosedTitle, L.Casino.BlackjackClosedHint).Pressed);
    }

    [Fact]
    public void TheHostGetsAGuestChoiceOnlyWhileTheServerListsTheBook()
    {
        Assert.True(TableLedger.ShowsRecord(2, false));
        Assert.True(TableLedger.ShowsRecord(0, true));
        Assert.False(TableLedger.ShowsRecord(0, false));

        Assert.Equal(3, TableLedger.ChoiceCount(2, true));
        Assert.Equal(2, TableLedger.ChoiceCount(2, false));
        Assert.Equal(1, TableLedger.ChoiceCount(0, true));
        Assert.True(TableLedger.IsGuestChoice(2, 2, true));
        Assert.True(TableLedger.IsGuestChoice(0, 0, true));
        Assert.False(TableLedger.IsGuestChoice(1, 2, true));
        Assert.False(TableLedger.IsGuestChoice(2, 2, false));
    }

    [Fact]
    public void TheRecordRailListsEveryOtherPlayerOnceAndSkipsGuests()
    {
        var rows = new[]
        {
            new CasinoTableLedgerRowDto("p1", "Mira", 0, 500_000, 650_000, 150_000, 12, true),
            new CasinoTableLedgerRowDto("p2", "Rhan", 1, 0, 0, 0, 0, false),
        };
        var entries = new[]
        {
            new CasinoLedgerEntryDto("e1", "t1", CasinoLedgerKinds.Payout, 10, "h", "Ciri", "p1", "Mira"),
            new CasinoLedgerEntryDto("e2", "t1", CasinoLedgerKinds.BuyIn, 10, "p3", "Tess", "h", "Ciri"),
            new CasinoLedgerEntryDto("g1", "t1", CasinoLedgerKinds.BuyIn, 10, string.Empty, "Chea Thorne", "h",
                "Ciri", Guest: true, GuestName: "Chea Thorne"),
        };

        var players = TableLedger.Players(rows, entries, "h");

        Assert.Equal(new[] { new LedgerPlayer("p1", "Mira"), new LedgerPlayer("p3", "Tess") }, players);
    }

    [Fact]
    public void APatchReplacesAnEntryInPlaceOrAddsItAtTheServerOrder()
    {
        var first = new CasinoLedgerEntryDto("e1", Amount: 10);
        var second = new CasinoLedgerEntryDto("e2", Amount: 20);
        var entries = new[] { first, second };

        var replaced = LedgerEntryPatch.Upsert(entries, second with { Settled = true }, false);
        var appended = LedgerEntryPatch.Upsert(entries, new CasinoLedgerEntryDto("e3"), false);
        var prepended = LedgerEntryPatch.Upsert(entries, new CasinoLedgerEntryDto("e3"), true);

        Assert.NotSame(entries, replaced);
        Assert.False(entries[1].Settled);
        Assert.True(replaced[1].Settled);
        Assert.Equal(2, replaced.Length);
        Assert.Equal(new[] { "e1", "e2", "e3" }, Ids(appended));
        Assert.Equal(new[] { "e3", "e1", "e2" }, Ids(prepended));
    }

    [Fact]
    public void ARemovedEntryLeavesTheRestInOrder()
    {
        var entries = new[]
        {
            new CasinoLedgerEntryDto("e1"), new CasinoLedgerEntryDto("e2"), new CasinoLedgerEntryDto("e3"),
        };

        Assert.Equal(new[] { "e1", "e3" }, Ids(LedgerEntryPatch.Remove(entries, "e2")));
        Assert.Equal(new[] { "e2", "e3" }, Ids(LedgerEntryPatch.Remove(entries, "e1")));
        Assert.Same(entries, LedgerEntryPatch.Remove(entries, "missing"));
        Assert.Equal(2, LedgerEntryPatch.IndexOf(entries, "e3"));
        Assert.Equal(-1, LedgerEntryPatch.IndexOf(entries, "missing"));
    }

    [Fact]
    public void CoDealersToggleInAndOut()
    {
        var config = new CasinoTableConfigDto(CoDealers: new[] { "u2" });

        Assert.True(TableDoor.IsCoDealer(config, "u2"));
        Assert.False(TableDoor.IsCoDealer(config, "u3"));
        Assert.Equal(new[] { "u2", "u3" }, TableDoor.Toggled(config.CoDealers, "u3"));
        Assert.Empty(TableDoor.Toggled(config.CoDealers, "u2"));
        Assert.Equal(new[] { "u4" }, TableDoor.Toggled(null, "u4"));
    }

    private static string[] Ids(CasinoLedgerEntryDto[] entries)
    {
        var ids = new string[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            ids[index] = entries[index].EntryId;
        }

        return ids;
    }
}
