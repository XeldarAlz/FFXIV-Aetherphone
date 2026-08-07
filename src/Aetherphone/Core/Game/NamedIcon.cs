namespace Aetherphone.Core.Game;

internal readonly struct NamedIcon
{
    public readonly string Name;
    public readonly uint IconId;

    public NamedIcon(string name, uint iconId)
    {
        Name = name;
        IconId = iconId;
    }

    public bool IsValid => IconId != 0 && !string.IsNullOrEmpty(Name);
}
