namespace Aetherphone.Core.Messaging;

internal sealed class MessageLauncher
{
    private string? pendingDisplay;
    private string? pendingTarget;
    private string? pendingLinkshellName;
    private LinkshellChannel? pendingLinkshell;

    public void Request(string display, string sendTarget)
    {
        pendingDisplay = display;
        pendingTarget = sendTarget;
        pendingLinkshell = null;
        pendingLinkshellName = null;
    }

    public void RequestLinkshell(LinkshellChannel channel, string name)
    {
        pendingLinkshell = channel;
        pendingLinkshellName = name;
        pendingDisplay = null;
        pendingTarget = null;
    }

    public bool TryConsume(out string display, out string sendTarget)
    {
        if (pendingTarget is null)
        {
            display = string.Empty;
            sendTarget = string.Empty;
            return false;
        }

        display = pendingDisplay!;
        sendTarget = pendingTarget;
        pendingDisplay = null;
        pendingTarget = null;
        return true;
    }

    public bool TryConsumeLinkshell(out LinkshellChannel channel, out string name)
    {
        if (pendingLinkshell is not { } pending)
        {
            channel = default;
            name = string.Empty;
            return false;
        }

        channel = pending;
        name = pendingLinkshellName ?? string.Empty;
        pendingLinkshell = null;
        pendingLinkshellName = null;
        return true;
    }
}
