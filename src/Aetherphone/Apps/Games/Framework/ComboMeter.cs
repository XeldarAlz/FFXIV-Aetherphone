namespace Aetherphone.Apps.Games.Framework;

internal struct ComboMeter
{
    public const float DefaultWindowSeconds = 2f;
    public const int MaxMultiplier = 8;
    private const int HeatHits = 20;
    private const float HeatDecayPerSecond = 1.5f;

    public float WindowSeconds;
    private float remaining;

    public int Count { get; private set; }

    public float Heat { get; private set; }

    public int Multiplier { get; private set; }

    public ComboMeter(float windowSeconds)
    {
        WindowSeconds = windowSeconds;
        remaining = 0f;
        Count = 0;
        Heat = 0f;
        Multiplier = 1;
    }

    public static ComboMeter Create() => new(DefaultWindowSeconds);

    public static ComboMeter Untimed() => new(0f);

    public readonly bool Timed => WindowSeconds > 0f;

    public readonly float WindowFraction =>
        WindowSeconds <= 0f ? 0f : Math.Clamp(remaining / WindowSeconds, 0f, 1f);

    public readonly bool Active => Count > 0;

    public int Hit(bool resetWindow = true)
    {
        Count++;
        if (resetWindow || Count == 1)
        {
            remaining = WindowSeconds;
        }

        Multiplier = MultiplierFor(Count);
        Heat = Math.Clamp(Count / (float)HeatHits, 0f, 1f);
        return Multiplier;
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var decaySeconds = deltaSeconds;
        if (Count > 0)
        {
            if (!Timed)
            {
                return;
            }

            remaining -= deltaSeconds;
            if (remaining > 0f)
            {
                return;
            }

            decaySeconds = -remaining;
            Count = 0;
            Multiplier = 1;
            remaining = 0f;
        }

        Heat = MathF.Max(0f, Heat - HeatDecayPerSecond * decaySeconds);
    }

    public void Reset()
    {
        Count = 0;
        Heat = 0f;
        Multiplier = 1;
        remaining = 0f;
    }

    public static int MultiplierFor(int hits)
    {
        if (hits >= 20)
        {
            return 8;
        }

        if (hits >= 12)
        {
            return 5;
        }

        if (hits >= 8)
        {
            return 3;
        }

        if (hits >= 4)
        {
            return 2;
        }

        return 1;
    }
}
