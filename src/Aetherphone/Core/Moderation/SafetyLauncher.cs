namespace Aetherphone.Core.Moderation;

internal sealed class SafetyLauncher
{
    private volatile bool pending;

    public void Request()
    {
        pending = true;
    }

    public bool TryConsume()
    {
        if (!pending)
        {
            return false;
        }

        pending = false;
        return true;
    }
}
