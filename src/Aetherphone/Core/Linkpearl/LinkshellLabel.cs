using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Linkpearl;

internal static class LinkshellLabel
{
    public static string Of(LinkshellChannel channel, string name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        return channel.IsCrossWorld
            ? Loc.T(L.Messages.CrossWorldLinkshell, channel.Slot + 1)
            : Loc.T(L.Messages.Linkshell, channel.Slot + 1);
    }
}
