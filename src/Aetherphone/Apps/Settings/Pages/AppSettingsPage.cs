using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppSettingsPage : ISettingsPage
{
    public static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.AllowNotifications),
        new(L.Settings.Banners),
        new(L.Settings.Sounds),
        new(L.Settings.Badges),
    };

    private const float HeroRowHeight = 84f;
    private const float HeroTileSize = 56f;
    private const float HeroTextGap = Metrics.Space.Lg;
    private const float HeroLineGap = Metrics.Space.Xxs;
    public string Title => app.DisplayName;
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Th;
    public Vector4 Tint => app.Accent;
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly IPhoneApp app;
    private readonly bool hasChannel;
    private readonly bool hasBadge;
    private readonly bool hasStoreEntry;
    private readonly StoreEntry storeEntry;
    private readonly Configuration configuration;
    private readonly SoundService sound;
    private readonly AppInstaller installer;
    private readonly ConfirmService confirm;
    private readonly ISettingsNavigator navigator;
    private readonly Action<string?> selectSound;
    private readonly Action removeApp;

    public AppSettingsPage(in AppSettingsEntry entry, Configuration configuration, SoundService sound,
        AppInstaller installer, ConfirmService confirm, ISettingsNavigator navigator)
    {
        app = entry.App;
        hasChannel = entry.HasChannel;
        hasBadge = entry.HasBadge;
        hasStoreEntry = AppStoreCatalog.TryFor(entry.AppId, out storeEntry);
        this.configuration = configuration;
        this.sound = sound;
        this.installer = installer;
        this.confirm = confirm;
        this.navigator = navigator;
        selectSound = SelectSound;
        removeApp = RemoveApp;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawHero(theme, scale);
            if (hasChannel || hasBadge)
            {
                DrawAlerts(theme);
            }

            if (string.Equals(app.Id, MusicMediaSettings.AppId, StringComparison.Ordinal))
            {
                MusicMediaSettings.Draw(configuration, theme);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawActions(context, theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void DrawHero(PhoneTheme theme, float scale)
    {
        var card = GroupCard.Begin(theme, HeroRowHeight);
        var row = card.NextRow(HeroRowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var tileSize = HeroTileSize * scale;
        IconTile.DrawApp(drawList, app.Id, new Vector2(row.Min.X + tileSize * 0.5f, row.Center.Y), tileSize,
            IconTile.Surface(app.Accent));
        var textLeft = row.Min.X + tileSize + HeroTextGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - textLeft);
        var hovering = UiInteract.Hover(row.Min, row.Max);
        var name = app.DisplayName;
        var nameHeight = Typography.Measure(name, TextStyles.Title3).Y;
        if (!hasStoreEntry)
        {
            Marquee.DrawLeft(new MarqueeId("settings.app.name", app.Id), name, textLeft,
                row.Center.Y - nameHeight * 0.5f, textWidth, TextStyles.Title3, theme.TextStrong, hovering);
            card.End();
            return;
        }

        var subtitle = Loc.T(storeEntry.Subtitle);
        var subtitleHeight = Typography.Measure(subtitle, TextStyles.Footnote).Y;
        var blockHeight = nameHeight + HeroLineGap * scale + subtitleHeight;
        var nameY = row.Center.Y - blockHeight * 0.5f;
        Marquee.DrawLeft(new MarqueeId("settings.app.name", app.Id), name, textLeft, nameY, textWidth,
            TextStyles.Title3, theme.TextStrong, hovering);
        Marquee.DrawLeft(new MarqueeId("settings.app.subtitle", app.Id), subtitle, textLeft,
            nameY + nameHeight + HeroLineGap * scale, textWidth, TextStyles.Footnote, theme.TextMuted, hovering);
        card.End();
    }

    private void DrawAlerts(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Common.Alerts), theme);
        var setting = configuration.NotificationSettingFor(app.Id);
        var card = GroupCard.Begin(theme, (hasChannel ? 3 : 0) + (hasBadge ? 1 : 0));
        if (hasChannel)
        {
            var allow = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.AllowNotifications), setting.Enabled, theme);
            var banners = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.Banners), setting.ShowNotificationBanner,
                theme, dimmed: !setting.Enabled);
            var sounds = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.Sounds), setting.PlaySound, theme,
                dimmed: !setting.Enabled);
            if (allow != setting.Enabled || banners != setting.ShowNotificationBanner || sounds != setting.PlaySound)
            {
                setting.Enabled = allow;
                setting.ShowNotificationBanner = banners;
                setting.PlaySound = sounds;
                configuration.Save();
            }
        }

        if (hasBadge)
        {
            var badgeEnabled = configuration.IsAppBadgeEnabled(app.Id);
            var badges = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.Badges), badgeEnabled, theme);
            if (badges != badgeEnabled)
            {
                configuration.SetAppBadgeEnabled(app.Id, badges);
            }
        }

        card.End();
        if (!hasChannel || !setting.Enabled || !setting.PlaySound)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Settings.Sound), theme);
        SoundOptionList.Draw(theme, sound, SoundKind.Notification, configuration.AppSoundOverride(app.Id), true,
            selectSound);
    }

    private void DrawActions(in PhoneContext context, PhoneTheme theme)
    {
        var canRemove = AppInstaller.CanUninstall(app.Id);
        var card = GroupCard.Begin(theme, canRemove ? 2 : 1);
        var open = SettingsRow.Action(card.NextRow(), Loc.T(L.Spotlight.Open), theme.Accent, theme);
        var remove = canRemove && SettingsRow.Action(card.NextRow(), Loc.T(L.Settings.RemoveApp), theme.Danger, theme);
        card.End();
        if (open)
        {
            context.Navigation.Open(app.Id);
        }

        if (remove)
        {
            AskRemove();
        }
    }

    private void AskRemove()
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Home.RemoveConfirm, app.DisplayName),
            ConfirmLabel = Loc.T(L.Home.Remove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = removeApp,
        });
    }

    private void RemoveApp()
    {
        if (!installer.Uninstall(app.Id))
        {
            return;
        }

        navigator.Back();
    }

    private void SelectSound(string? token)
    {
        var setting = configuration.NotificationSettingFor(app.Id);
        if (!string.Equals(setting.Sound, token, StringComparison.Ordinal))
        {
            setting.Sound = token;
            configuration.Save();
        }

        sound.Preview(SoundKind.Notification, token ?? configuration.NotificationSound,
            configuration.NotificationVolume);
    }
}
