namespace Aetherphone.Apps.Games.Framework.World;

internal struct DripEconomy
{
    public float RatePerSecond;
    public float Cap;
    private float amount;

    public DripEconomy(float amount, float ratePerSecond, float cap)
    {
        Cap = MathF.Max(0f, cap);
        RatePerSecond = ratePerSecond;
        this.amount = Math.Clamp(amount, 0f, Cap);
    }

    public readonly float Amount => amount;

    public readonly int Whole => (int)amount;

    public readonly bool Full => amount >= Cap;

    public readonly bool CanAfford(float cost) => cost >= 0f && amount >= cost;

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || RatePerSecond <= 0f || amount >= Cap)
        {
            return;
        }

        amount = MathF.Min(Cap, amount + RatePerSecond * deltaSeconds);
    }

    public bool TrySpend(float cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        amount -= cost;
        return true;
    }

    public float Add(float pickup)
    {
        if (pickup <= 0f || amount >= Cap)
        {
            return 0f;
        }

        var before = amount;
        amount = MathF.Min(Cap, amount + pickup);
        return amount - before;
    }
}
