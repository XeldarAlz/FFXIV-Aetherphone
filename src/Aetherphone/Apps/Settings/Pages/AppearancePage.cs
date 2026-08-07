using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppearancePage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Appearance);
    public string Summary => CatalogLabels.Accent(configuration.AccentName);
    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    public string? GuideAnchor => "settings.row.appearance";
    private const float SizeReadoutColumn = 46f;
    private static readonly ThemeMode[] ModeOrder = { ThemeMode.Light, ThemeMode.Dark, ThemeMode.Auto };
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly ISettingsNavigator navigator;
    private readonly PhotoLibrary photos;
    private readonly ConfirmService confirm;
    private readonly WallpaperLibrary wallpapers;
    private readonly WallpaperImageCache wallpaperImages;

    public AppearancePage(Configuration configuration, ThemeProvider themes, ISettingsNavigator navigator,
        PhotoLibrary photos, ConfirmService confirm, WallpaperLibrary wallpapers,
        WallpaperImageCache wallpaperImages)
    {
        this.configuration = configuration;
        this.themes = themes;
        this.navigator = navigator;
        this.photos = photos;
        this.confirm = confirm;
        this.wallpapers = wallpapers;
        this.wallpaperImages = wallpaperImages;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Theme), theme);
            var accentLabel = Loc.T(L.Settings.Accent);
            var cardWidth = ImGui.GetContentRegionAvail().X - 2f * Metrics.Space.Lg * UiScale.Current;
            var accentStacked = SwatchStrip.NeedsTwoRows(accentLabel, ThemeCatalog.Accents.Count + 1, cardWidth);
            var card = GroupCard.Begin(theme, accentStacked ? 5 : 4);
            var modeIndex = SegmentStrip.Draw("settings.themeMode", card.NextRow(), ModeLabels(), CurrentModeIndex(),
                theme);
            var mode = ModeOrder[modeIndex];
            if (mode != configuration.ThemeMode)
            {
                configuration.ThemeMode = mode;
                ApplyTheme();
            }

            var customAccent = ThemeCatalog.IsCustomAccent(configuration.AccentName);
            var accentIndex = SwatchStrip.Draw(card.NextRow(accentStacked ? 2 : 1), accentLabel, ThemeCatalog.Accents,
                customAccent ? -1 : ThemeCatalog.IndexOf(ThemeCatalog.Accents, configuration.AccentName), theme,
                accentStacked, ThemeCatalog.ResolveAccent(configuration.AccentName), customAccent);
            if (accentIndex == ThemeCatalog.Accents.Count)
            {
                navigator.Open(new AccentPage(configuration, themes));
            }
            else if (accentIndex >= 0)
            {
                var accentName = ThemeCatalog.Accents[accentIndex].Name;
                if (accentName != configuration.AccentName)
                {
                    configuration.AccentName = accentName;
                    ApplyTheme();
                }
            }

            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.PhoneCase),
                    CatalogLabels.PhoneCase(configuration.PhoneCaseName), theme))
            {
                navigator.Open(new PhoneCasePage(configuration, themes));
            }

            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.Wallpaper), string.Empty, theme))
            {
                navigator.Open(new WallpaperPage(configuration, themes, navigator, photos, wallpapers,
                    wallpaperImages));
            }

            card.End();
            SettingsSection.Header(Loc.T(L.Settings.TextSize), theme);
            var zoomCard = GroupCard.Begin(theme, 1);
            DrawTextSizeSlider(zoomCard.NextRow(), theme);
            zoomCard.End();

            SettingsSection.Header(Loc.T(L.Settings.PhoneSize), theme);
            var sizeCard = GroupCard.Begin(theme, 1);
            DrawPhoneSizeSlider(sizeCard.NextRow(), theme);
            sizeCard.End();

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

            DrawHomeSection(theme);
        }
    }

    private void DrawPhoneSizeSlider(Rect row, PhoneTheme theme)
    {
        var smallest = PhoneSizeCatalog.MinimumWidth;
        var largest = MathF.Max(PhoneBounds.ClampWidth(PhoneSizeCatalog.MaximumWidth), smallest + 1f);
        var span = largest - smallest;
        var effective = PhoneBounds.ClampWidth(configuration.PhoneWidth);
        var slider = DrawSettingSlider("settings.phoneSize", row, theme, (effective - smallest) / span);
        var width = PhoneSizeCatalog.Snap(PhoneBounds.ClampWidth(smallest + slider.Value * span));
        if ((slider.Dragging || slider.Released) && MathF.Abs(width - configuration.PhoneWidth) > 0.01f)
        {
            configuration.PhoneWidth = width;
        }

        if (slider.Released)
        {
            configuration.Save();
        }

        DrawPercentReadout(row, theme, PhoneSizeCatalog.ZoomFor(PhoneBounds.ClampWidth(configuration.PhoneWidth)));
    }

    private void DrawTextSizeSlider(Rect row, PhoneTheme theme)
    {
        const float smallest = TextZoomCatalog.MinimumZoom;
        const float span = TextZoomCatalog.MaximumZoom - TextZoomCatalog.MinimumZoom;
        var effective = TextZoomCatalog.Clamp(configuration.TextZoom);
        var slider = DrawSettingSlider("settings.textZoom", row, theme, (effective - smallest) / span);
        var zoom = TextZoomCatalog.Snap(TextZoomCatalog.Clamp(smallest + slider.Value * span));
        if ((slider.Dragging || slider.Released) && MathF.Abs(zoom - configuration.TextZoom) > 0.001f)
        {
            configuration.TextZoom = zoom;
            Plugin.Fonts.SetZoom(zoom);
        }

        if (slider.Released)
        {
            configuration.Save();
        }

        DrawPercentReadout(row, theme, TextZoomCatalog.Clamp(configuration.TextZoom));
    }

    private static Slider.Result DrawSettingSlider(string id, Rect row, PhoneTheme theme, float normalized)
    {
        var scale = UiScale.Current;
        return Slider.Draw(id, row, normalized, theme, 0f,
            SizeReadoutColumn * scale + Metrics.Space.Md * scale);
    }

    private static void DrawPercentReadout(Rect row, PhoneTheme theme, float fraction)
    {
        var text = $"{(int)MathF.Round(fraction * 100f)}%";
        var size = Typography.Measure(text, TextStyles.Body);
        Typography.Draw(new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f), text, theme.TextMuted,
            TextStyles.Body);
    }

    private void DrawHomeSection(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Home.HomeScreen), theme);
        var card = GroupCard.Begin(theme, 3);
        var densityIndex = SegmentStrip.Draw("settings.homeGrid", card.NextRow(), DensityLabels(),
            DensityIndex(configuration.HomeGridRows), theme);
        var rows = GridRowOptions[densityIndex];
        if (rows != configuration.HomeGridRows)
        {
            configuration.HomeGridRows = rows;
            configuration.Save();
        }

        var showAppNames = SettingsRow.Bool(card.NextRow(), Loc.T(L.Home.ShowAppNames), configuration.ShowAppNames,
            theme);
        if (showAppNames != configuration.ShowAppNames)
        {
            configuration.ShowAppNames = showAppNames;
            configuration.Save();
        }

        if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Home.ResetLayout), string.Empty, theme))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Home.ResetLayout),
                Message = Loc.T(L.Home.ResetLayoutMessage),
                ConfirmLabel = Loc.T(L.Home.ResetLayoutConfirm),
                CancelLabel = Loc.T(L.Photos.DeleteCancel),
                Danger = true,
                Confirm = ResetHomeLayout,
            });
        }

        card.End();
    }

    private void ResetHomeLayout()
    {
        configuration.Home = null;
        configuration.Save();
    }

    private static readonly int[] GridRowOptions = { 5, 6, 7 };

    private static string[] DensityLabels() =>
        new[] { Loc.T(L.Home.GridComfortable), Loc.T(L.Home.GridStandard), Loc.T(L.Home.GridCompact), };

    private static int DensityIndex(int rows)
    {
        for (var index = 0; index < GridRowOptions.Length; index++)
        {
            if (GridRowOptions[index] == HomeLayoutService.ClampRows(rows))
            {
                return index;
            }
        }

        return 1;
    }

    private static string[] ModeLabels() =>
        new[] { Loc.T(L.Settings.ThemeLight), Loc.T(L.Settings.ThemeDark), Loc.T(L.Settings.ThemeAuto), };

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
