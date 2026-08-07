namespace Aetherphone.Core.Shortcuts;

internal enum ShortcutHoldOutcome : byte
{
    Ready,
    Waiting,
    Expired,
}

internal struct ShortcutHold
{
    public const float MaxSeconds = 15f;

    private float waited;

    public readonly bool IsWaiting => waited > 0f;

    public void Clear() => waited = 0f;

    public ShortcutHoldOutcome Advance(bool blocked, float deltaSeconds)
    {
        if (!blocked)
        {
            waited = 0f;
            return ShortcutHoldOutcome.Ready;
        }

        waited += MathF.Max(deltaSeconds, 0f);
        if (waited < MaxSeconds)
        {
            return ShortcutHoldOutcome.Waiting;
        }

        waited = 0f;
        return ShortcutHoldOutcome.Expired;
    }
}
