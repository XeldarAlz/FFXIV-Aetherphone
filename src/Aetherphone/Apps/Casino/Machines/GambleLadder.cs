using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Machines;

internal enum GamblePhase : byte
{
    None,
    Offered,
    Choosing,
    Waiting,
    Revealing,
    Closing,
}

internal sealed class GambleLadder
{
    public const float RevealSeconds = 0.7f;
    public const float CloseSeconds = 1.4f;

    private GamblePhase phase;
    private float seconds;
    private int lastPick = -1;

    public GamblePhase Phase => phase;

    public bool Open => phase is GamblePhase.Choosing or GamblePhase.Waiting or GamblePhase.Revealing
        or GamblePhase.Closing;

    public bool Offered => phase == GamblePhase.Offered;

    public string ParentRoundId { get; private set; } = string.Empty;

    public long Bet { get; private set; }

    public long Amount { get; private set; }

    public long Base { get; private set; }

    public long Staked { get; private set; }

    public int Step { get; private set; }

    public int Card { get; private set; } = -1;

    public bool Won { get; private set; }

    public bool CanContinue { get; private set; }

    public string LastRoundId { get; private set; } = string.Empty;

    public float RevealProgress => phase == GamblePhase.Revealing ? Math.Clamp(seconds / RevealSeconds, 0f, 1f)
        : phase == GamblePhase.Closing ? 1f : 0f;

    public bool CardShown => phase is GamblePhase.Revealing or GamblePhase.Closing && RevealProgress >= 0.5f;

    public long UnshownPayout => phase is GamblePhase.Waiting or GamblePhase.Revealing && !CardShown
        ? Amount
        : 0;

    public int LastPick => lastPick;

    public void Offer(string roundId, long bet, long amount)
    {
        Reset();
        if (!SlotsRules.GambleEligible(SlotsRules.BirdId, bet, amount) || roundId.Length == 0)
        {
            return;
        }

        phase = GamblePhase.Offered;
        ParentRoundId = roundId;
        Bet = bet;
        Amount = amount;
        Base = amount;
    }

    public void Choose()
    {
        if (phase == GamblePhase.Offered)
        {
            phase = GamblePhase.Choosing;
            Card = -1;
        }
    }

    public bool Pick(CasinoPlayStore play, int pick)
    {
        if (phase != GamblePhase.Choosing || !play.GambleSlots(ParentRoundId, pick, Amount))
        {
            return false;
        }

        lastPick = pick;
        Staked = Amount;
        phase = GamblePhase.Waiting;
        seconds = 0f;
        return true;
    }

    public bool Absorb(CasinoSlotsGambleDto result)
    {
        if (phase != GamblePhase.Waiting)
        {
            return false;
        }

        if (!result.Granted)
        {
            Reset();
            return false;
        }

        Card = result.Card;
        Won = result.Won;
        CanContinue = result.CanContinue;
        Step = result.Step;
        Staked = result.Stake;
        Amount = result.Payout;
        LastRoundId = result.RoundId;
        ParentRoundId = result.RoundId;
        phase = GamblePhase.Revealing;
        seconds = 0f;
        return true;
    }

    public void Fail()
    {
        if (phase == GamblePhase.Waiting)
        {
            Reset();
        }
    }

    public void Update(float deltaSeconds)
    {
        seconds += deltaSeconds;
        if (phase == GamblePhase.Revealing && seconds >= RevealSeconds)
        {
            if (Won && CanContinue)
            {
                phase = GamblePhase.Choosing;
                seconds = 0f;
                return;
            }

            phase = GamblePhase.Closing;
            seconds = 0f;
            return;
        }

        if (phase == GamblePhase.Closing && seconds >= CloseSeconds)
        {
            Reset();
        }
    }

    public void Snap()
    {
        if (phase is GamblePhase.Revealing or GamblePhase.Closing)
        {
            if (Won && CanContinue)
            {
                phase = GamblePhase.Choosing;
                return;
            }

            Reset();
        }
    }

    public void Collect()
    {
        if (phase is GamblePhase.Offered or GamblePhase.Choosing)
        {
            Reset();
        }
    }

    public long RungAmount(int rung)
    {
        var value = Base;
        for (var index = 0; index < rung; index++)
        {
            value *= 2;
        }

        return value;
    }

    public void Reset()
    {
        phase = GamblePhase.None;
        seconds = 0f;
        ParentRoundId = string.Empty;
        Amount = 0;
        Base = 0;
        Staked = 0;
        Step = 0;
        Card = -1;
        Won = false;
        CanContinue = false;
        lastPick = -1;
    }
}
