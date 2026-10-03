namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapBlitz
{
    public const float StartSeconds = 60f;
    public const float BonusSeconds = 5f;
    public const float FrostDuration = 8f;
    public const float FirstRequirement = 24f;
    public const float RequirementStep = 6f;
    public const float ChargeNeeded = 15f;
    public const int PowerCount = 4;
    private static readonly int[] PowerColors = { 3, 1, 4, 5 };
    private readonly float[] charges = new float[PowerCount];

    public GemSwapBlitz()
    {
        Reset();
    }

    public float TimeLeft { get; private set; }
    public float BarFill { get; private set; }
    public float BarRequirement { get; private set; }
    public float FrostLeft { get; private set; }
    public bool Ended { get; private set; }
    public int BonusCount { get; private set; }
    public bool Frozen => FrostLeft > 0f;
    public float BarFraction => MathF.Min(1f, BarFill / BarRequirement);

    public static int PowerColor(int power) => PowerColors[power];

    public void Reset()
    {
        TimeLeft = StartSeconds;
        BarFill = 0f;
        BarRequirement = FirstRequirement;
        FrostLeft = 0f;
        Ended = false;
        BonusCount = 0;
        Array.Clear(charges, 0, PowerCount);
    }

    public bool Tick(float deltaSeconds)
    {
        if (Ended || deltaSeconds <= 0f)
        {
            return false;
        }

        if (FrostLeft > 0f)
        {
            FrostLeft = MathF.Max(0f, FrostLeft - deltaSeconds);
            return false;
        }

        TimeLeft -= deltaSeconds;
        if (TimeLeft > 0f)
        {
            return false;
        }

        TimeLeft = 0f;
        Ended = true;
        return true;
    }

    public static float FillFor(int cleared, int chain)
    {
        var cascade = 1f + 0.5f * (Math.Max(1, chain) - 1);
        var size = Math.Max(0, cleared - 3);
        return cleared * cascade + size;
    }

    public bool AddClear(int cleared, int chain)
    {
        if (Ended || cleared <= 0)
        {
            return false;
        }

        BarFill += FillFor(cleared, chain);
        if (BarFill < BarRequirement)
        {
            return false;
        }

        BarFill = 0f;
        BarRequirement += RequirementStep;
        TimeLeft += BonusSeconds;
        BonusCount++;
        return true;
    }

    public float Charge(int power) => MathF.Min(1f, charges[power] / ChargeNeeded);

    public bool IsReady(int power) => charges[power] >= ChargeNeeded;

    public bool AddCharge(int power, int gems)
    {
        if (Ended || gems <= 0 || IsReady(power))
        {
            return false;
        }

        charges[power] = MathF.Min(ChargeNeeded, charges[power] + gems);
        return IsReady(power);
    }

    public bool TryFire(int power)
    {
        if (Ended || !IsReady(power))
        {
            return false;
        }

        charges[power] = 0f;
        if (power == (int)GemPower.Frost)
        {
            FrostLeft = FrostDuration;
        }

        return true;
    }
}
