using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceTicketBuilder
{
    private long roundIndex = -1;

    public int Kind { get; private set; } = RaceRules.KindWin;

    public int First { get; private set; } = RaceRules.NoRunner;

    public int Second { get; private set; } = RaceRules.NoRunner;

    public long RiddenRound { get; private set; } = -1;

    public bool Pair => RaceRules.IsPair(Kind);

    public int RunnerB => Pair ? Second : RaceRules.NoRunner;

    public bool Ready => RaceRules.IsTicket(Kind, First, RunnerB);

    public bool WantsSecond => Pair && RaceRules.IsRunner(First) && !RaceRules.IsRunner(Second);

    public void Reset()
    {
        roundIndex = -1;
        Kind = RaceRules.KindWin;
        RiddenRound = -1;
        Clear();
    }

    public void Follow(long nextRoundIndex)
    {
        if (nextRoundIndex == roundIndex)
        {
            return;
        }

        roundIndex = nextRoundIndex;
        Clear();
    }

    public void Clear()
    {
        First = RaceRules.NoRunner;
        Second = RaceRules.NoRunner;
    }

    public void SetKind(int kind)
    {
        if (!RaceRules.IsKind(kind))
        {
            return;
        }

        Kind = kind;
        if (!Pair)
        {
            Second = RaceRules.NoRunner;
        }
    }

    public void Tap(int slot)
    {
        if (!RaceRules.IsRunner(slot))
        {
            return;
        }

        if (slot == First)
        {
            First = Second;
            Second = RaceRules.NoRunner;
            return;
        }

        if (slot == Second)
        {
            Second = RaceRules.NoRunner;
            return;
        }

        if (!Pair || !RaceRules.IsRunner(First))
        {
            First = slot;
            return;
        }

        Second = slot;
    }

    public int PickOf(int slot)
    {
        if (slot == First && RaceRules.IsRunner(slot))
        {
            return 0;
        }

        return Pair && slot == Second && RaceRules.IsRunner(slot) ? 1 : -1;
    }

    public void MarkRidden(long round)
    {
        RiddenRound = round;
    }

    public static long RideAmount(long previousPayout, long ceiling, long stack)
    {
        if (previousPayout < RaceRules.MinBet)
        {
            return 0;
        }

        return CasinoLadder.Clamp(previousPayout, RaceRules.MinBet, Math.Min(ceiling, previousPayout), stack);
    }
}
