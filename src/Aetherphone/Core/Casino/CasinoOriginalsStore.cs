using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum OriginalsGame : byte
{
    Mines,
    Dice,
    Limbo,
    Keno,
    HiLo,
}

internal sealed class CasinoOriginalsStore : IDisposable
{
    private const int NoFailure = -1;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore store;
    private readonly StoreWork work = new("CasinoOriginals");

    private volatile bool inFlight;
    private CasinoMinesDto? minesResult;
    private CasinoDiceDto? diceResult;
    private CasinoLimboDto? limboResult;
    private CasinoKenoDto? kenoResult;
    private CasinoHiLoDto? hiLoResult;
    private CasinoOriginalsOpenDto? openResult;
    private int failedGame = NoFailure;
    private PendingStake? pending;

    public CasinoOriginalsStore(AethernetSession session, CasinoClient casino, CasinoStore store)
    {
        this.session = session;
        this.casino = casino;
        this.store = store;
    }

    public bool InFlight => inFlight;

    public CasinoMinesDto? TakeMines() => Interlocked.Exchange(ref minesResult, null);

    public CasinoDiceDto? TakeDice() => Interlocked.Exchange(ref diceResult, null);

    public CasinoLimboDto? TakeLimbo() => Interlocked.Exchange(ref limboResult, null);

    public CasinoKenoDto? TakeKeno() => Interlocked.Exchange(ref kenoResult, null);

    public CasinoHiLoDto? TakeHiLo() => Interlocked.Exchange(ref hiLoResult, null);

    public CasinoOriginalsOpenDto? TakeOpen() => Interlocked.Exchange(ref openResult, null);

    public bool TakeFailure(OriginalsGame game) =>
        Interlocked.CompareExchange(ref failedGame, NoFailure, (int)game) == (int)game;

    public void StartMines(long stake, int mines)
    {
        Stake(new PendingStake(OriginalsGame.Mines, string.Empty, string.Empty, stake, mines, false, null));
    }

    public void RollDice(long stake, int target, bool over)
    {
        Stake(new PendingStake(OriginalsGame.Dice, string.Empty, string.Empty, stake, target, over, null));
    }

    public void PlayLimbo(long stake, int target)
    {
        Stake(new PendingStake(OriginalsGame.Limbo, string.Empty, string.Empty, stake, target, false, null));
    }

    public void DrawKeno(long stake, int risk, ReadOnlySpan<int> picks)
    {
        if (!OriginalsRules.AreKenoPicks(picks))
        {
            return;
        }

        Stake(new PendingStake(OriginalsGame.Keno, string.Empty, string.Empty, stake, risk, false, picks.ToArray()));
    }

    public void StartHiLo(long stake)
    {
        Stake(new PendingStake(OriginalsGame.HiLo, string.Empty, string.Empty, stake, 0, false, null));
    }

    public void RevealMine(string roundId, int tile)
    {
        if (!Ready() || roundId.Length == 0 || tile < 0 || tile >= OriginalsRules.MinesTiles)
        {
            return;
        }

        var sittingId = SittingId();
        inFlight = true;
        work.Run("mines reveal", async token =>
        {
            var result = await casino.RevealMineAsync(roundId, tile, token).ConfigureAwait(false);
            Deliver(OriginalsGame.Mines, sittingId, result, result?.Granted ?? false, result?.Stack ?? 0,
                ref minesResult);
        }, () => inFlight = false);
    }

    public void CashOutMines(string roundId)
    {
        if (!Ready() || roundId.Length == 0)
        {
            return;
        }

        var sittingId = SittingId();
        inFlight = true;
        work.Run("mines cashout", async token =>
        {
            var result = await casino.CashOutMinesAsync(roundId, token).ConfigureAwait(false);
            Deliver(OriginalsGame.Mines, sittingId, result, result?.Granted ?? false, result?.Stack ?? 0,
                ref minesResult);
        }, () => inFlight = false);
    }

    public void GuessHiLo(string roundId, int step, HiLoCall call)
    {
        if (!Ready() || roundId.Length == 0 || call == HiLoCall.Skip)
        {
            return;
        }

        var sittingId = SittingId();
        var wire = OriginalsRules.CallWire(call);
        inFlight = true;
        work.Run("hilo guess", async token =>
        {
            var result = await casino.GuessHiLoAsync(roundId, step, wire, token).ConfigureAwait(false);
            Deliver(OriginalsGame.HiLo, sittingId, result, result?.Granted ?? false, result?.Stack ?? 0,
                ref hiLoResult);
        }, () => inFlight = false);
    }

    public void SkipHiLo(string roundId, int step)
    {
        if (!Ready() || roundId.Length == 0)
        {
            return;
        }

        var sittingId = SittingId();
        inFlight = true;
        work.Run("hilo skip", async token =>
        {
            var result = await casino.SkipHiLoAsync(roundId, step, token).ConfigureAwait(false);
            Deliver(OriginalsGame.HiLo, sittingId, result, result?.Granted ?? false, result?.Stack ?? 0,
                ref hiLoResult);
        }, () => inFlight = false);
    }

    public void CashOutHiLo(string roundId)
    {
        if (!Ready() || roundId.Length == 0)
        {
            return;
        }

        var sittingId = SittingId();
        inFlight = true;
        work.Run("hilo cashout", async token =>
        {
            var result = await casino.CashOutHiLoAsync(roundId, token).ConfigureAwait(false);
            Deliver(OriginalsGame.HiLo, sittingId, result, result?.Granted ?? false, result?.Stack ?? 0,
                ref hiLoResult);
        }, () => inFlight = false);
    }

    public void LoadOpen()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        work.Run("originals open", async token =>
        {
            var result = await casino.OpenOriginalsAsync(token).ConfigureAwait(false);
            if (result is not null)
            {
                Interlocked.Exchange(ref openResult, result);
            }
        });
    }

    internal static bool ResendsPending(PendingStake? held, OriginalsGame game, string sittingId) =>
        held is not null && held.Game == game && sittingId.Length > 0
        && string.Equals(held.SittingId, sittingId, StringComparison.Ordinal);

    private void Stake(PendingStake request)
    {
        var sittingId = SittingId();
        if (!Ready() || sittingId.Length == 0 || request.Stake < OriginalsRules.MinBet)
        {
            return;
        }

        var held = pending;
        var outgoing = ResendsPending(held, request.Game, sittingId)
            ? held!
            : request with { SittingId = sittingId, ClientRoundId = Guid.NewGuid().ToString("N") };
        pending = outgoing;
        inFlight = true;
        work.Run("originals stake", async token =>
        {
            switch (outgoing.Game)
            {
                case OriginalsGame.Mines:
                {
                    var result = await casino.StartMinesAsync(outgoing.SittingId, outgoing.ClientRoundId,
                        outgoing.Stake, outgoing.Setting, token).ConfigureAwait(false);
                    Settle(outgoing, result, result?.Granted ?? false, result?.Stack ?? 0, ref minesResult);
                    break;
                }
                case OriginalsGame.Dice:
                {
                    var result = await casino.RollDiceAsync(outgoing.SittingId, outgoing.ClientRoundId,
                        outgoing.Stake, outgoing.Setting, outgoing.Over, token).ConfigureAwait(false);
                    Settle(outgoing, result, result?.Granted ?? false, result?.Stack ?? 0, ref diceResult);
                    break;
                }
                case OriginalsGame.Limbo:
                {
                    var result = await casino.PlayLimboAsync(outgoing.SittingId, outgoing.ClientRoundId,
                        outgoing.Stake, outgoing.Setting, token).ConfigureAwait(false);
                    Settle(outgoing, result, result?.Granted ?? false, result?.Stack ?? 0, ref limboResult);
                    break;
                }
                case OriginalsGame.Keno:
                {
                    var result = await casino.DrawKenoAsync(outgoing.SittingId, outgoing.ClientRoundId,
                        outgoing.Stake, outgoing.Setting, outgoing.Picks ?? Array.Empty<int>(), token)
                        .ConfigureAwait(false);
                    Settle(outgoing, result, result?.Granted ?? false, result?.Stack ?? 0, ref kenoResult);
                    break;
                }
                default:
                {
                    var result = await casino.StartHiLoAsync(outgoing.SittingId, outgoing.ClientRoundId,
                        outgoing.Stake, token).ConfigureAwait(false);
                    Settle(outgoing, result, result?.Granted ?? false, result?.Stack ?? 0, ref hiLoResult);
                    break;
                }
            }
        }, () => inFlight = false);
    }

    private void Settle<T>(PendingStake sent, T? result, bool granted, long stack, ref T? slot) where T : class
    {
        if (result is not null)
        {
            Interlocked.CompareExchange(ref pending, null, sent);
        }

        Deliver(sent.Game, sent.SittingId, result, granted, stack, ref slot);
    }

    private void Deliver<T>(OriginalsGame game, string sittingId, T? result, bool granted, long stack, ref T? slot)
        where T : class
    {
        if (result is null)
        {
            Interlocked.Exchange(ref failedGame, (int)game);
            if (game is OriginalsGame.Mines or OriginalsGame.HiLo)
            {
                LoadOpen();
            }

            return;
        }

        Interlocked.Exchange(ref slot, result);
        if (granted)
        {
            store.AbsorbStack(sittingId, stack);
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

internal sealed record PendingStake(
    OriginalsGame Game,
    string SittingId,
    string ClientRoundId,
    long Stake,
    int Setting,
    bool Over,
    int[]? Picks);
