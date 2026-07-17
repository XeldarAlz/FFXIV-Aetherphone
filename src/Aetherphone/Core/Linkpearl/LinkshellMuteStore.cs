namespace Aetherphone.Core.Linkpearl;

internal sealed class LinkshellMuteStore
{
    private readonly Configuration configuration;
    private readonly HashSet<string> muted;
    public event Action? Changed;

    public LinkshellMuteStore(Configuration configuration)
    {
        this.configuration = configuration;
        muted = new HashSet<string>(configuration.MutedLinkshells, StringComparer.Ordinal);
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

        configuration.MutedLinkshells = new List<string>(muted);
        configuration.Save();
        Changed?.Invoke();
    }
}
