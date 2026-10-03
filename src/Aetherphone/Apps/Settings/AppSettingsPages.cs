using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Apps.Settings;

internal sealed class AppSettingsPages
{
    private readonly Dictionary<string, AppSettingsPage> pages = new(StringComparer.Ordinal);
    private readonly Configuration configuration;
    private readonly SoundService sound;
    private readonly AppInstaller installer;
    private readonly ConfirmService confirm;
    private readonly ISettingsNavigator navigator;

    public AppSettingsPages(Configuration configuration, SoundService sound, AppInstaller installer,
        ConfirmService confirm, ISettingsNavigator navigator)
    {
        this.configuration = configuration;
        this.sound = sound;
        this.installer = installer;
        this.confirm = confirm;
        this.navigator = navigator;
    }

    public AppSettingsPage For(in AppSettingsEntry entry)
    {
        if (pages.TryGetValue(entry.AppId, out var page))
        {
            return page;
        }

        page = new AppSettingsPage(entry, configuration, sound, installer, confirm, navigator);
        pages[entry.AppId] = page;
        return page;
    }
}
