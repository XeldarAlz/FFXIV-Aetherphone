namespace Aetherphone.Core.Messaging;

internal enum LinkshellKind
{
    Linkshell,
    CrossWorldLinkshell,
}

internal readonly record struct LinkshellChannel(LinkshellKind Kind, int Slot)
{
    public bool IsCrossWorld => Kind == LinkshellKind.CrossWorldLinkshell;

    public string Key => IsCrossWorld ? $"cwls:{Slot}" : $"ls:{Slot}";

    public string Command => IsCrossWorld ? $"/cwlinkshell{Slot + 1}" : $"/linkshell{Slot + 1}";
}

internal readonly record struct LinkshellEntry(LinkshellChannel Channel, string Name);
