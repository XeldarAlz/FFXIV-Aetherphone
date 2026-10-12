using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoBookRulesTests
{
    private static CasinoTableConfigDto Book(int currency = CasinoCurrencies.Gil, long bank = 5_000_000,
        long maxPayout = 0, string name = "Mist Lounge", int listing = CasinoListings.Open,
        string[]? coDealers = null) =>
        new(GameKind: CasinoWire.BookKind, Name: name, Seats: 0, Listing: listing, Spectators: false,
            DealerMode: CasinoDealerModes.House, CoDealers: coDealers, AutoDeal: false, Currency: currency,
            Bank: bank, MaxPayout: maxPayout);

    [Fact]
    public void ABookIsItsOwnHostedKindAndNeverAVenueRoom()
    {
        Assert.True(CasinoBookRules.IsBook(CasinoWire.BookKind));
        Assert.False(CasinoBookRules.IsBook(CasinoWire.BlackjackKind));
        Assert.False(CasinoBookRules.IsBook(VenueKinds.Raffle));
        Assert.False(CasinoBookRules.IsBook(null));
        Assert.False(VenueKinds.IsVenue(CasinoWire.BookKind));
    }

    [Fact]
    public void AGilBookWithABankPassesTheCheck()
    {
        Assert.Equal(string.Empty, CasinoBookRules.Check(Book()));
        Assert.Equal(string.Empty, CasinoBookRules.Check(Book(maxPayout: 1_000_000)));
        Assert.Equal(string.Empty, CasinoBookRules.Check(Book(bank: CasinoHostingRules.MaxGil,
            maxPayout: CasinoHostingRules.MaxGil)));
        Assert.Equal(string.Empty, CasinoHostingRules.Check(Book()));
    }

    [Fact]
    public void ChipsAndPracticeAreRefusedAsGilOnly()
    {
        Assert.Equal(CasinoReasons.GilOnly, CasinoBookRules.Check(Book(currency: CasinoCurrencies.Chips)));
        Assert.Equal(CasinoReasons.GilOnly, CasinoBookRules.Check(Book(currency: CasinoCurrencies.Practice)));
    }

    [Fact]
    public void TheBankAndPayoutLadderIsTheGilBlackjackOne()
    {
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoBookRules.Check(Book(bank: 0)));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoBookRules.Check(Book(bank: CasinoHostingRules.MaxGil + 1)));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoBookRules.Check(Book(maxPayout: 5_000_001)));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoBookRules.Check(Book(maxPayout: -1)));
    }

    [Fact]
    public void NameListingAndCoDealersFollowTheHostingRules()
    {
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoBookRules.Check(Book(name: new string('x', CasinoHostingRules.NameMaxLength + 1))));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoBookRules.Check(Book(listing: CasinoListings.Open + 1)));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoBookRules.Check(Book(coDealers: new[] { "u2" })));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoBookRules.Check(Book() with { TurnSeconds = 17 }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoBookRules.Check(Book() with { PracticeStack = CasinoHostingRules.MinPracticeStack - 1 }));
    }

    [Fact]
    public void GuestNamesAreTrimmedCollapsedAndCappedLikeTheServerDoes()
    {
        Assert.Equal("Chea Thorne", CasinoBookRules.NormalizeGuestName("Chea  Thorne "));
        Assert.Equal("Chea Thorne", CasinoBookRules.NormalizeGuestName("\tChea\u0007 \n Thorne"));
        Assert.Equal(string.Empty, CasinoBookRules.NormalizeGuestName("  \u0001 "));
        Assert.True(CasinoBookRules.IsGuestName(new string('a', CasinoHostingRules.GuestNameMaxLength)));
        Assert.False(CasinoBookRules.IsGuestName(new string('a', CasinoHostingRules.GuestNameMaxLength + 1)));
        Assert.False(CasinoBookRules.IsGuestName(string.Empty));
    }

    [Fact]
    public void ATypedNameCountsOnceItHasAVisibleCharacter()
    {
        Assert.True(CasinoBookRules.HasGuestName("C"));
        Assert.True(CasinoBookRules.HasGuestName("   Chea"));
        Assert.False(CasinoBookRules.HasGuestName(string.Empty));
        Assert.False(CasinoBookRules.HasGuestName("   "));
        Assert.False(CasinoBookRules.HasGuestName("\t\u0001\n"));
    }

    [Fact]
    public void TheBookConstantsMirrorTheServer()
    {
        Assert.Equal(64, CasinoHostingRules.GuestNameMaxLength);
        Assert.Equal(8, CasinoHostingRules.BookIdleHours);
        Assert.Equal(60, CasinoHostingRules.BookTouchSeconds);
        Assert.Contains(CasinoFeatures.VenueBook, CasinoFeatureSet.Known);
    }
}
