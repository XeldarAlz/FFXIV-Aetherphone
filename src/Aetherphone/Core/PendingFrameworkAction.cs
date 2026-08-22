using Dalamud.Plugin.Services;

namespace Aetherphone.Core;

internal sealed class PendingFrameworkAction
{
    private readonly IFramework framework;
    private readonly TimeSpan timeout;
    private readonly TimeSpan retryDelay;
    private readonly int maxAttempts;
    private readonly Func<bool> ready;
    private readonly Func<bool> attempt;

    private bool armed;
    private DateTime expiresAtUtc;
    private DateTime nextAttemptUtc;
    private int attemptsRemaining;

    public PendingFrameworkAction(IFramework framework, TimeSpan timeout, TimeSpan retryDelay, Func<bool> ready,
        Func<bool> attempt, int maxAttempts = int.MaxValue)
    {
        this.framework = framework;
        this.timeout = timeout;
        this.retryDelay = retryDelay;
        this.ready = ready;
        this.attempt = attempt;
        this.maxAttempts = maxAttempts;
    }

    public void Arm()
    {
        expiresAtUtc = DateTime.UtcNow + timeout;
        nextAttemptUtc = DateTime.MinValue;
        attemptsRemaining = maxAttempts;
        if (armed)
        {
            return;
        }

        armed = true;
        framework.Update += Tick;
    }

    public void Disarm()
    {
        if (!armed)
        {
            return;
        }

        armed = false;
        framework.Update -= Tick;
    }

    private void Tick(IFramework owner)
    {
        var now = DateTime.UtcNow;
        if (now >= expiresAtUtc)
        {
            Disarm();
            return;
        }

        if (!ready())
        {
            return;
        }

        if (now < nextAttemptUtc)
        {
            return;
        }

        if (attempt())
        {
            Disarm();
            return;
        }

        attemptsRemaining--;
        if (attemptsRemaining <= 0)
        {
            Disarm();
            return;
        }

        nextAttemptUtc = now + retryDelay;
    }
}
