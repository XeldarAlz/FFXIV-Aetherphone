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
