namespace Aetherphone.Core.Apps;

internal enum SettingsPageKind : byte
{
    None,
    Notifications,
    Privacy,
    Calls,
}

internal sealed class SettingsLauncher
{
    private SettingsPageKind pending;

    public void Request(SettingsPageKind page) => pending = page;

    public SettingsPageKind TryConsume()
    {
        var page = pending;
        pending = SettingsPageKind.None;
        return page;
    }
}
