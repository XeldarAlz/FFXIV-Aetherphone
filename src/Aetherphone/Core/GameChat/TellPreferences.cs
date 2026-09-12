namespace Aetherphone.Core.GameChat;

internal sealed class TellPreferences
{
    private readonly Configuration configuration;
    private readonly HashSet<string> pinned = new(StringComparer.Ordinal);
    private readonly HashSet<string> muted = new(StringComparer.Ordinal);

    public TellPreferences(Configuration configuration)
    {
        this.configuration = configuration;
        pinned.UnionWith(configuration.LinkpearlPinnedTells);
        muted.UnionWith(configuration.LinkpearlMutedTells);
    }

    public event Action? Changed;

    public bool IsPinned(string streamKey) => pinned.Contains(streamKey);

    public bool IsMuted(string streamKey) => muted.Contains(streamKey);

    public bool TogglePinned(string streamKey) =>
        Toggle(pinned, configuration.LinkpearlPinnedTells, streamKey);

    public bool ToggleMuted(string streamKey) =>
        Toggle(muted, configuration.LinkpearlMutedTells, streamKey);

    public ChatDensity Layout(string streamKey) =>
        configuration.LinkpearlTellLayouts.TryGetValue(streamKey, out var stored)
            ? (ChatDensity)stored
            : ChatDensity.Bubbles;

    public void SetLayout(string streamKey, ChatDensity density)
    {
        if (Layout(streamKey) == density)
        {
            return;
        }

        configuration.LinkpearlTellLayouts[streamKey] = (int)density;
        Commit();
    }

    public void Forget(string streamKey)
    {
        var changed = pinned.Remove(streamKey) | muted.Remove(streamKey)
                      | configuration.LinkpearlTellLayouts.Remove(streamKey);
        if (!changed)
        {
            return;
        }

        configuration.LinkpearlPinnedTells.Remove(streamKey);
        configuration.LinkpearlMutedTells.Remove(streamKey);
        Commit();
    }

    private bool Toggle(HashSet<string> set, List<string> stored, string streamKey)
    {
        bool next;
        if (set.Remove(streamKey))
        {
            stored.Remove(streamKey);
            next = false;
        }
        else
        {
            set.Add(streamKey);
            stored.Add(streamKey);
            next = true;
        }

        Commit();
        return next;
    }

    private void Commit()
    {
        configuration.Save();
        Changed?.Invoke();
    }
}
