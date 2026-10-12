using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TradeMatchFlowTests
{
    private const string Host = "u-host";
    private const string Player = "u-xeldar";
    private const string Table = "table-a1";

    private static readonly string[] CharacterWindow = { "Trade", "Xeldar Al", "Confirm" };

    private static TradeTableContext HostContext(params CasinoLedgerEntryDto[] entries) =>
        new(Table, Host, Host, "Ciri", new[] { new TradeSeat(Player, "Xeldar") }, entries);

    private static TradeTableContext PlayerContext(params CasinoLedgerEntryDto[] entries) =>
        new(Table, Player, Host, "Ciri", new[] { new TradeSeat(Player, "Xeldar") }, entries);

    private static CasinoLedgerTradeMatchDto Matched(string userId, string name, bool hostSide) =>
        new(true, string.Empty, true, userId, name, hostSide);

    [Fact]
    public void ADisplayNameInTheWindowIsMatchedLocally()
    {
        var result = TradeMatchFlow.Local(new TradeCompletion(new[] { "Xeldar" }, 50_000, 0), HostContext());
        Assert.Equal(TradeMatchRoute.Local, result.Route);
        Assert.Equal(Player, result.Proposal.CounterpartyUserId);
        Assert.Equal(TradeMatchRoute.NoMatch,
            TradeMatchFlow.Local(new TradeCompletion(CharacterWindow, 50_000, 0), HostContext()).Route);
        Assert.Equal(TradeMatchRoute.NoTable,
            TradeMatchFlow.Local(new TradeCompletion(CharacterWindow, 50_000, 0), default).Route);
    }

    [Fact]
    public async Task ACharacterNameTheTableDoesNotShowIsMatchedOnTheServer()
    {
        var source = new FakeMatchSource(Matched(Player, "Xeldar", true));
        var result = await TradeMatchFlow.ServerAsync(source, new TradeCompletion(CharacterWindow, 50_000, 0),
            HostContext(), CancellationToken.None);
        Assert.Equal(TradeMatchRoute.Server, result.Route);
        Assert.Equal(TradeProposalKind.Propose, result.Proposal.Kind);
        Assert.Equal(CasinoLedgerKinds.BuyIn, result.Proposal.LedgerKind);
        Assert.Equal(Player, result.Proposal.CounterpartyUserId);
        Assert.Equal("Xeldar", result.Proposal.CounterpartyName);
        Assert.Equal(50_000, result.Proposal.Amount);
        Assert.True(result.Proposal.HostSide);
        Assert.Equal(Table, source.RoomId);
        Assert.Equal(CharacterWindow, source.Request!.Names);
        Assert.Equal(50_000, source.Request.Received);
        Assert.Equal(0, source.Request.Sent);
    }

    [Fact]
    public async Task AServerMatchConfirmsTheEntryAlreadyWaiting()
    {
        var payout = new CasinoLedgerEntryDto("e9", Table, CasinoLedgerKinds.Payout, 50_000, Host, "Ciri", Player,
            "Xeldar", PayerConfirmed: true);
        var source = new FakeMatchSource(Matched(Host, "Ciri", false));
        var result = await TradeMatchFlow.ServerAsync(source, new TradeCompletion(new[] { "C'ilahri Cottol" }, 50_000, 0),
            PlayerContext(payout), CancellationToken.None);
        Assert.Equal(TradeMatchRoute.Server, result.Route);
        Assert.Equal(TradeProposalKind.Confirm, result.Proposal.Kind);
        Assert.Equal("e9", result.Proposal.EntryId);
        Assert.False(result.Proposal.HostSide);
    }

    [Fact]
    public async Task AServerWithoutTheRouteIsNoMatch()
    {
        var source = new FakeMatchSource(null, 404);
        var result = await TradeMatchFlow.ServerAsync(source, new TradeCompletion(CharacterWindow, 50_000, 0),
            HostContext(), CancellationToken.None);
        Assert.Equal(TradeMatchRoute.Unreachable, result.Route);
        Assert.Equal("404", result.Reason);
        Assert.Equal(TradeProposalKind.None, result.Proposal.Kind);
    }

    [Fact]
    public async Task RefusalsAndAnswersThatDoNotFitTheTableProposeNothing()
    {
        var completion = new TradeCompletion(CharacterWindow, 50_000, 0);
        var refused = await TradeMatchFlow.ServerAsync(
            new FakeMatchSource(new CasinoLedgerTradeMatchDto(false, "not_party")), completion, HostContext(),
            CancellationToken.None);
        Assert.Equal(TradeMatchRoute.Refused, refused.Route);
        Assert.Equal("not_party", refused.Reason);

        var nobody = await TradeMatchFlow.ServerAsync(new FakeMatchSource(new CasinoLedgerTradeMatchDto(true)),
            completion, HostContext(), CancellationToken.None);
        Assert.Equal(TradeMatchRoute.NoMatch, nobody.Route);

        var coDealer = new TradeTableContext(Table, "u-dealer", Host, "Ciri", Array.Empty<TradeSeat>(),
            Array.Empty<CasinoLedgerEntryDto>());
        var dealt = await TradeMatchFlow.ServerAsync(new FakeMatchSource(Matched(Player, "Xeldar", true)), completion,
            coDealer, CancellationToken.None);
        Assert.Equal(TradeMatchRoute.Mismatch, dealt.Route);

        var silent = new FakeMatchSource(Matched(Player, "Xeldar", true));
        var unread = await TradeMatchFlow.ServerAsync(silent, new TradeCompletion(new[] { " " }, 50_000, 0),
            HostContext(), CancellationToken.None);
        Assert.Equal(TradeMatchRoute.NoNames, unread.Route);
        Assert.Equal(0, silent.Calls);
    }

    [Fact]
    public async Task APlayerToPlayerMatchOnlyConfirmsAnEntryThatExists()
    {
        var completion = new TradeCompletion(new[] { "Mira Rhan" }, 10_000, 0);
        var source = new FakeMatchSource(Matched("u-mira", "Mira", false));
        var nothing = await TradeMatchFlow.ServerAsync(source, completion, PlayerContext(), CancellationToken.None);
        Assert.Equal(TradeMatchRoute.Unrecordable, nothing.Route);

        var stake = new CasinoLedgerEntryDto("s1", Table, CasinoLedgerKinds.Stake, 10_000, "u-mira", "Mira", Player,
            "Xeldar");
        var owed = await TradeMatchFlow.ServerAsync(source, completion, PlayerContext(stake), CancellationToken.None);
        Assert.Equal(TradeProposalKind.Confirm, owed.Proposal.Kind);
        Assert.Equal("s1", owed.Proposal.EntryId);
    }

    [Fact]
    public void OnlyNonBlankNamesReachTheServerWithinTheCaps()
    {
        var many = new string[TradeNames.MaxServerNames + 4];
        Array.Fill(many, "Xeldar Al");
        many[0] = string.Empty;
        many[1] = new string('a', TradeNames.MaxServerNameLength + 30);
        var names = TradeNames.ForServer(many);
        Assert.Equal(TradeNames.MaxServerNames, names.Length);
        Assert.Equal(TradeNames.MaxServerNameLength, names[0].Length);
        Assert.Empty(TradeNames.ForServer(new[] { "", "  " }));
        Assert.Equal("c'ilahri cottol", TradeNames.Normalize("C\u2019ilahri Cottol"));
    }

    [Fact]
    public void ANewPromptWaitsBehindAnUnansweredOne()
    {
        var queue = new TradeProposalQueue();
        var first = new TradeProposal(TradeProposalKind.Propose, Table, CasinoLedgerKinds.BuyIn, Player, "Xeldar",
            50_000, string.Empty, true, true);
        var second = first with { Amount = 70_000 };
        Assert.True(queue.Add(first));
        Assert.True(queue.Add(second));
        Assert.Equal(2, queue.Count);
        Assert.Equal(50_000, queue.Head.Amount);
        Assert.True(queue.TryTake(out var taken));
        Assert.Equal(50_000, taken.Amount);
        Assert.Equal(70_000, queue.Head.Amount);
        Assert.True(queue.TryTake(out _));
        Assert.False(queue.TryTake(out _));
        Assert.Equal(TradeProposalKind.None, queue.Head.Kind);

        for (var index = 0; index < TradeProposalQueue.Capacity; index++)
        {
            Assert.True(queue.Add(first with { Amount = index + 1 }));
        }

        Assert.False(queue.Add(second));
        Assert.Equal(TradeProposalQueue.Capacity, queue.Count);
        Assert.Equal(2, queue.Head.Amount);
    }

    [Fact]
    public void EachMissRouteKeepsItsKind()
    {
        Assert.Equal(TradeMissKind.NotAtTable, TradeMatchFlow.MissKindOf(TradeMatchRoute.NoMatch));
        Assert.Equal(TradeMissKind.Unread, TradeMatchFlow.MissKindOf(TradeMatchRoute.NoNames));
        Assert.Equal(TradeMissKind.Registered, TradeMatchFlow.MissKindOf(TradeMatchRoute.Mismatch));
        Assert.Equal(TradeMissKind.Registered, TradeMatchFlow.MissKindOf(TradeMatchRoute.Unrecordable));
        Assert.Equal(TradeMissKind.Unchecked, TradeMatchFlow.MissKindOf(TradeMatchRoute.Unreachable));
        Assert.Equal(TradeMissKind.Unchecked, TradeMatchFlow.MissKindOf(TradeMatchRoute.Refused));
        Assert.Equal(TradeMissKind.NotAtTable, new TradeMiss(Table, 50_000, 1_000).Kind);
    }

    [Fact]
    public void AnEntryThePatronRecordedAfterAMissBecomesAConfirmPrompt()
    {
        var received = new TradeMiss(Table, 50_000, 1_000, "Chea Thorne", true);
        var waiting = new CasinoLedgerEntryDto("e1", Table, CasinoLedgerKinds.BuyIn, 50_000, Player, "Xeldar Al", Host,
            "Ciri", Player, CasinoLedgerKinds.Trade, true, false);
        var proposal = TradeMatchFlow.WaitingOnHost(received, Host, new[] { waiting });
        Assert.Equal(TradeProposalKind.Confirm, proposal.Kind);
        Assert.Equal("e1", proposal.EntryId);
        Assert.Equal(Player, proposal.CounterpartyUserId);
        Assert.Equal("Xeldar Al", proposal.CounterpartyName);
        Assert.True(proposal.HostSide);
        Assert.True(proposal.Incoming);

        var sent = received with { Incoming = false };
        var payout = waiting with { Kind = CasinoLedgerKinds.Payout, PayerUserId = Host, PayerName = "Ciri",
            PayeeUserId = Player, PayeeName = "Xeldar Al", PayerConfirmed = false, PayeeConfirmed = true };
        Assert.Equal("e1", TradeMatchFlow.WaitingOnHost(sent, Host, new[] { payout }).EntryId);

        Assert.Equal(TradeProposalKind.None, TradeMatchFlow.WaitingOnHost(received, Host,
            new[] { waiting with { Amount = 40_000 } }).Kind);
        Assert.Equal(TradeProposalKind.None, TradeMatchFlow.WaitingOnHost(received, Host,
            new[] { waiting with { PayeeConfirmed = true } }).Kind);
        Assert.Equal(TradeProposalKind.None, TradeMatchFlow.WaitingOnHost(received, Host,
            new[] { waiting with { PayerConfirmed = false } }).Kind);
        Assert.Equal(TradeProposalKind.None, TradeMatchFlow.WaitingOnHost(received, Host,
            new[] { waiting with { Settled = true } }).Kind);
        Assert.Equal(TradeProposalKind.None, TradeMatchFlow.WaitingOnHost(sent, Host, new[] { waiting }).Kind);
    }

    [Fact]
    public void TheAmberLineNamesTheDirectionOnlyWhenTheServerFoundNobody()
    {
        var received = new TradeMiss(Table, 50_000, 1_000, Incoming: true);
        Assert.Equal(L.Venue.TradeMissReceived.Key, TableLedger.MissLine(received).Key);
        Assert.Equal(L.Venue.TradeMissSent.Key, TableLedger.MissLine(received with { Incoming = false }).Key);
        Assert.Equal(L.Venue.TradeMissUnchecked.Key,
            TableLedger.MissLine(received with { Kind = TradeMissKind.Unchecked }).Key);
        Assert.Equal(L.Venue.TradeMissUnchecked.Key,
            TableLedger.MissLine(received with { Kind = TradeMissKind.Unread }).Key);

        Assert.True(TableLedger.OffersGuest(received, true, true));
        Assert.True(TableLedger.OffersGuest(received with { Kind = TradeMissKind.Unread }, true, true));
        Assert.False(TableLedger.OffersGuest(received, false, true));
        Assert.False(TableLedger.OffersGuest(received, true, false));
        Assert.False(TableLedger.OffersGuest(received with { Kind = TradeMissKind.Registered }, true, true));
        Assert.False(TableLedger.OffersGuest(received with { Kind = TradeMissKind.Unchecked }, true, true));
    }

    [Fact]
    public void AnUnmatchedTradeShowsInItsOwnTablesLedgerForAWhile()
    {
        var miss = new TradeMiss(Table, 50_000, 1_000);
        Assert.True(miss.ShownAt(Table, 1_000 + TradeMiss.ShownMilliseconds));
        Assert.False(miss.ShownAt(Table, 1_001 + TradeMiss.ShownMilliseconds));
        Assert.False(miss.ShownAt("table-b2", 1_000));
    }

    [Fact]
    public void TheMatchRouteAndBodyMatchTheWire()
    {
        Assert.Equal("/casino/tables/table-a1/ledger/match",
            CasinoClient.TablePath("table-a1", CasinoClient.LedgerMatchLeaf));
        var json = JsonSerializer.Serialize(new CasinoLedgerTradeMatchRequest(new[] { "Xeldar Al" }, 50_000, 0),
            AethernetJsonContext.Default.CasinoLedgerTradeMatchRequest);
        Assert.Contains("\"names\":[\"Xeldar Al\"]", json, StringComparison.Ordinal);
        Assert.Contains("\"received\":50000", json, StringComparison.Ordinal);
        var answer = JsonSerializer.Deserialize(
            "{\"granted\":true,\"reason\":\"\",\"matched\":true,\"counterpartyUserId\":\"u1\",\"counterpartyName\":\"Ciri\",\"hostSide\":false}",
            AethernetJsonContext.Default.CasinoLedgerTradeMatchDto);
        Assert.True(answer!.Matched);
        Assert.Equal("u1", answer.CounterpartyUserId);
        Assert.Equal("Ciri", answer.CounterpartyName);
    }

    private sealed class FakeMatchSource : ITradeMatchSource
    {
        private readonly CasinoLedgerTradeMatchDto? answer;
        private readonly int status;

        public FakeMatchSource(CasinoLedgerTradeMatchDto? answer, int status = 0)
        {
            this.answer = answer;
            this.status = status;
        }

        public int Calls { get; private set; }

        public string RoomId { get; private set; } = string.Empty;

        public CasinoLedgerTradeMatchRequest? Request { get; private set; }

        public Task<CasinoLedgerTradeMatchDto?> MatchTradeAsync(string roomId, CasinoLedgerTradeMatchRequest request,
            CancellationToken token, Action<AepFailure>? onFailure = null)
        {
            Calls++;
            RoomId = roomId;
            Request = request;
            if (answer is null && status > 0)
            {
                onFailure?.Invoke(AepFailure.FromStatus(status, null));
            }

            return Task.FromResult(answer);
        }
    }
}
