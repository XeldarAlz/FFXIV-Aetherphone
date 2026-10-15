using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BookLedgerAdmissionTests
{
    [Fact]
    public void OnlyAnAdmittedBookPatronNeedsToRegisterBeforeOpeningTheLedger()
    {
        var book = new CasinoTableRowDto(TableId: "book", GameKind: CasinoWire.BookKind, Admitted: true);
        Assert.True(BookLedgerAdmission.Required(book, false));
        Assert.False(BookLedgerAdmission.Required(book, true));
        Assert.False(BookLedgerAdmission.Required(book with { Admitted = false }, false));
        Assert.False(BookLedgerAdmission.Required(book with { GameKind = CasinoWire.BlackjackKind }, false));
        Assert.False(BookLedgerAdmission.Required(book with { GameKind = VenueKinds.Raffle }, false));
        Assert.False(BookLedgerAdmission.Required(null, false));
    }

    [Fact]
    public void DrawingWhileJoiningStartsOneRequestAndOnlyASuccessOpensTheLedger()
    {
        var admission = new BookLedgerAdmission();
        var pending = admission.Begin("book", "patron", false)!;
        Assert.Null(admission.For("book", "patron"));
        Assert.Null(admission.Begin("book", "patron", false));
        Assert.Null(admission.Begin("book", "patron", true));
        Assert.True(admission.Complete(pending, new CasinoTableActionDto(true, string.Empty)));
        Assert.True(admission.For("book", "patron")!.Granted);
        Assert.Null(admission.Begin("book", "patron", false));
        Assert.False(admission.Complete(pending, null));
        Assert.True(admission.For("book", "patron")!.Granted);
    }

    [Fact]
    public void ARefusedReaderRemainsOutsideUntilAnExplicitRetrySucceeds()
    {
        var admission = new BookLedgerAdmission();
        var pending = admission.Begin("book", "patron", false)!;
        Assert.True(admission.Complete(pending, new CasinoTableActionDto(false, CasinoReasons.InviteOnly)));
        Assert.False(admission.For("book", "patron")!.Granted);
        Assert.Equal(CasinoReasons.InviteOnly, admission.For("book", "patron")!.Reason);
        Assert.Null(admission.Begin("book", "patron", false));

        var retry = admission.Begin("book", "patron", true)!;
        Assert.Null(admission.For("book", "patron"));
        Assert.False(admission.Complete(pending, new CasinoTableActionDto(true, string.Empty)));
        Assert.True(admission.Complete(retry, new CasinoTableActionDto(true, string.Empty)));
        Assert.True(admission.For("book", "patron")!.Granted);
    }

    [Fact]
    public void AFailedRequestOffersRetryInsteadOfLeavingTheLedgerLoading()
    {
        var admission = new BookLedgerAdmission();
        var pending = admission.Begin("book", "patron", false)!;
        Assert.True(admission.Complete(pending, null));
        Assert.False(admission.For("book", "patron")!.Granted);
        Assert.Equal(CasinoReasons.Unreachable, admission.For("book", "patron")!.Reason);
        Assert.NotNull(admission.Begin("book", "patron", true));
    }

    [Fact]
    public void LeavingOrSwitchingTheTableOrAccountDiscardsAnOldJoinAnswer()
    {
        var admission = new BookLedgerAdmission();
        var abandoned = admission.Begin("book", "patron", false)!;
        admission.Reset();
        Assert.False(admission.Complete(abandoned, new CasinoTableActionDto(true, string.Empty)));
        Assert.Null(admission.For("book", "patron"));

        var previousTable = admission.Begin("book", "patron", false)!;
        var previousAccount = admission.Begin("other-book", "patron", false)!;
        var current = admission.Begin("other-book", "other-patron", false)!;
        Assert.False(admission.Complete(previousTable, new CasinoTableActionDto(true, string.Empty)));
        Assert.False(admission.Complete(previousAccount, new CasinoTableActionDto(true, string.Empty)));
        Assert.True(admission.Complete(current, new CasinoTableActionDto(true, string.Empty)));
        Assert.Null(admission.For("book", "other-patron"));
        Assert.Null(admission.For("other-book", "patron"));
        Assert.True(admission.For("other-book", "other-patron")!.Granted);
    }

    [Fact]
    public void TheJoinRouteMatchesTheBookAdmissionEndpoint()
    {
        Assert.Equal("/casino/tables/book/ledger/join", CasinoClient.TablePath("book", CasinoClient.LedgerJoinLeaf));
        var admission = new BookLedgerAdmission();
        Assert.Null(admission.Begin(string.Empty, "patron", false));
        Assert.Null(admission.Begin("book", string.Empty, false));
    }
}
