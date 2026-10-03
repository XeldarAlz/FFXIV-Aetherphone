using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class GeneralPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.ShowInGpose),
        new(L.Settings.ImportScreenshots),
        new(L.Settings.MonthlyAlbums),
        new(L.Settings.NativeFileDialog),
        new(L.Settings.ShowSensitive),
        new(L.Settings.MarketContextMenu),
        new(L.Settings.LinkpearlContextMenu),
        new(L.Settings.AutoTranslate),
        new(L.Settings.Use24HourClock, L.Settings.ClockFormat),
        new(L.Settings.OpenOnStartup, L.Settings.Startup),
        new(L.Settings.OpenMinimized, L.Settings.Startup),
    };

    public string Title => Loc.T(L.Settings.General);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.SlidersH;
    public Vector4 Tint => new(0.52f, 0.54f, 0.60f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly Configuration configuration;
    private readonly TranslationService translation;
    private readonly ConfirmService confirm;

    public GeneralPage(Configuration configuration, TranslationService translation, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.translation = translation;
        this.confirm = confirm;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var translationRow = translation.Enabled ? 1 : 0;
            var card = GroupCard.Begin(theme, 7 + translationRow);
            var showInGpose = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ShowInGpose),
                configuration.ShowInGpose, theme, null, Loc.T(L.Settings.ShowInGposeHint));
            var importScreenshots = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ImportScreenshots),
                configuration.ImportScreenshots, theme, null, Loc.T(L.Settings.ImportScreenshotsHint));
            var monthlyAlbums = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.MonthlyAlbums),
                configuration.PhotosMonthlyAlbums, theme, null, Loc.T(L.Settings.MonthlyAlbumsHint));
            var usesNativeFileDialog = configuration.UseNativeFileDialog ?? NativeFileDialog.IsSupported;
            var nativeFileDialog = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.NativeFileDialog),
                usesNativeFileDialog, theme, null, Loc.T(L.Settings.NativeFileDialogHint));
            var showSensitive = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ShowSensitive),
                configuration.ShowSensitiveContent, theme, null, Loc.T(L.Settings.ShowSensitiveHint));
            var marketContextMenu = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.MarketContextMenu),
                configuration.MarketContextMenu, theme, null, Loc.T(L.Settings.MarketContextMenuHint));
            var linkpearlContextMenu = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.LinkpearlContextMenu),
                configuration.LinkpearlContextMenu, theme, null, Loc.T(L.Settings.LinkpearlContextMenuHint));
            var autoTranslate = configuration.AutoTranslatePosts;
            if (translationRow > 0)
            {
                autoTranslate = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.AutoTranslate),
                    configuration.AutoTranslatePosts, theme, null, Loc.T(L.Settings.AutoTranslateHint));
            }

            card.End();
            if (autoTranslate != configuration.AutoTranslatePosts)
            {
                SetAutoTranslate(autoTranslate);
            }

            if (showInGpose != configuration.ShowInGpose)
            {
                configuration.ShowInGpose = showInGpose;
                Plugin.PluginInterface.UiBuilder.DisableGposeUiHide = showInGpose;
                configuration.Save();
            }

            if (importScreenshots != configuration.ImportScreenshots)
            {
                configuration.ImportScreenshots = importScreenshots;
                configuration.Save();
            }

            if (monthlyAlbums != configuration.PhotosMonthlyAlbums)
            {
                configuration.PhotosMonthlyAlbums = monthlyAlbums;
                configuration.PhotosMonthlyAlbumsAsked = true;
                configuration.Save();
            }

            if (nativeFileDialog != usesNativeFileDialog)
            {
                configuration.UseNativeFileDialog = nativeFileDialog;
                configuration.Save();
            }

            if (showSensitive != configuration.ShowSensitiveContent)
            {
                configuration.ShowSensitiveContent = showSensitive;
                configuration.Save();
            }

            if (marketContextMenu != configuration.MarketContextMenu)
            {
                configuration.MarketContextMenu = marketContextMenu;
                configuration.Save();
            }

            if (linkpearlContextMenu != configuration.LinkpearlContextMenu)
            {
                configuration.LinkpearlContextMenu = linkpearlContextMenu;
                configuration.Save();
            }

            SettingsSection.Header(Loc.T(L.Settings.ClockFormat), theme);
            var clockCard = GroupCard.Begin(theme, 1);
            var use24Hour = SettingsRow.Bool(clockCard.NextRow(), Loc.T(L.Settings.Use24HourClock),
                TimeText.Use24Hour, theme, null, TimeText.Clock(DateTime.Now));
            clockCard.End();
            if (use24Hour != TimeText.Use24Hour)
            {
                configuration.Use24HourClock = use24Hour;
                TimeText.ApplyClockPreference(use24Hour);
                configuration.Save();
            }

            SettingsSection.Header(Loc.T(L.Settings.Startup), theme);
            var startupCard = GroupCard.Begin(theme, 2);
            var openStartup = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenOnStartup),
                configuration.OpenOnStartup, theme);
            var openMinimized = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenMinimized),
                configuration.OpenMinimizedOnStartup, theme, null, Loc.T(L.Settings.StartupHint));
            startupCard.End();
            if (openStartup != configuration.OpenOnStartup)
            {
                configuration.OpenOnStartup = openStartup;
                configuration.Save();
            }

            if (openMinimized != configuration.OpenMinimizedOnStartup)
            {
                configuration.OpenMinimizedOnStartup = openMinimized;
                configuration.Save();
            }
        }
    }
    private void SetAutoTranslate(bool enabled)
    {
        if (!enabled)
        {
            configuration.AutoTranslatePosts = false;
            configuration.Save();
            return;
        }

        TranslateLink.WithDisclosure(translation, confirm, () =>
        {
            configuration.AutoTranslatePosts = true;
            configuration.Save();
        });
    }
}
