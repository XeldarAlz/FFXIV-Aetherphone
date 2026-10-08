using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class HoldemSeatFlow
{
    private readonly HoldemStore store;

    private CasinoSeatStage stage = CasinoSeatStage.Watching;
    private CasinoSeatSettle settle = CasinoSeatSettle.None;
    private string reason = string.Empty;
    private long settleArmedAtTick;
    private bool standQueued;

    public HoldemSeatFlow(HoldemStore store)
    {
        this.store = store;
    }

    public CasinoSeatStage Stage => stage;

    public string Reason => reason;

    public bool StandQueued => standQueued;

    public bool Busy => CasinoSeatMachine.Busy(stage) || store.IntentInFlight;

    public bool Elsewhere => CasinoSeatMachine.ShowsTakeOver(stage);

    public void Reset()
    {
        stage = CasinoSeatStage.Watching;
        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        reason = string.Empty;
        standQueued = false;
    }

    public void ClearReason()
    {
        reason = string.Empty;
    }

    public void Observe(bool hasSeat, bool handedOff, bool leaving, long nowTick)
    {
        ConsumeOutcomes(nowTick);
        var signal = CasinoSeatMachine.SignalFor(hasSeat, handedOff);
        if (!Settled(signal, nowTick))
        {
            return;
        }

        stage = CasinoSeatMachine.Next(stage, signal);
        if (!hasSeat)
        {
            standQueued = false;
            return;
        }

        standQueued = standQueued || leaving;
    }

    public void Sit(string roomId, int seatIndex, long buyIn, bool postBigBlind)
    {
        if (Busy || CasinoSeatMachine.Holds(stage) || roomId.Length == 0)
        {
            return;
        }

        reason = string.Empty;
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitRequested);
        store.Sit(roomId, seatIndex, buyIn, postBigBlind);
    }

    public void Stand(string roomId)
    {
        if (Busy || roomId.Length == 0)
        {
            return;
        }

        reason = string.Empty;
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandRequested);
        store.Leave(roomId);
    }

    public void Claim(CasinoRoomSession room)
    {
        if (!CasinoSeatMachine.ShowsTakeOver(stage))
        {
            return;
        }

        reason = string.Empty;
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.TakeOverRequested);
        settle = CasinoSeatMachine.SettleFor(CasinoSeatStage.Claiming, false);
        settleArmedAtTick = Environment.TickCount64;
        room.Claim();
    }

    public void Left()
    {
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.Left);
        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        standQueued = false;
    }

    private bool Settled(CasinoSeatSignal signal, long nowTick)
    {
        if (settle == CasinoSeatSettle.None)
        {
            return true;
        }

        if (!CasinoSeatMachine.AcceptsBoard(settle, signal, settleArmedAtTick, nowTick))
        {
            return false;
        }

        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        return true;
    }

    private void ConsumeOutcomes(long nowTick)
    {
        if (store.TakeIntentFailure())
        {
            reason = CasinoReasons.Unreachable;
            settle = CasinoSeatSettle.None;
            settleArmedAtTick = 0;
            stage = RefusalOf(stage);
        }

        var outcome = store.TakeSeatOutcome();
        if (outcome is null)
        {
            return;
        }

        if (!outcome.Granted)
        {
            reason = outcome.Reason;
            settle = CasinoSeatSettle.None;
            settleArmedAtTick = 0;
            stage = RefusalOf(stage);
            return;
        }

        reason = string.Empty;
        standQueued = outcome.AtHandEnd;
        settle = CasinoSeatMachine.SettleFor(stage, outcome.AtHandEnd);
        settleArmedAtTick = nowTick;
        stage = CasinoSeatMachine.Next(stage, GrantOf(stage, outcome.AtHandEnd));
    }

    private static CasinoSeatSignal GrantOf(CasinoSeatStage stage, bool atHandEnd)
    {
        return stage switch
        {
            CasinoSeatStage.Sitting => CasinoSeatSignal.SitGranted,
            CasinoSeatStage.Claiming => CasinoSeatSignal.ClaimGranted,
            CasinoSeatStage.Standing => atHandEnd ? CasinoSeatSignal.StandQueued : CasinoSeatSignal.StandGranted,
            _ => CasinoSeatSignal.SeatBoundHere,
        };
    }

    private static CasinoSeatStage RefusalOf(CasinoSeatStage stage)
    {
        return stage switch
        {
            CasinoSeatStage.Sitting => CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitRefused),
            CasinoSeatStage.Claiming => CasinoSeatMachine.Next(stage, CasinoSeatSignal.ClaimRefused),
            CasinoSeatStage.Standing => CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandRefused),
            _ => stage,
        };
    }
}
