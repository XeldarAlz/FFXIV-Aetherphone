namespace Aetherphone.Apps.Games.Framework;

internal struct RollingValue
{
    private const float FollowSpeed = 10f;
    private const float PopKick = 0.16f;
    private const float PopLimit = 1.30f;
    private const float PopSettleSpeed = 5.5f;

    private float shown;
    private float pop;
    private int target;
    private bool initialized;

    public readonly int Display => (int)MathF.Round(shown);
    public readonly float PopScale => 1f + pop;

    public void Snap(int value)
    {
        shown = value;
        target = value;
        pop = 0f;
        initialized = true;
    }

    public bool Update(int value, float deltaSeconds)
    {
        if (!initialized)
        {
            Snap(value);
            return false;
        }

        var changed = value != target;
        if (changed)
        {
            target = value;
            pop = MathF.Min(PopLimit - 1f, pop + PopKick);
        }

        var difference = target - shown;
        if (MathF.Abs(difference) < 0.5f)
        {
            shown = target;
        }
        else
        {
            shown += difference * MathF.Min(1f, deltaSeconds * FollowSpeed);
        }

        pop = MathF.Max(0f, pop - deltaSeconds * PopSettleSpeed * (pop + 0.12f));
        return changed;
    }
}
