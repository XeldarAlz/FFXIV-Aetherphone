namespace Aetherphone.Core.Messaging;

internal sealed class VelvetLauncher
{
    private string? pendingUserId;

    public void Request(string userId)
    {
        pendingUserId = userId;
    }

    public bool TryConsume(out string userId)
    {
        if (pendingUserId is null)
        {
            userId = string.Empty;
            return false;
        }

        userId = pendingUserId;
        pendingUserId = null;
        return true;
    }
}
