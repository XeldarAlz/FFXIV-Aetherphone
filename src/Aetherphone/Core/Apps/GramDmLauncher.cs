namespace Aetherphone.Core.Apps;

internal sealed class GramDmLauncher
{
    private string? pendingUserId;
    private string? pendingDraft;

    public void Request(string userId, string? draft = null)
    {
        pendingUserId = userId;
        pendingDraft = draft;
    }

    public bool TryConsume(out string userId, out string? draft)
    {
        if (pendingUserId is null)
        {
            userId = string.Empty;
            draft = null;
            return false;
        }

        userId = pendingUserId;
        draft = pendingDraft;
        pendingUserId = null;
        pendingDraft = null;
        return true;
    }
}
