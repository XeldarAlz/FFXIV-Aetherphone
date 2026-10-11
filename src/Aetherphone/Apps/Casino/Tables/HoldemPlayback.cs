using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal enum HoldemLaunchKind : byte
{
    Hole,
    Board,
}

internal readonly struct HoldemLaunch
{
    public readonly HoldemLaunchKind Kind;
    public readonly int Seat;
    public readonly int Slot;
    public readonly float Delay;

    public HoldemLaunch(HoldemLaunchKind kind, int seat, int slot, float delay)
    {
        Kind = kind;
        Seat = seat;
        Slot = slot;
        Delay = delay;
    }

    public int Tag => Kind == HoldemLaunchKind.Board
        ? HoldemPlayback.BoardTagBase + Slot
        : HoldemPlayback.HoleTagBase + Seat * HoldemRules.HoleCards + Slot;
}

internal sealed class HoldemPlayback
{
    public const int MaxSeats = HoldemRules.MaxSeats;
    public const int BoardTagBase = 100;
    public const int HoleTagBase = 200;
    public const float RevealSeconds = HoldemRules.RevealMillisecondsPerCard / 1000f;
    public const float DealStagger = 0.08f;
    public const float FlightSeconds = 0.42f;

    private const int LaunchCapacity = 32;

    private readonly HoldemLaunch[] launches = new HoldemLaunch[LaunchCapacity];
    private readonly long[] bets = new long[MaxSeats];
    private readonly long[] swept = new long[MaxSeats];
    private readonly bool[] dealt = new bool[MaxSeats];
    private readonly bool[] shown = new bool[MaxSeats];
    private readonly bool[] revealed = new bool[MaxSeats];
    private readonly int[] order = new int[MaxSeats];
    private readonly int[] dealtSeats = new int[MaxSeats];

    private string handId = string.Empty;
    private int launchHead;
    private int launchCount;
    private int boardSeen;
    private int phase = -1;
    private bool primed;
    private bool witnessed;
    private bool resultPending;
    private bool resultSeen;
    private bool resultWitnessed;
    private bool sweepPending;
    private bool voidPending;
    private bool handStarted;

    public string HandId => handId;

    public int BoardSeen => boardSeen;

    public bool Witnessed => witnessed;

    public void Reset()
    {
        handId = string.Empty;
        launchHead = 0;
        launchCount = 0;
        boardSeen = 0;
        phase = -1;
        primed = false;
        witnessed = false;
        resultPending = false;
        resultSeen = false;
        resultWitnessed = false;
        sweepPending = false;
        voidPending = false;
        handStarted = false;
        Array.Clear(bets);
        Array.Clear(swept);
        Array.Clear(dealt);
        Array.Clear(shown);
        Array.Clear(revealed);
    }

    public bool HoleDealt(int seat) => seat >= 0 && seat < MaxSeats && dealt[seat];

    public bool BoardDealt(int index) => index >= 0 && index < boardSeen;

    public void Advance(CasinoHoldemRoomStateDto? board, bool snap)
    {
        if (board is null)
        {
            return;
        }

        var seats = board.Seats ?? Array.Empty<CasinoHoldemSeatDto>();
        var boardCards = board.Board ?? Array.Empty<int>();
        var join = !primed || snap;
        primed = true;
        if (!string.Equals(handId, board.HandId, StringComparison.Ordinal))
        {
            BeginHand(board, seats, join);
        }

        if (board.HandId.Length == 0)
        {
            phase = board.Phase;
            return;
        }

        TrackBoard(boardCards, join);
        TrackBets(board, seats, join);
        TrackReveals(seats, join);
        TrackResult(board, join);
        phase = board.Phase;
    }

    public bool TryTakeLaunch(out HoldemLaunch launch)
    {
        if (launchCount == 0)
        {
            launch = default;
            return false;
        }

        launch = launches[launchHead];
        launchHead = (launchHead + 1) % LaunchCapacity;
        launchCount--;
        return true;
    }

    public bool TakeHandStarted()
    {
        if (!handStarted)
        {
            return false;
        }

        handStarted = false;
        return true;
    }

    public bool TryTakeSweep(Span<long> amounts)
    {
        if (!sweepPending)
        {
            return false;
        }

        sweepPending = false;
        var limit = Math.Min(amounts.Length, MaxSeats);
        for (var seat = 0; seat < limit; seat++)
        {
            amounts[seat] = swept[seat];
        }

        return true;
    }

    public bool TryTakeReveal(out int seat)
    {
        for (var index = 0; index < MaxSeats; index++)
        {
            if (!revealed[index])
            {
                continue;
            }

            revealed[index] = false;
            seat = index;
            return true;
        }

        seat = -1;
        return false;
    }

    public bool TryTakeResult(out bool live)
    {
        if (!resultPending)
        {
            live = false;
            return false;
        }

        resultPending = false;
        live = resultWitnessed;
        return true;
    }

    public bool TakeVoided()
    {
        if (!voidPending)
        {
            return false;
        }

        voidPending = false;
        return true;
    }

    private void BeginHand(CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto[] seats, bool join)
    {
        handId = board.HandId;
        launchHead = 0;
        launchCount = 0;
        boardSeen = 0;
        resultPending = false;
        resultSeen = false;
        resultWitnessed = false;
        sweepPending = false;
        Array.Clear(bets);
        Array.Clear(dealt);
        Array.Clear(shown);
        Array.Clear(revealed);
        if (board.HandId.Length == 0)
        {
            witnessed = false;
            return;
        }

        var live = !join && board.Phase == HoldemPhases.Preflop;
        witnessed = !join;
        handStarted = live;
        var count = 0;
        for (var index = 0; index < seats.Length && count < MaxSeats; index++)
        {
            var seat = seats[index];
            if (!IsSeat(seat.SeatIndex) || !HoldemSeatStates.DealtIn(seat.State))
            {
                continue;
            }

            dealtSeats[count] = seat.SeatIndex;
            count++;
        }

        var dealtSpan = new ReadOnlySpan<int>(dealtSeats, 0, count);
        for (var index = 0; index < count; index++)
        {
            dealt[dealtSeats[index]] = true;
        }

        if (!live)
        {
            return;
        }

        var ordered = HoldemRules.DealOrder(dealtSpan, board.Button, order);
        var step = 0;
        for (var pass = 0; pass < HoldemRules.HoleCards; pass++)
        {
            for (var index = 0; index < ordered; index++)
            {
                Push(new HoldemLaunch(HoldemLaunchKind.Hole, order[index], pass, step * DealStagger));
                step++;
            }
        }
    }

    private void TrackBoard(int[] boardCards, bool join)
    {
        var length = Math.Min(boardCards.Length, HoldemRules.BoardSize);
        if (length <= boardSeen)
        {
            boardSeen = length;
            return;
        }

        if (!join)
        {
            for (var index = boardSeen; index < length; index++)
            {
                Push(new HoldemLaunch(HoldemLaunchKind.Board, -1, index, (index - boardSeen) * RevealSeconds));
            }
        }

        boardSeen = length;
    }

    private void TrackBets(CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto[] seats, bool join)
    {
        var heldTotal = 0L;
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            heldTotal += bets[seat];
        }

        var nowTotal = 0L;
        for (var index = 0; index < seats.Length; index++)
        {
            nowTotal += seats[index].Bet;
        }

        var streetMoved = phase != board.Phase && phase >= HoldemPhases.Preflop;
        if (!join && streetMoved && heldTotal > 0 && nowTotal == 0)
        {
            Array.Copy(bets, swept, MaxSeats);
            sweepPending = true;
        }

        Array.Clear(bets);
        for (var index = 0; index < seats.Length; index++)
        {
            var seat = seats[index].SeatIndex;
            if (IsSeat(seat))
            {
                bets[seat] = seats[index].Bet;
            }
        }
    }

    private void TrackReveals(CasinoHoldemSeatDto[] seats, bool join)
    {
        for (var index = 0; index < seats.Length; index++)
        {
            var seat = seats[index].SeatIndex;
            if (!IsSeat(seat))
            {
                continue;
            }

            var showing = seats[index].Shown && HasRealCards(seats[index].Cards);
            if (showing && !shown[seat] && !join)
            {
                revealed[seat] = true;
            }

            shown[seat] = showing;
        }
    }

    private void TrackResult(CasinoHoldemRoomStateDto board, bool join)
    {
        if (board.Phase == HoldemPhases.Voided)
        {
            if (phase != HoldemPhases.Voided && !join)
            {
                voidPending = true;
            }

            return;
        }

        var settled = board.Winners is { Length: > 0 } && board.Phase >= HoldemPhases.Showdown;
        if (resultSeen || !(HoldemPhases.Over(board.Phase) || settled))
        {
            return;
        }

        resultSeen = true;
        resultPending = true;
        resultWitnessed = !join;
    }

    private void Push(in HoldemLaunch launch)
    {
        if (launchCount == LaunchCapacity)
        {
            launchHead = (launchHead + 1) % LaunchCapacity;
            launchCount--;
        }

        launches[(launchHead + launchCount) % LaunchCapacity] = launch;
        launchCount++;
    }

    internal static bool HasRealCards(int[]? cards)
    {
        if (cards is null || cards.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < cards.Length; index++)
        {
            if (cards[index] < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSeat(int seat) => seat >= 0 && seat < MaxSeats;
}
