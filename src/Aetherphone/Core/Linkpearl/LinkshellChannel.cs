namespace Aetherphone.Core.Linkpearl;

internal enum LinkshellKind
{
    Linkshell,
    CrossWorldLinkshell,
}

internal readonly record struct LinkshellChannel(LinkshellKind Kind, int Slot)
{
    private const string CrossWorldPrefix = "cwls:";
    private const string LocalPrefix = "ls:";
    public bool IsCrossWorld => Kind == LinkshellKind.CrossWorldLinkshell;
    public string Key => IsCrossWorld ? $"{CrossWorldPrefix}{Slot}" : $"{LocalPrefix}{Slot}";
    public string Command => IsCrossWorld ? $"/cwlinkshell{Slot + 1}" : $"/linkshell{Slot + 1}";

    public static bool TryParse(string key, out LinkshellChannel channel)
    {
        if (!string.IsNullOrEmpty(key))
        {
            if (key.StartsWith(CrossWorldPrefix, StringComparison.Ordinal) &&
                int.TryParse(key.AsSpan(CrossWorldPrefix.Length), out var crossWorldSlot))
            {
                channel = new LinkshellChannel(LinkshellKind.CrossWorldLinkshell, crossWorldSlot);
                return true;
            }

            if (key.StartsWith(LocalPrefix, StringComparison.Ordinal) &&
                int.TryParse(key.AsSpan(LocalPrefix.Length), out var localSlot))
            {
                channel = new LinkshellChannel(LinkshellKind.Linkshell, localSlot);
                return true;
            }
        }

        channel = default;
        return false;
    }
}

internal readonly record struct LinkshellEntry(LinkshellChannel Channel, string Name);
