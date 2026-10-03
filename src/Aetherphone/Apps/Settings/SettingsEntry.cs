using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Settings;

internal readonly struct SettingsEntry
{
    public readonly LocString Label;
    public readonly LocString Section;
    public readonly bool HasSection;

    public SettingsEntry(LocString label)
    {
        Label = label;
        Section = default;
        HasSection = false;
    }

    public SettingsEntry(LocString label, LocString section)
    {
        Label = label;
        Section = section;
        HasSection = true;
    }
}
