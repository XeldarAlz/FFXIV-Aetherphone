namespace Aetherphone.Core.Apps;

internal sealed class DmLauncher
{
    private string? pendingUserId;
    private string? pendingConversationId;
    private bool pendingCalls;

    public void RequestUser(string userId)
    {
        pendingUserId = userId;
        pendingConversationId = null;
    }

    public void RequestCalls()
    {
        pendingCalls = true;
        pendingUserId = null;
        pendingConversationId = null;
    }

    public bool TryConsumeCalls()
    {
        if (!pendingCalls)
        {
            return false;
        }

        pendingCalls = false;
        return true;
    }

    public void RequestConversation(string conversationId)
    {
        pendingConversationId = conversationId;
        pendingUserId = null;
    }

    public bool TryConsumeUser(out string userId)
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

    public bool TryConsumeConversation(out string conversationId)
    {
        if (pendingConversationId is null)
        {
            conversationId = string.Empty;
            return false;
        }

        conversationId = pendingConversationId;
        pendingConversationId = null;
        return true;
    }
}
