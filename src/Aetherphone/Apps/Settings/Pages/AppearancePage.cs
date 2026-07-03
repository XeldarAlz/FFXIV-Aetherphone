using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppearancePage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Appearance);

    public string Summary => CatalogLabels.Accent(configuration.AccentName);

    public string Glyph => "A";

    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);

    private static readonly ThemeMode[] ModeOrder = { ThemeMode.Light, ThemeMode.Dark, ThemeMode.Auto };

    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly ISettingsNavigator navigator;
    private readonly PhotoLibrary photos;

    public AppearancePage(Configuration configuration, ThemeProvider themes, ISettingsNavigator navigator, PhotoLibrary photos)
    {
        this.configuration = configuration;
        this.themes = themes;
        this.navigator = navigator;
        this.photos = photos;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Theme), theme);
            var card = GroupCard.Begin(theme, 3);

            var modeIndex = SegmentStrip.Draw("settings.themeMode", card.NextRow(), ModeLabels(), CurrentModeIndex(), theme);
            var mode = ModeOrder[modeIndex];
            if (mode != configuration.ThemeMode)
            {
                configuration.ThemeMode = mode;
                ApplyTheme();
            }

            var accentIndex = SwatchStrip.Draw(card.NextRow(), Loc.T(L.Settings.Accent), ThemeCatalog.Accents, ThemeCatalog.IndexOf(ThemeCatalog.Accents, configuration.AccentName), theme);
            var accentName = ThemeCatalog.Accents[accentIndex].Name;
            if (accentName != configuration.AccentName)
            {
                configuration.AccentName = accentName;
                ApplyTheme();
            }

            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.Wallpaper), string.Empty, theme))
            {
                navigator.Open(new WallpaperPage(configuration, themes, navigator, photos));
            }

            card.End();

            SettingsSection.Header(Loc.T(L.Settings.TextSize), theme);
            var zoomCard = GroupCard.Begin(theme, 1);
            var zoomIndex = SegmentStrip.Draw("settings.textZoom", zoomCard.NextRow(), TextZoomCatalog.Labels, TextZoomCatalog.IndexOf(configuration.TextZoom), theme);
            zoomCard.End();

            var zoom = TextZoomCatalog.Scales[zoomIndex];
            if (MathF.Abs(zoom - configuration.TextZoom) > 0.001f)
            {
                configuration.TextZoom = zoom;
                Plugin.Fonts.SetZoom(zoom);
                configuration.Save();
            }

            SettingsSection.Header(Loc.T(L.Settings.PhoneSize), theme);
            var sizeCard = GroupCard.Begin(theme, 1);
            var sizeIndex = SegmentStrip.Draw("settings.phoneSize", sizeCard.NextRow(), PhoneSizeCatalog.Labels, PhoneSizeCatalog.IndexOf(configuration.PhoneScale), theme);
            sizeCard.End();

            var phoneScale = PhoneSizeCatalog.Scales[sizeIndex];
            if (MathF.Abs(phoneScale - configuration.PhoneScale) > 0.001f)
            {
                configuration.PhoneScale = phoneScale;
                configuration.Save();
            }
        }
    }

    private static string[] ModeLabels() => new[]
    {
        Loc.T(L.Settings.ThemeLight),
        Loc.T(L.Settings.ThemeDark),
        Loc.T(L.Settings.ThemeAuto),
    };

    private int CurrentModeIndex()
    {
        for (var index = 0; index < ModeOrder.Length; index++)
        {
            if (ModeOrder[index] == configuration.ThemeMode)
            {
                return index;
            }
        }

        return 0;
    }

    private void ApplyTheme()
    {
        themes.Apply(configuration);
        configuration.Save();
    }
}
