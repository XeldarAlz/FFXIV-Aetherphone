using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GuestLedgerTests
{
    private const string Table = "table-a1";
    private const string Host = "u-host";

    private static readonly string[] PartnerWindow = { "Chea Thorne", "Trade", "Confirm" };

    private static TradeMiss Miss(long amount = 50_000, bool incoming = true, string name = "Chea Thorne",
        TradeMissKind kind = TradeMissKind.NotAtTable) =>
        new(Table, amount, 1_000, name, incoming, kind);

    private static CasinoLedgerEntryDto GuestBuyIn(string name, string kind = CasinoLedgerKinds.BuyIn) =>
        new("g1", Table, kind, 20_000, string.Empty, name, Host, "Ciri", "host", CasinoLedgerKinds.Trade, true, true,
            true, Guest: true, GuestName: name);

    [Fact]
    public void ThePartnerNameComesOnlyFromTheTradeArraysAndIsNormalised()
    {
        Assert.Equal("Chea Thorne", GuestLedger.PartnerNameOf(new TradeCompletion(new[] { " Chea  Thorne " }, 50_000,
            0, true)));
        Assert.Equal(string.Empty, GuestLedger.PartnerNameOf(new TradeCompletion(PartnerWindow, 50_000, 0)));
        Assert.Equal(string.Empty, GuestLedger.PartnerNameOf(new TradeCompletion(Array.Empty<string>(), 50_000, 0,
            true)));
        Assert.Equal(string.Empty, GuestLedger.PartnerNameOf(new TradeCompletion(
            new[] { new string('a', CasinoHostingRules.GuestNameMaxLength + 1) }, 50_000, 0, true)));
    }

    [Fact]
    public void OnlyTheHostIsOfferedAGuestEntryAndOnlyWhileTheServerHasTheFeature()
    {
        var entries = Array.Empty<CasinoLedgerEntryDto>();
        var offer = GuestLedger.Offer(Miss(), true, entries);
        Assert.NotNull(offer);
        Assert.Equal(Table, offer!.TableId);
        Assert.Equal("Chea Thorne", offer.GuestName);
        Assert.Equal(50_000, offer.Amount);

        Assert.Null(GuestLedger.Offer(Miss(), false, entries));
        Assert.Null(GuestLedger.Offer(null, true, entries));
        Assert.Null(GuestLedger.Offer(Miss(name: string.Empty), true, entries));
        Assert.Null(GuestLedger.Offer(Miss(amount: 0), true, entries));
        Assert.Null(GuestLedger.Shown(offer, false));
        Assert.Same(offer, GuestLedger.Shown(offer, true));
    }

    [Fact]
    public void AGuestIsOfferedOnlyWhenNobodyRegisteredWasFound()
    {
        var entries = Array.Empty<CasinoLedgerEntryDto>();
        Assert.False(GuestLedger.Offer(Miss(), true, entries)!.Unchecked);
        Assert.True(GuestLedger.Offer(Miss(kind: TradeMissKind.Unread), true, entries)!.Unchecked);
        Assert.Null(GuestLedger.Offer(Miss(kind: TradeMissKind.Registered), true, entries));
        Assert.Null(GuestLedger.Offer(Miss(kind: TradeMissKind.Unchecked), true, entries));
    }

    [Fact]
    public void TheGuestButtonNamesTheDirection()
    {
        Assert.Equal(L.Book.RecordGuestBuyIn.Key, GuestLedger.RecordLabel(CasinoLedgerKinds.BuyIn).Key);
        Assert.Equal(L.Book.RecordGuestRebuy.Key, GuestLedger.RecordLabel(CasinoLedgerKinds.Rebuy).Key);
        Assert.Equal(L.Book.RecordGuestPayout.Key, GuestLedger.RecordLabel(CasinoLedgerKinds.Payout).Key);
        Assert.Equal(CasinoLedgerKinds.Rebuy, GuestLedger.KindOf(Miss(), new[] { GuestBuyIn("Chea Thorne") }));
        Assert.Equal(CasinoLedgerKinds.Payout,
            GuestLedger.KindOf(Miss(incoming: false), Array.Empty<CasinoLedgerEntryDto>()));
    }

    [Fact]
    public void ReceivedGilIsABuyInAndSentGilIsAPayout()
    {
        var entries = Array.Empty<CasinoLedgerEntryDto>();
        var received = GuestLedger.Offer(Miss(incoming: true), true, entries)!;
        Assert.Equal(CasinoLedgerKinds.BuyIn, received.LedgerKind);
        Assert.True(received.Incoming);

        var sent = GuestLedger.Offer(Miss(incoming: false), true, entries)!;
        Assert.Equal(CasinoLedgerKinds.Payout, sent.LedgerKind);
        Assert.False(sent.Incoming);
    }

    [Fact]
    public void ASecondBuyInFromTheSameGuestIsARebuy()
    {
        var entries = new[] { GuestBuyIn("Chea Thorne") };
        Assert.Equal(CasinoLedgerKinds.Rebuy, GuestLedger.BuyInKindFor(entries, "Chea Thorne"));
        Assert.Equal(CasinoLedgerKinds.BuyIn, GuestLedger.BuyInKindFor(entries, "Mira Rhan"));
        Assert.Equal(CasinoLedgerKinds.Rebuy, GuestLedger.Offer(Miss(), true, entries)!.LedgerKind);
        Assert.Equal(CasinoLedgerKinds.Rebuy,
            GuestLedger.BuyInKindFor(new[] { GuestBuyIn("Chea Thorne", CasinoLedgerKinds.Rebuy) }, "Chea Thorne"));

        var registered = GuestBuyIn("Chea Thorne") with { Guest = false, GuestName = string.Empty, PayerUserId = "u1" };
        Assert.Equal(CasinoLedgerKinds.BuyIn, GuestLedger.BuyInKindFor(new[] { registered }, "Chea Thorne"));
        var payout = GuestBuyIn("Chea Thorne", CasinoLedgerKinds.Payout);
        Assert.Equal(CasinoLedgerKinds.BuyIn, GuestLedger.BuyInKindFor(new[] { payout }, "Chea Thorne"));
    }

    [Fact]
    public void ATypedGuestNameIsNormalisedAndBoundedBeforeItIsSent()
    {
        var entries = Array.Empty<CasinoLedgerEntryDto>();
        var buyIn = GuestLedger.Typed(Table, "  Chea   Thorne ", false, 50_000, entries);
        Assert.NotNull(buyIn);
        Assert.Equal(Table, buyIn!.TableId);
        Assert.Equal("Chea Thorne", buyIn.GuestName);
        Assert.Equal(CasinoLedgerKinds.BuyIn, buyIn.LedgerKind);
        Assert.Equal(50_000, buyIn.Amount);
        Assert.True(buyIn.Incoming);

        var payout = GuestLedger.Typed(Table, "Chea Thorne", true, 20_000, entries)!;
        Assert.Equal(CasinoLedgerKinds.Payout, payout.LedgerKind);
        Assert.False(payout.Incoming);
        Assert.Equal(CasinoLedgerKinds.Rebuy,
            GuestLedger.Typed(Table, "Chea Thorne", false, 10_000, new[] { GuestBuyIn("Chea Thorne") })!.LedgerKind);

        var longest = new string('a', CasinoHostingRules.GuestNameMaxLength);
        Assert.Equal(longest, GuestLedger.Typed(Table, longest, false, 1, entries)!.GuestName);
        Assert.Equal(CasinoHostingRules.MaxGil,
            GuestLedger.Typed(Table, "Chea Thorne", false, CasinoHostingRules.MaxGil, entries)!.Amount);
        Assert.Null(GuestLedger.Typed(Table, longest + "a", false, 1, entries));
        Assert.Null(GuestLedger.Typed(Table, " \t\u0007 ", false, 50_000, entries));
        Assert.Null(GuestLedger.Typed(Table, "Chea Thorne", false, 0, entries));
        Assert.Null(GuestLedger.Typed(Table, "Chea Thorne", false, CasinoHostingRules.MaxGil + 1, entries));
        Assert.Null(GuestLedger.Typed(string.Empty, "Chea Thorne", false, 50_000, entries));
    }

    [Fact]
    public void DismissingTheOfferKeepsTheAmberLineForTenMinutes()
    {
        var miss = Miss();
        Assert.True(miss.ShownAt(Table, 1_000 + TradeMiss.ShownMilliseconds));
        Assert.False(miss.ShownAt(Table, 1_001 + TradeMiss.ShownMilliseconds));
        Assert.Equal("Chea Thorne", miss.PartnerName);
        Assert.True(miss.Incoming);
        Assert.Equal(string.Empty, new TradeMiss(Table, 50_000, 1_000).PartnerName);
    }
}
