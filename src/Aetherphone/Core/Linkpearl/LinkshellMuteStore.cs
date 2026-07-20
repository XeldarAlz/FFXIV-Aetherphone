using Aetherphone.Core.Game;

namespace Aetherphone.Core.Linkpearl;

internal sealed class LinkshellMuteStore
{
    private readonly Configuration configuration;
    private HashSet<string> muted = new(StringComparer.Ordinal);
    private ulong contentId;
    public event Action? Changed;

    public LinkshellMuteStore(Configuration configuration, CharacterWatch characterWatch)
    {
        this.configuration = configuration;
        characterWatch.Changed += OnCharacterChanged;
    }

    private void OnCharacterChanged(ulong id)
    {
        contentId = id;
        if (id != 0 && !configuration.LinkshellMutesPerCharacterMigrated)
        {
            if (configuration.MutedLinkshells.Count > 0)
            {
                configuration.MutedLinkshellsByCharacter[id] = new List<string>(configuration.MutedLinkshells);
                configuration.MutedLinkshells = new List<string>();
            }

            configuration.LinkshellMutesPerCharacterMigrated = true;
            configuration.Save();
        }

        muted = configuration.MutedLinkshellsByCharacter.TryGetValue(id, out var list)
            ? new HashSet<string>(list, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        Changed?.Invoke();
    }

    public bool IsMuted(LinkshellChannel channel) => muted.Contains(channel.Key);

    public bool Toggle(LinkshellChannel channel)
    {
        var next = !muted.Contains(channel.Key);
        SetMuted(channel, next);
        return next;
    }

    public void SetMuted(LinkshellChannel channel, bool value)
    {
        var changed = value ? muted.Add(channel.Key) : muted.Remove(channel.Key);
        if (!changed)
        {
            return;
        }

        if (contentId != 0)
        {
            configuration.MutedLinkshellsByCharacter[contentId] = new List<string>(muted);
            configuration.Save();
        }

        Changed?.Invoke();
    }
}
