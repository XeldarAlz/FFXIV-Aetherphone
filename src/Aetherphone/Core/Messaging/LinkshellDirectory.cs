using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Messaging;

internal static unsafe class LinkshellDirectory
{
    private const int SlotCount = 8;

    public static string Name(LinkshellChannel channel)
    {
        return channel.IsCrossWorld ? CrossWorldName(channel.Slot) : LocalName(channel.Slot);
    }

    public static void Collect(List<LinkshellEntry> into)
    {
        into.Clear();

        var local = InfoProxyLinkshell.Instance();
        if (local is not null)
        {
            for (var slot = 0; slot < SlotCount; slot++)
            {
                var entry = local->GetLinkshellInfo((uint)slot);
                if (entry is null || entry->Id == 0)
                {
                    continue;
                }

                var name = local->GetLinkshellName(entry->Id).ToString() ?? string.Empty;
                into.Add(new LinkshellEntry(new LinkshellChannel(LinkshellKind.Linkshell, slot), name));
            }
        }

        var crossWorld = InfoProxyCrossWorldLinkshell.Instance();
        if (crossWorld is not null)
        {
            for (var slot = 0; slot < SlotCount; slot++)
            {
                var name = crossWorld->GetCrossworldLinkshellName((uint)slot);
                if (name is null || name->Length == 0)
                {
                    continue;
                }

                into.Add(new LinkshellEntry(new LinkshellChannel(LinkshellKind.CrossWorldLinkshell, slot), name->ToString()));
            }
        }
    }

    private static string LocalName(int slot)
    {
        var proxy = InfoProxyLinkshell.Instance();
        if (proxy is null)
        {
            return string.Empty;
        }

        var entry = proxy->GetLinkshellInfo((uint)slot);
        if (entry is null || entry->Id == 0)
        {
            return string.Empty;
        }

        return proxy->GetLinkshellName(entry->Id).ToString() ?? string.Empty;
    }

    private static string CrossWorldName(int slot)
    {
        var proxy = InfoProxyCrossWorldLinkshell.Instance();
        if (proxy is null)
        {
            return string.Empty;
        }

        var name = proxy->GetCrossworldLinkshellName((uint)slot);
        return name is null ? string.Empty : name->ToString();
    }
}
