using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class DiceRollPlayback
{
    public const float SlideSeconds = 0.7f;
    public const int TickSpan = 500;

    private float from = OriginalsRules.DiceScale * 0.5f;
    private float to = OriginalsRules.DiceScale * 0.5f;
    private float elapsed = SlideSeconds;
    private bool rolling;
    private bool settlePending;
    private bool hasRoll;
    private int lastTickBand;
    private OriginalsOutcome outcome;
    private int target;
    private bool over;
    private bool won;

    public bool Rolling => rolling;

    public bool HasRoll => hasRoll;

    public int Roll => (int)to;

    public bool Won => won;

    public int Target => target;

    public bool Over => over;

    public float Marker => rolling ? from + (to - from) * Easing.EaseOutCubic(Progress) : to;

    public float Progress => Math.Clamp(elapsed / SlideSeconds, 0f, 1f);

    public static bool IsValid(CasinoDiceDto dto) =>
        dto.Granted && dto.RoundId.Length > 0 && dto.Roll >= 0 && dto.Roll <= OriginalsRules.DiceScale;

    public bool Begin(CasinoDiceDto dto, bool instant)
    {
        if (!IsValid(dto))
        {
            return false;
        }

        from = Marker;
        to = dto.Roll;
        target = dto.Target;
        over = dto.Over;
        won = dto.Won;
        hasRoll = true;
        outcome = new OriginalsOutcome(dto.Stake, dto.Payout, dto.RoundId);
        settlePending = true;
        lastTickBand = Band(from);
        elapsed = 0f;
        rolling = true;
        if (instant)
        {
            Snap();
        }

        return true;
    }

    public void Advance(float deltaSeconds)
    {
        if (!rolling)
        {
            return;
        }

        elapsed += deltaSeconds;
        if (elapsed >= SlideSeconds)
        {
            Snap();
        }
    }

    public void Snap()
    {
        elapsed = SlideSeconds;
        rolling = false;
        lastTickBand = Band(to);
    }

    public bool TakeTick()
    {
        var band = Band(Marker);
        if (band == lastTickBand)
        {
            return false;
        }

        lastTickBand = band;
        return true;
    }

    public bool TakeSettled(out OriginalsOutcome settled)
    {
        settled = default;
        if (!settlePending || rolling)
        {
            return false;
        }

        settlePending = false;
        settled = outcome;
        return true;
    }

    public void Clear()
    {
        from = OriginalsRules.DiceScale * 0.5f;
        to = from;
        elapsed = SlideSeconds;
        rolling = false;
        settlePending = false;
        hasRoll = false;
        won = false;
    }

    private static int Band(float marker) => (int)(marker / TickSpan);
}
