using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoTableLedgerTests
{
    [Fact]
    public void AnUnconfirmedEntryIsAmberUntilBothSidesTap()
    {
        var entry = new CasinoLedgerEntryDto(EntryId: "e1", Kind: CasinoLedgerKinds.BuyIn, Amount: 500_000,
            PayerUserId: "p", PayerName: "Mira", PayeeUserId: "h", PayeeName: "Rhan", PayerConfirmed: true);

        Assert.Equal(LedgerTone.Waiting, TableLedger.ToneOf(entry));
        Assert.Equal(Loc.T(L.Tables.EntryWaitingOn, "Rhan"), TableLedger.StateText(entry));
        Assert.Equal(Loc.T(L.Tables.EntryWaitingBoth),
            TableLedger.StateText(entry with { PayerConfirmed = false }));
        Assert.Equal(LedgerTone.Settled,
            TableLedger.ToneOf(entry with { PayeeConfirmed = true, Settled = true }));
        Assert.Equal(LedgerTone.Disputed, TableLedger.ToneOf(entry with { Disputed = true }));
        Assert.Equal(LedgerTone.Waiting,
            TableLedger.ToneOf(entry with { Disputed = true, DisputeResolved = true }));
    }

    [Fact]
    public void AGuestEntrySaysTheHostRecordedIt()
    {
        var entry = new CasinoLedgerEntryDto(EntryId: "g1", Kind: CasinoLedgerKinds.BuyIn, Amount: 50_000,
            PayerName: "Chea Thorne", PayeeUserId: "h", PayeeName: "Rhan", PayerConfirmed: true,
            PayeeConfirmed: true, Settled: true, Guest: true, GuestName: "Chea Thorne");

        Assert.Equal(Loc.T(L.Book.GuestRecorded), TableLedger.StateText(entry));
        Assert.Equal(Loc.T(L.Tables.EntrySettled), TableLedger.StateText(entry with { Guest = false }));
    }

    [Fact]
    public void GilAmountsReadAsGilWithTheFullNumber()
    {
        Assert.Equal(Loc.T(L.Tables.GilAmount, NumberText.Group(20_000_000)),
            TableLedger.AmountText(20_000_000, CasinoCurrencies.Gil));
        Assert.Equal(NumberText.Group(5_000), TableLedger.AmountText(5_000, CasinoCurrencies.Practice));
    }

    [Fact]
    public void ASecondGilBuyInIsARebuy()
    {
        var ledger = new CasinoTableLedgerDto(Rows: new[]
        {
            new CasinoTableLedgerRowDto("p1", "Mira", 0, 500_000, 650_000, 150_000, 12, true),
            new CasinoTableLedgerRowDto("p2", "Rhan", 1, 0, 0, 0, 0, true),
        }, Currency: CasinoCurrencies.Gil);

        Assert.Equal(CasinoLedgerKinds.Rebuy, TableLedger.BuyInKindFor(ledger, "p1"));
        Assert.Equal(CasinoLedgerKinds.BuyIn, TableLedger.BuyInKindFor(ledger, "p2"));
        Assert.Equal(CasinoLedgerKinds.BuyIn, TableLedger.BuyInKindFor(ledger, "stranger"));
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
            Assert.True(seen.Add(TableLedger.KindLabel(kinds[index]).Key), kinds[index]);
        }

        Assert.Equal(L.Tables.KindOther.Key, TableLedger.KindLabel("future").Key);
    }

    [Fact]
    public void AGuestEntryIsCopiedWithItsNameMarkedAsAGuest()
    {
        var buyIn = new CasinoLedgerEntryDto("g1", "t1", CasinoLedgerKinds.BuyIn, 50_000, string.Empty, "Chea Thorne",
            "h", "Ciri", "host", CasinoLedgerKinds.Trade, true, true, true, Guest: true, GuestName: "Chea Thorne");
        var payout = buyIn with
        {
            Kind = CasinoLedgerKinds.Payout, PayerUserId = "h", PayerName = "Ciri", PayeeUserId = string.Empty,
            PayeeName = "Chea Thorne",
        };
        var registered = new CasinoLedgerEntryDto("e1", "t1", CasinoLedgerKinds.BuyIn, 50_000, "p", "Mira", "h",
            "Ciri", Settled: true, PayerConfirmed: true, PayeeConfirmed: true);

        Assert.Equal(Loc.T(L.Tables.CopyEntry, Loc.T(L.Tables.KindBuyIn), "Chea Thorne (guest)", "Ciri",
            TableLedger.AmountText(50_000, CasinoCurrencies.Gil), Loc.T(L.Book.GuestRecorded)),
            TableLedger.EntryLine(buyIn, CasinoCurrencies.Gil));
        Assert.Contains("Ciri to Chea Thorne (guest)", TableLedger.EntryLine(payout, CasinoCurrencies.Gil),
            StringComparison.Ordinal);
        Assert.DoesNotContain("(guest)", TableLedger.EntryLine(registered, CasinoCurrencies.Gil),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AGuestEntryTakesNoConfirmationAndNoDispute()
    {
        var guest = new CasinoLedgerEntryDto("g1", "t1", CasinoLedgerKinds.Payout, 50_000, "h", "Ciri", string.Empty,
            "Chea Thorne", Guest: true, GuestName: "Chea Thorne");

        Assert.False(CasinoGilLedger.CanConfirm(guest, "h"));
        Assert.False(CasinoGilLedger.CanDispute(guest, "h"));
        Assert.False(CasinoGilLedger.CanConfirm(guest with { Settled = true }, "h"));
        Assert.True(CasinoGilLedger.CanConfirm(guest with { Guest = false, PayeeUserId = "p" }, "h"));
        Assert.True(CasinoGilLedger.CanDispute(guest with { Guest = false, PayeeUserId = "p" }, "p"));
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
        Assert.True(TableLedger.ShowsRecord(true, 2, false, string.Empty));
        Assert.True(TableLedger.ShowsRecord(true, 0, true, string.Empty));
        Assert.False(TableLedger.ShowsRecord(true, 0, false, string.Empty));
        Assert.True(TableLedger.ShowsRecord(false, 0, false, "h"));
        Assert.False(TableLedger.ShowsRecord(false, 0, true, string.Empty));

        Assert.Equal(3, TableLedger.ChoiceCount(2, true));
        Assert.Equal(2, TableLedger.ChoiceCount(2, false));
        Assert.Equal(1, TableLedger.ChoiceCount(0, true));
        Assert.True(TableLedger.IsGuestChoice(2, 2, true));
        Assert.True(TableLedger.IsGuestChoice(0, 0, true));
        Assert.False(TableLedger.IsGuestChoice(1, 2, true));
        Assert.False(TableLedger.IsGuestChoice(2, 2, false));
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
}
