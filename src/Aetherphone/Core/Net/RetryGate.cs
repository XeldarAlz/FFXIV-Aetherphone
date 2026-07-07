namespace Aetherphone.Core.Net;

internal sealed class RetryGate
{
    private readonly TimeSpan cooldown;
    private DateTime lastAttemptUtc = DateTime.MinValue;

    public RetryGate(TimeSpan cooldown)
    {
        this.cooldown = cooldown;
    }

    public bool TryPass()
    {
        var now = DateTime.UtcNow;
        if (now - lastAttemptUtc < cooldown)
        {
            return false;
        }

        lastAttemptUtc = now;
        return true;
    }

    public void Reset() => lastAttemptUtc = DateTime.MinValue;
}
