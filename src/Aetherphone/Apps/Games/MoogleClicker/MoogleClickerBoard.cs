using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.MoogleClicker;

namespace Aetherphone.Apps.Games.MoogleClicker;

internal readonly struct TapOutcome
{
    public readonly int Multiplier;
    public readonly bool Critical;
    public readonly bool TierUp;

    public TapOutcome(int multiplier, bool critical, bool tierUp)
    {
        Multiplier = multiplier;
        Critical = critical;
        TierUp = tierUp;
    }

    public double KupoMultiplier => Multiplier * (Critical ? MoogleClickerBoard.CriticalMultiplier : 1d);
}

internal sealed class MoogleClickerBoard
{
    public const float ComboWindowSeconds = 0.6f;
    public const double CriticalMultiplier = 7d;
    public const float MinionTravelSeconds = 9f;
    public const float FirstMinionMinSeconds = 20f;
    public const float FirstMinionMaxSeconds = 45f;
    public const float MinionMinSeconds = 60f;
    public const float MinionMaxSeconds = 150f;
    public const float MinionEdge = 0.08f;
    public const float MinionTop = 0.16f;
    public const float MinionBottom = 0.62f;
    private const float MinionSway = 0.06f;
    private const float MinionWaves = 2.5f;
    private const float FrenzyOdds = 0.45f;
    private const float LumpOdds = 0.85f;

    private GameRandom random;
    private ComboMeter combo = new(ComboWindowSeconds);
    private float minionCountdown;
    private float minionProgress;
    private float minionHeight;
    private bool minionFromLeft;

    public ComboMeter Combo => combo;

    public bool MinionActive { get; private set; }

    public float MinionProgress => minionProgress;

    public float MinionCountdown => minionCountdown;

    public bool MinionFromLeft => minionFromLeft;

    public int Criticals { get; private set; }

    public int BestCombo { get; private set; }

    public Vector2 MinionPoint
    {
        get
        {
            var travel = minionFromLeft ? minionProgress : 1f - minionProgress;
            var x = -MinionEdge + (1f + MinionEdge * 2f) * travel;
            var y = minionHeight + MathF.Sin(minionProgress * MathF.PI * 2f * MinionWaves) * MinionSway;
            return new Vector2(x, y);
        }
    }

    public static float CriticalChance(int multiplier) => multiplier switch
    {
        >= 8 => 0.25f,
        >= 5 => 0.18f,
        >= 3 => 0.12f,
        >= 2 => 0.06f,
        _ => 0f,
    };

    public void Reset(GameRandom seeded)
    {
        random = seeded;
        combo.Reset();
        MinionActive = false;
        minionProgress = 0f;
        minionHeight = 0f;
        minionFromLeft = true;
        Criticals = 0;
        BestCombo = 0;
        minionCountdown = random.Range(FirstMinionMinSeconds, FirstMinionMaxSeconds);
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        combo.Update(deltaSeconds);
        if (MinionActive)
        {
            minionProgress += deltaSeconds / MinionTravelSeconds;
            if (minionProgress >= 1f)
            {
                MinionActive = false;
                Schedule();
            }

            return;
        }

        minionCountdown -= deltaSeconds;
        if (minionCountdown <= 0f)
        {
            Spawn();
        }
    }

    public TapOutcome Tap()
    {
        var before = combo.Multiplier;
        var multiplier = combo.Hit();
        BestCombo = Math.Max(BestCombo, combo.Count);
        var critical = random.Chance(CriticalChance(multiplier));
        if (critical)
        {
            Criticals++;
        }

        return new TapOutcome(multiplier, critical, multiplier > before);
    }

    public KupoReward Catch()
    {
        if (!MinionActive)
        {
            return KupoReward.None;
        }

        MinionActive = false;
        Schedule();
        var roll = random.NextFloat();
        if (roll < FrenzyOdds)
        {
            return KupoReward.Frenzy;
        }

        return roll < LumpOdds ? KupoReward.Lump : KupoReward.TapFrenzy;
    }

    private void Spawn()
    {
        MinionActive = true;
        minionProgress = 0f;
        minionFromLeft = random.Chance(0.5f);
        minionHeight = random.Range(MinionTop + MinionSway, MinionBottom - MinionSway);
    }

    private void Schedule()
    {
        minionProgress = 0f;
        minionCountdown = random.Range(MinionMinSeconds, MinionMaxSeconds);
    }
}
