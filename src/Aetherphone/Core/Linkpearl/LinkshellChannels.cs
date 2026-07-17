using Dalamud.Game.Text;

namespace Aetherphone.Core.Linkpearl;

internal static class LinkshellChannels
{
    public static bool TryResolve(XivChatType kind, out LinkshellChannel channel)
    {
        switch (kind)
        {
            case XivChatType.Ls1:
            case XivChatType.Ls2:
            case XivChatType.Ls3:
            case XivChatType.Ls4:
            case XivChatType.Ls5:
            case XivChatType.Ls6:
            case XivChatType.Ls7:
            case XivChatType.Ls8:
                channel = new LinkshellChannel(LinkshellKind.Linkshell, kind - XivChatType.Ls1);
                return true;
            case XivChatType.CrossLinkShell1:
                channel = new LinkshellChannel(LinkshellKind.CrossWorldLinkshell, 0);
                return true;
            case XivChatType.CrossLinkShell2:
            case XivChatType.CrossLinkShell3:
            case XivChatType.CrossLinkShell4:
            case XivChatType.CrossLinkShell5:
            case XivChatType.CrossLinkShell6:
            case XivChatType.CrossLinkShell7:
            case XivChatType.CrossLinkShell8:
                channel = new LinkshellChannel(LinkshellKind.CrossWorldLinkshell,
                    kind - XivChatType.CrossLinkShell2 + 1);
                return true;
            default:
                channel = default;
                return false;
        }
    }
}
