using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.GameChat;

internal static unsafe class LinkshellNames
{
    private const long RefreshIntervalMilliseconds = 1000;

    private static readonly string[] Local = NewSlots();
    private static readonly string[] CrossWorld = NewSlots();
    private static long lastReadMilliseconds = -RefreshIntervalMilliseconds;

    public static string For(GameChannel channel)
    {
        if (!channel.IsSlotted)
        {
            return string.Empty;
        }

        Refresh();
        var slots = channel.Category == ChannelCategory.CrossWorld ? CrossWorld : Local;
        return slots[channel.Slot];
    }

    public static string Label(GameChannel channel)
    {
        var real = For(channel);
        return real.Length > 0 ? real : GameChannels.DisplayName(channel);
    }

    public static bool Joined(GameChannel channel) => !channel.IsSlotted || For(channel).Length > 0;

    private static void Refresh()
    {
        if (!GameMemory.Attached)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - lastReadMilliseconds < RefreshIntervalMilliseconds)
        {
            return;
        }

        lastReadMilliseconds = now;
        ReadLocal();
        ReadCrossWorld();
    }

    private static void ReadLocal()
    {
        var proxy = InfoProxyLinkshell.Instance();
        for (var slot = 0; slot < GameChannels.LinkshellSlots; slot++)
        {
            if (proxy is null)
            {
                Local[slot] = string.Empty;
                continue;
            }

            var entry = proxy->GetLinkshellInfo((uint)slot);
            if (entry is null || entry->Id == 0)
            {
                Local[slot] = string.Empty;
                continue;
            }

            Store(Local, slot, proxy->GetLinkshellName(entry->Id).ToString());
        }
    }

    private static void ReadCrossWorld()
    {
        var proxy = InfoProxyCrossWorldLinkshell.Instance();
        for (var slot = 0; slot < GameChannels.LinkshellSlots; slot++)
        {
            if (proxy is null)
            {
                CrossWorld[slot] = string.Empty;
                continue;
            }

            var name = proxy->GetCrossworldLinkshellName((uint)slot);
            if (name is null || name->Length == 0)
            {
                CrossWorld[slot] = string.Empty;
                continue;
            }

            Store(CrossWorld, slot, name->ToString());
        }
    }

    private static void Store(string[] slots, int slot, string? value)
    {
        var next = value ?? string.Empty;
        if (!string.Equals(slots[slot], next, StringComparison.Ordinal))
        {
            slots[slot] = next;
        }
    }

    private static string[] NewSlots()
    {
        var slots = new string[GameChannels.LinkshellSlots];
        for (var index = 0; index < slots.Length; index++)
        {
            slots[index] = string.Empty;
        }

        return slots;
    }
}
