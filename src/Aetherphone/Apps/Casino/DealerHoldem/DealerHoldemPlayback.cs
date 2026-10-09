using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal enum DealerHoldemCueKind : byte
{
    Hero,
    Dealer,
    Board,
    Reveal,
    Verdict,
    Resolve,
    Settle,
}

internal readonly record struct DealerHoldemCue(DealerHoldemCueKind Kind, int Index, float At, bool Instant);

internal sealed class DealerHoldemPlayback
{
    public const int Capacity = 24;
    public const float DealGap = 0.16f;
    public const float BoardGap = 0.14f;
    public const float StreetPause = 0.25f;
    public const float RevealPause = 0.35f;
    public const float VerdictPause = 0.55f;
    public const float ResolveGap = 0.5f;
    public const float SettlePause = 0.35f;

    public static readonly DealerHoldemSpot[] ResolveOrder =
    {
        DealerHoldemSpot.Play,
        DealerHoldemSpot.Ante,
        DealerHoldemSpot.Blind,
        DealerHoldemSpot.Trips,
    };

    private readonly DealerHoldemCue[] queue = new DealerHoldemCue[Capacity];
    private readonly bool[] resolved = new bool[DealerHoldemRules.SpotCount];

    private CasinoDealerHoldemDto? round;
    private int head;
    private int count;
    private float clock;
    private float tail;
    private int heroScheduled;
    private int dealerScheduled;
    private int boardScheduled;
    private bool revealScheduled;
    private bool settleScheduled;
    private int heroShown;
    private int dealerShown;
    private int boardShown;
    private bool revealed;
    private bool verdict;
    private bool settled;

    public CasinoDealerHoldemDto? Round => round;

    public string RoundId => round?.RoundId ?? string.Empty;

    public bool HasRound => round is not null;

    public bool Busy => count > 0;

    public int HeroShown => heroShown;

    public int DealerShown => dealerShown;

    public int BoardShown => boardShown;

    public bool Revealed => revealed;

    public bool VerdictShown => verdict;

    public bool Settled => settled;

    public bool Deciding => round is not null && !Busy && DealerHoldemRules.IsDecision(round.Phase)
                            && (round.Actions?.Length ?? 0) > 0;

    public bool Finishing => round is not null && DealerHoldemRules.IsDecision(round.Phase)
                             && (round.Actions?.Length ?? 0) == 0;

    public bool Open => round is not null && !DealerHoldemRules.IsOver(round.Phase);

    public bool IsResolved(DealerHoldemSpot spot) => resolved[(int)spot];

    public int Card(DealerHoldemCueKind kind, int index)
    {
        var cards = kind switch
        {
            DealerHoldemCueKind.Hero => round?.PlayerCards,
            DealerHoldemCueKind.Board => round?.Board,
            _ => round?.DealerCards,
        };
        return cards is not null && index >= 0 && index < cards.Length ? cards[index] : -1;
    }

    public void Clear()
    {
        round = null;
        head = 0;
        count = 0;
        clock = 0f;
        tail = 0f;
        heroScheduled = 0;
        dealerScheduled = 0;
        boardScheduled = 0;
        revealScheduled = false;
        settleScheduled = false;
        heroShown = 0;
        dealerShown = 0;
        boardShown = 0;
        revealed = false;
        verdict = false;
        settled = false;
        Array.Clear(resolved);
    }

    public bool Apply(CasinoDealerHoldemDto next, bool instant)
    {
        if (next.RoundId.Length == 0)
        {
            return false;
        }

        if (round is null || !string.Equals(round.RoundId, next.RoundId, StringComparison.Ordinal))
        {
            Clear();
        }

        round = next;
        if (instant)
        {
            Snap();
        }

        tail = MathF.Max(tail, clock);
        ScheduleDeal(next, instant);
        ScheduleBoard(next, instant);
        ScheduleShowdown(next, instant);
        return true;
    }

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds > 0f)
        {
            clock += deltaSeconds;
        }
    }

    public void Snap()
    {
        clock = MathF.Max(clock, tail);
    }

    public bool TryTake(out DealerHoldemCue cue)
    {
        cue = default;
        if (count == 0 || queue[head].At > clock)
        {
            return false;
        }

        cue = queue[head];
        head = (head + 1) % Capacity;
        count--;
        Show(cue);
        return true;
    }

    private void Show(in DealerHoldemCue cue)
    {
        switch (cue.Kind)
        {
            case DealerHoldemCueKind.Hero:
                heroShown = Math.Max(heroShown, cue.Index + 1);
                break;
            case DealerHoldemCueKind.Dealer:
                dealerShown = Math.Max(dealerShown, cue.Index + 1);
                break;
            case DealerHoldemCueKind.Board:
                boardShown = Math.Max(boardShown, cue.Index + 1);
                break;
            case DealerHoldemCueKind.Reveal:
                revealed = true;
                break;
            case DealerHoldemCueKind.Verdict:
                verdict = true;
                break;
            case DealerHoldemCueKind.Resolve:
                resolved[cue.Index] = true;
                break;
            default:
                settled = true;
                break;
        }
    }

    private void ScheduleDeal(CasinoDealerHoldemDto next, bool instant)
    {
        var hero = Math.Min(DealerHoldemRules.HoleCards, next.PlayerCards?.Length ?? 0);
        while (heroScheduled < hero || dealerScheduled < DealerHoldemRules.HoleCards)
        {
            if (heroScheduled < hero)
            {
                Enqueue(DealerHoldemCueKind.Hero, heroScheduled, DealGap, instant);
                heroScheduled++;
            }

            if (dealerScheduled < DealerHoldemRules.HoleCards)
            {
                Enqueue(DealerHoldemCueKind.Dealer, dealerScheduled, DealGap, instant);
                dealerScheduled++;
            }
        }
    }

    private void ScheduleBoard(CasinoDealerHoldemDto next, bool instant)
    {
        var board = Math.Min(DealerHoldemRules.BoardCards, next.Board?.Length ?? 0);
        if (boardScheduled >= board)
        {
            return;
        }

        Pause(StreetPause, instant);
        while (boardScheduled < board)
        {
            Enqueue(DealerHoldemCueKind.Board, boardScheduled, BoardGap, instant);
            boardScheduled++;
        }
    }

    private void ScheduleShowdown(CasinoDealerHoldemDto next, bool instant)
    {
        if (!DealerHoldemRules.IsOver(next.Phase) || settleScheduled)
        {
            return;
        }

        if (!revealScheduled && (next.DealerCards?.Length ?? 0) >= DealerHoldemRules.HoleCards)
        {
            Enqueue(DealerHoldemCueKind.Reveal, 0, RevealPause, instant);
            Enqueue(DealerHoldemCueKind.Verdict, 0, VerdictPause, instant);
            revealScheduled = true;
        }

        for (var index = 0; index < ResolveOrder.Length; index++)
        {
            var spot = ResolveOrder[index];
            if (StakeOf(next, spot) <= 0)
            {
                continue;
            }

            Enqueue(DealerHoldemCueKind.Resolve, (int)spot, ResolveGap, instant);
        }

        Enqueue(DealerHoldemCueKind.Settle, 0, SettlePause, instant);
        settleScheduled = true;
    }

    private void Pause(float seconds, bool instant)
    {
        if (!instant)
        {
            tail += seconds;
        }
    }

    private void Enqueue(DealerHoldemCueKind kind, int index, float gap, bool instant)
    {
        if (count >= Capacity)
        {
            return;
        }

        if (!instant)
        {
            tail += gap;
        }

        queue[(head + count) % Capacity] = new DealerHoldemCue(kind, index, instant ? clock : tail, instant);
        count++;
    }

    public static long StakeOf(CasinoDealerHoldemDto round, DealerHoldemSpot spot) => spot switch
    {
        DealerHoldemSpot.Trips => round.Trips,
        DealerHoldemSpot.Ante => round.Ante,
        DealerHoldemSpot.Blind => round.Blind,
        _ => round.Play,
    };

    public static long ReturnOf(CasinoDealerHoldemDto round, DealerHoldemSpot spot) => spot switch
    {
        DealerHoldemSpot.Trips => round.TripsPayout,
        DealerHoldemSpot.Ante => round.AntePayout,
        DealerHoldemSpot.Blind => round.BlindPayout,
        _ => round.PlayPayout,
    };

    public static DealerHoldemResult ResultOf(CasinoDealerHoldemDto round, DealerHoldemSpot spot) =>
        DealerHoldemRules.IsOver(round.Phase)
            ? DealerHoldemRules.ResultOf(StakeOf(round, spot), ReturnOf(round, spot))
            : DealerHoldemResult.None;
}
