namespace Aetherphone.Core.Apps;

internal sealed class SocialDmLauncher
{
    private string? pendingAppId;
    private string? pendingUserId;
    private string? pendingDraft;

    public void Request(string appId, string userId, string? draft = null)
    {
        pendingAppId = appId;
        pendingUserId = userId;
        pendingDraft = draft;
    }

    public bool TryConsume(string appId, out string userId, out string? draft)
    {
        if (pendingUserId is null || pendingAppId != appId)
        {
            userId = string.Empty;
            draft = null;
            return false;
        }

        userId = pendingUserId;
        draft = pendingDraft;
        pendingAppId = null;
        pendingUserId = null;
        pendingDraft = null;
        return true;
    }
}
