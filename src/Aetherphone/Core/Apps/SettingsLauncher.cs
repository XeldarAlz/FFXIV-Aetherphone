namespace Aetherphone.Core.Apps;

internal enum SettingsPageKind : byte
{
    None,
    Notifications,
    Privacy,
    Calls,
    Changelog,
}

internal sealed class SettingsLauncher
{
    private SettingsPageKind pending;
    private string? pendingAppId;

    public void Request(SettingsPageKind page)
    {
        pending = page;
        pendingAppId = null;
    }

    public void RequestAppNotifications(string appId)
    {
        pending = SettingsPageKind.Notifications;
        pendingAppId = appId;
    }

    public SettingsPageKind TryConsume()
    {
        var page = pending;
        pending = SettingsPageKind.None;
        return page;
    }

    public string? TryConsumeAppId()
    {
        var appId = pendingAppId;
        pendingAppId = null;
        return appId;
    }
}
