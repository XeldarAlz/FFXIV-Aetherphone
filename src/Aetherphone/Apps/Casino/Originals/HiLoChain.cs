using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Originals;

internal readonly record struct HiLoOption(HiLoCall Call, int ChanceBasisPoints, long MultiplierTenThousandths);

internal enum HiLoStep : byte
{
    Won,
    Skipped,
    Lost,
}

internal sealed class HiLoChain
{
    public const float FlipSeconds = 0.36f;
    public const int MaxOptions = 5;

    private readonly int[] cards = new int[OriginalsRules.HiLoDeck];
    private readonly HiLoCall[] moves = new HiLoCall[OriginalsRules.HiLoMaxMoves];
    private readonly HiLoStep[] steps = new HiLoStep[OriginalsRules.HiLoMaxMoves];
    private readonly long[] chainHundredths = new long[OriginalsRules.HiLoDeck];
    private readonly HiLoOption[] options = new HiLoOption[MaxOptions];

    private string roundId = string.Empty;
    private bool capped;
    private int count;
    private int optionCount;
    private int phase = OriginalsRules.PhaseLive;
    private long stake;
    private long payout;
    private long multiplier;
    private float flip = 1f;
    private bool settlePending;
    private bool landedPending;
    private bool bustPending;

    public string RoundId => roundId;

    public bool HasRound => roundId.Length > 0 && count > 0;

    public bool Live => HasRound && phase == OriginalsRules.PhaseLive;

    public int Phase => phase;

    public int Count => count;

    public int Step => Math.Max(0, count - 1);

    public int Current => count > 0 ? cards[count - 1] : -1;

    public long Stake => stake;

    public long MultiplierTenThousandths => multiplier;

    public bool CanCashOut => Live && multiplier > OriginalsRules.MultiplierUnit;

    public long CashOutValue => stake * multiplier / OriginalsRules.MultiplierUnit;

    public int OptionCount => optionCount;

    public float Flip => flip;

    public bool Animating => flip < 1f;

    public int Card(int index) => cards[index];

    public HiLoCall Move(int index) => moves[index];

    public HiLoStep StepResult(int index) => steps[index];

    public long ChainHundredths(int index) => chainHundredths[index];

    public HiLoOption Option(int index) => options[index];

    public static bool IsValid(CasinoHiLoDto dto)
    {
        var dealt = dto.Cards;
        var played = dto.Moves ?? Array.Empty<string>();
        if (!dto.Granted || dto.RoundId.Length == 0 || dealt is null || dealt.Length == 0
            || dealt.Length > OriginalsRules.HiLoDeck || played.Length != dealt.Length - 1)
        {
            return false;
        }

        for (var index = 0; index < dealt.Length; index++)
        {
            if (!OriginalsRules.IsHiLoCard(dealt[index]))
            {
                return false;
            }
        }

        for (var index = 0; index < played.Length; index++)
        {
            if (!OriginalsRules.TryParseCall(played[index], out _))
            {
                return false;
            }
        }

        var offered = dto.Calls ?? Array.Empty<CasinoHiLoCallDto>();
        for (var index = 0; index < offered.Length; index++)
        {
            if (!OriginalsRules.TryParseCall(offered[index].Call, out var call) || call == HiLoCall.Skip)
            {
                return false;
            }
        }

        return true;
    }

    public bool Apply(CasinoHiLoDto dto, bool instant)
    {
        if (!IsValid(dto))
        {
            return false;
        }

        var sameRound = string.Equals(dto.RoundId, roundId, StringComparison.Ordinal);
        var wasLive = sameRound && phase == OriginalsRules.PhaseLive;
        var previous = sameRound ? count : 0;
        Absorb(dto);
        if (count > previous)
        {
            flip = instant ? 1f : 0f;
            landedPending = true;
        }

        if (dto.Phase == OriginalsRules.PhaseBusted && (wasLive || !sameRound))
        {
            bustPending = true;
        }

        if (dto.Phase != OriginalsRules.PhaseLive && (wasLive || !sameRound))
        {
            settlePending = true;
        }

        return true;
    }

    public bool Resume(CasinoHiLoDto dto)
    {
        if (!IsValid(dto) || dto.Phase != OriginalsRules.PhaseLive)
        {
            return false;
        }

        Absorb(dto);
        flip = 1f;
        landedPending = false;
        bustPending = false;
        settlePending = false;
        return true;
    }

    public void Advance(float deltaSeconds)
    {
        if (flip < 1f)
        {
            flip = MathF.Min(1f, flip + deltaSeconds / FlipSeconds);
        }
    }

    public void Snap()
    {
        flip = 1f;
    }

    public bool TakeLanded()
    {
        if (!landedPending || flip < 1f)
        {
            return false;
        }

        landedPending = false;
        return true;
    }

    public bool TakeBust()
    {
        if (!bustPending || flip < 1f)
        {
            return false;
        }

        bustPending = false;
        return true;
    }

    public bool TakeSettled(out OriginalsOutcome outcome)
    {
        outcome = default;
        if (!settlePending || flip < 1f)
        {
            return false;
        }

        settlePending = false;
        outcome = new OriginalsOutcome(stake, payout, roundId, capped);
        return true;
    }

    public void Clear()
    {
        roundId = string.Empty;
        count = 0;
        optionCount = 0;
        phase = OriginalsRules.PhaseLive;
        stake = 0;
        payout = 0;
        capped = false;
        multiplier = 0;
        flip = 1f;
        settlePending = false;
        landedPending = false;
        bustPending = false;
    }

    private void Absorb(CasinoHiLoDto dto)
    {
        roundId = dto.RoundId;
        phase = dto.Phase;
        stake = dto.Stake;
        payout = dto.Payout;
        capped = dto.Capped;
        multiplier = dto.MultiplierTenThousandths;
        var dealt = dto.Cards!;
        var played = dto.Moves ?? Array.Empty<string>();
        count = dealt.Length;
        Array.Copy(dealt, cards, count);
        var chain = 1d;
        chainHundredths[0] = 100;
        for (var index = 0; index < played.Length; index++)
        {
            var call = OriginalsRules.TryParseCall(played[index], out var parsed) ? parsed : HiLoCall.Skip;
            moves[index] = call;
            var shown = OriginalsRules.HiLoRank(cards[index]);
            var next = OriginalsRules.HiLoRank(cards[index + 1]);
            if (call == HiLoCall.Skip)
            {
                steps[index] = HiLoStep.Skipped;
            }
            else if (OriginalsRules.CallWins(call, shown, next))
            {
                steps[index] = HiLoStep.Won;
                chain *= OriginalsRules.CallFactor(call, shown);
            }
            else
            {
                steps[index] = HiLoStep.Lost;
            }

            chainHundredths[index + 1] = (long)Math.Floor(Math.Min(chain, OriginalsRules.HiLoChainCap) * 100d);
        }

        var offered = dto.Calls ?? Array.Empty<CasinoHiLoCallDto>();
        optionCount = 0;
        for (var index = 0; index < offered.Length && optionCount < MaxOptions; index++)
        {
            if (!OriginalsRules.TryParseCall(offered[index].Call, out var call))
            {
                continue;
            }

            options[optionCount] = new HiLoOption(call, offered[index].ChanceBasisPoints,
                offered[index].MultiplierTenThousandths);
            optionCount++;
        }
    }
}
