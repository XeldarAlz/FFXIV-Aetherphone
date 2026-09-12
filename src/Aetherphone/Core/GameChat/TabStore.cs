using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;

namespace Aetherphone.Core.GameChat;

internal enum TabPreset : byte
{
    FreeCompany,
    Linkshells,
    Local,
    Party,
}

internal sealed class TabStore
{
    public const int MaxTabs = 12;
    public const int MaxPinned = 6;

    private readonly Configuration configuration;
    private readonly HashSet<string> legacyMuted = new(StringComparer.Ordinal);

    public TabStore(Configuration configuration, CharacterWatch characterWatch)
    {
        this.configuration = configuration;
        characterWatch.Changed += ReadLegacyMutes;
    }

    public IReadOnlyList<ChatTab> Tabs => configuration.LinkpearlTabs;

    public event Action? Changed;

    public bool CanAdd => configuration.LinkpearlTabs.Count < MaxTabs;

    public ChatTab? Find(string id)
    {
        var tabs = configuration.LinkpearlTabs;
        for (var index = 0; index < tabs.Count; index++)
        {
            if (string.Equals(tabs[index].Id, id, StringComparison.Ordinal))
            {
                return tabs[index];
            }
        }

        return null;
    }

    public ChatTab Create(string name, IReadOnlyList<string> channels)
    {
        var tab = new ChatTab
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Channels = new List<string>(channels),
            Alerts = AlertPolicy.Mentions,
            Density = configuration.LinkpearlDefaultDensity == (int)ChatDensity.Bubbles
                ? ChatDensity.Bubbles
                : ChatDensity.Log,
        };
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            if (legacyMuted.Contains(tab.Channels[index]))
            {
                tab.MutedChannels.Add(tab.Channels[index]);
            }
        }

        tab.Tint = DefaultTint(tab.Channels);
        ApplyDefaults(tab);
        configuration.LinkpearlTabs.Add(tab);
        Commit();
        return tab;
    }

    public void Update(ChatTab tab)
    {
        ApplyDefaults(tab);
        Commit();
    }

    public void Delete(ChatTab tab)
    {
        if (!configuration.LinkpearlTabs.Remove(tab))
        {
            return;
        }

        Commit();
    }

    public void Move(ChatTab tab, int target)
    {
        var tabs = configuration.LinkpearlTabs;
        var current = tabs.IndexOf(tab);
        if (current < 0)
        {
            return;
        }

        var clamped = Math.Clamp(target, 0, tabs.Count - 1);
        if (clamped == current)
        {
            return;
        }

        tabs.RemoveAt(current);
        tabs.Insert(clamped, tab);
        Commit();
    }

    public bool TogglePin(ChatTab tab)
    {
        if (!tab.Pinned && PinnedCount() >= MaxPinned)
        {
            return false;
        }

        tab.Pinned = !tab.Pinned;
        Commit();
        return true;
    }

    public int PinnedCount()
    {
        var tabs = configuration.LinkpearlTabs;
        var count = 0;
        for (var index = 0; index < tabs.Count; index++)
        {
            if (tabs[index].Pinned)
            {
                count++;
            }
        }

        return count;
    }

    public ChatTab? FirstTabWith(string channelKey)
    {
        var tabs = configuration.LinkpearlTabs;
        for (var index = 0; index < tabs.Count; index++)
        {
            if (tabs[index].Includes(channelKey))
            {
                return tabs[index];
            }
        }

        return null;
    }

    public ChatTab AddPreset(TabPreset preset)
    {
        var channels = new List<string>(GameChannels.LinkshellSlots * 2);
        switch (preset)
        {
            case TabPreset.FreeCompany:
                channels.Add(GameChannels.FreeCompanyKey);
                channels.Add(GameChannels.NoviceKey);
                break;
            case TabPreset.Linkshells:
                CollectCategory(channels, ChannelCategory.Linkshell);
                CollectCategory(channels, ChannelCategory.CrossWorld);
                break;
            case TabPreset.Local:
                CollectCategory(channels, ChannelCategory.Local);
                break;
            default:
                channels.Add(GameChannels.PartyKey);
                channels.Add(GameChannels.AllianceKey);
                break;
        }

        var tab = Create(Loc.T(PresetLabel(preset)), channels);
        if (preset != TabPreset.Local)
        {
            return tab;
        }

        tab.Alerts = AlertPolicy.Off;
        Commit();
        return tab;
    }

    public static LocString PresetLabel(TabPreset preset) => preset switch
    {
        TabPreset.FreeCompany => L.Linkpearl.ChannelFreeCompany,
        TabPreset.Linkshells => L.Linkpearl.CategoryLinkshell,
        TabPreset.Local => L.Linkpearl.CategoryLocal,
        _ => L.Linkpearl.ChannelParty,
    };

    private static int DefaultTint(List<string> channels)
    {
        if (channels.Count == 0 || !GameChannels.TryByKey(channels[0], out var channel))
        {
            return 0;
        }

        var palette = ChannelTints.TabPalette;
        for (var index = 0; index < palette.Length; index++)
        {
            if (palette[index] == channel.Tint)
            {
                return index;
            }
        }

        return 0;
    }

    private static void CollectCategory(List<string> into, ChannelCategory category)
    {
        var all = GameChannels.All;
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Category == category)
            {
                into.Add(all[index].Key);
            }
        }
    }

    private static void ApplyDefaults(ChatTab tab)
    {
        for (var index = tab.Channels.Count - 1; index >= 0; index--)
        {
            if (!GameChannels.TryByKey(tab.Channels[index], out var channel) ||
                channel.Category == ChannelCategory.Direct)
            {
                tab.Channels.RemoveAt(index);
            }
        }

        for (var index = tab.MutedChannels.Count - 1; index >= 0; index--)
        {
            if (!tab.Channels.Contains(tab.MutedChannels[index]))
            {
                tab.MutedChannels.RemoveAt(index);
            }
        }

        if (string.Equals(tab.SendChannel, GameChannels.FollowGameKey, StringComparison.Ordinal))
        {
            return;
        }

        if (tab.SendChannel.Length > 0 && tab.Channels.Contains(tab.SendChannel) &&
            GameChannels.TryByKey(tab.SendChannel, out var current) && current.CanSend)
        {
            return;
        }

        tab.SendChannel = FirstSendable(tab.Channels);
    }

    private static string FirstSendable(List<string> channels)
    {
        for (var index = 0; index < channels.Count; index++)
        {
            if (GameChannels.TryByKey(channels[index], out var channel) && channel.CanSend && !channel.NeedsTarget)
            {
                return channel.Key;
            }
        }

        return string.Empty;
    }

    private void ReadLegacyMutes(ulong contentId)
    {
        legacyMuted.Clear();
        if (contentId == 0 || !configuration.MutedLinkshellsByCharacter.TryGetValue(contentId, out var legacy))
        {
            return;
        }

        for (var index = 0; index < legacy.Count; index++)
        {
            if (TryTranslateLegacyKey(legacy[index], out var channelKey))
            {
                legacyMuted.Add(channelKey);
            }
        }
    }

    private static bool TryTranslateLegacyKey(string legacy, out string channelKey)
    {
        if (LegacySlot(legacy, "cwls:", out var crossWorldSlot))
        {
            channelKey = $"cwls{crossWorldSlot + 1}";
            return true;
        }

        if (LegacySlot(legacy, "ls:", out var slot))
        {
            channelKey = $"ls{slot + 1}";
            return true;
        }

        channelKey = string.Empty;
        return false;
    }

    private static bool LegacySlot(string legacy, string prefix, out int slot)
    {
        slot = 0;
        if (!legacy.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(legacy.AsSpan(prefix.Length), out slot) && slot >= 0 &&
               slot < GameChannels.LinkshellSlots;
    }

    private void Commit()
    {
        configuration.Save();
        Changed?.Invoke();
    }
}
