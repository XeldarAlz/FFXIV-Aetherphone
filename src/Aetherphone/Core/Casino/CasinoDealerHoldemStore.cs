using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoDealerHoldemStore : IDisposable
{
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore store;
    private readonly StoreWork work = new("CasinoDealerHoldem");

    private volatile bool inFlight;
    private volatile bool failed;
    private CasinoDealerHoldemDto? result;
    private CasinoDealerHoldemOpenDto? openResult;
    private DealerHoldemDeal? pending;

    public CasinoDealerHoldemStore(AethernetSession session, CasinoClient casino, CasinoStore store)
    {
        this.session = session;
        this.casino = casino;
        this.store = store;
    }

    public bool InFlight => inFlight;

    public CasinoDealerHoldemDto? TakeResult() => Interlocked.Exchange(ref result, null);

    public CasinoDealerHoldemOpenDto? TakeOpen() => Interlocked.Exchange(ref openResult, null);

    public bool TakeFailure()
    {
        if (!failed)
        {
            return false;
        }

        failed = false;
        return true;
    }

    public void Start(long ante, long trips)
    {
        var sittingId = SittingId();
        if (!Ready() || sittingId.Length == 0 || !DealerHoldemRules.IsOpening(ante, trips))
        {
            return;
        }

        var held = pending;
        var outgoing = ResendsPending(held, sittingId, ante, trips)
            ? held!
            : new DealerHoldemDeal(sittingId, Guid.NewGuid().ToString("N"), ante, trips);
        pending = outgoing;
        inFlight = true;
        work.Run("dealer holdem start", async token =>
        {
            var answer = await casino.StartDealerHoldemAsync(outgoing.SittingId, outgoing.ClientRoundId,
                outgoing.Ante, outgoing.Trips, token).ConfigureAwait(false);
            if (answer is not null)
            {
                Interlocked.CompareExchange(ref pending, null, outgoing);
            }

            Deliver(outgoing.SittingId, answer);
        }, () => inFlight = false);
    }

    public void Decide(string roundId, int step, string action, int multiple)
    {
        if (!Ready() || roundId.Length == 0 || !DealerHoldemRules.IsAction(action))
        {
            return;
        }

        var sittingId = SittingId();
        inFlight = true;
        work.Run("dealer holdem decide", async token =>
        {
            var answer = await casino.DecideDealerHoldemAsync(roundId, step, action, multiple, token)
                .ConfigureAwait(false);
            Deliver(sittingId, answer);
        }, () => inFlight = false);
    }

    public void LoadOpen()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        work.Run("dealer holdem open", async token =>
        {
            var answer = await casino.OpenDealerHoldemAsync(token).ConfigureAwait(false);
            if (answer is not null)
            {
                Interlocked.Exchange(ref openResult, answer);
            }
        });
    }

    internal static bool ResendsPending(DealerHoldemDeal? held, string sittingId, long ante, long trips) =>
        held is not null && sittingId.Length > 0 && held.Ante == ante && held.Trips == trips
        && string.Equals(held.SittingId, sittingId, StringComparison.Ordinal);

    private void Deliver(string sittingId, CasinoDealerHoldemDto? answer)
    {
        if (answer is null)
        {
            failed = true;
            LoadOpen();
            return;
        }

        Interlocked.Exchange(ref result, answer);
        if (answer.Granted && sittingId.Length > 0)
        {
            store.AbsorbStack(sittingId, answer.Stack);
            return;
        }

        store.RefreshNow();
    }

    private bool Ready() => !inFlight && session.IsSignedIn;

    private string SittingId() => store.State?.Sitting?.Id ?? string.Empty;

    public void Dispose()
    {
        work.Dispose();
    }
}

internal sealed record DealerHoldemDeal(string SittingId, string ClientRoundId, long Ante, long Trips);
