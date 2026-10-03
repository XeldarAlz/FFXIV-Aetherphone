using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NotificationsPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.QuietWhileBusy),
        new(L.Settings.ShowNotificationBanner),
    };

    public string Title => Loc.T(L.Settings.Notifications);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Bell;
    public Vector4 Tint => new(0.98f, 0.27f, 0.25f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly Configuration configuration;
    private readonly ISettingsNavigator navigator;
    private readonly InstalledAppList apps;
    private readonly AppSettingsPages pages;

    public NotificationsPage(Configuration configuration, ISettingsNavigator navigator, InstalledAppList apps,
        AppSettingsPages pages)
    {
        this.configuration = configuration;
        this.navigator = navigator;
        this.apps = apps;
        this.pages = pages;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var doNotDisturb = configuration.DoNotDisturb;
            var alerts = GroupCard.Begin(theme, 2);
            var quietWhileBusy = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.QuietWhileBusy),
                configuration.QuietWhileBusy, theme, null, Loc.T(L.Settings.QuietWhileBusyHint),
                dimmed: doNotDisturb);
            var showNotificationBanner = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.ShowNotificationBanner),
                configuration.ShowNotificationBanner, theme, null, Loc.T(L.Settings.ShowNotificationBannerHint),
                dimmed: doNotDisturb);
            alerts.End();
            if (quietWhileBusy != configuration.QuietWhileBusy)
            {
                configuration.QuietWhileBusy = quietWhileBusy;
                configuration.Save();
            }

            if (showNotificationBanner != configuration.ShowNotificationBanner)
            {
                configuration.ShowNotificationBanner = showNotificationBanner;
                configuration.Save();
            }

            var entries = apps.Entries;
            var count = NotifyingCount(entries);
            if (count == 0)
            {
                return;
            }

            SettingsSection.Header(Loc.T(L.Settings.NotificationApps), theme);
            var rows = GroupCard.Begin(theme, count);
            rows.SeparatorInset = SettingsRow.AppTileTextInset;
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (!IsListed(entry))
                {
                    continue;
                }

                if (SettingsRow.AppLink(rows.NextRow(), entry.AppId, entry.Accent, entry.Name,
                        AppNotificationSummary.For(configuration, entry), theme))
                {
                    navigator.Open(pages.For(entry));
                }
            }

            rows.End();
        }
    }

    private bool IsListed(in AppSettingsEntry entry) => entry.Notifies && apps.IsInstalled(entry.AppId);

    private int NotifyingCount(ReadOnlySpan<AppSettingsEntry> entries)
    {
        var count = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (IsListed(entries[index]))
            {
                count++;
            }
        }

        return count;
    }
}
