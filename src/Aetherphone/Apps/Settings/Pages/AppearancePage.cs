using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppearancePage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.Theme),
        new(L.Settings.SeasonalDecorations),
        new(L.Settings.SeasonalNameFont, L.Settings.SeasonalDecorations),
        new(L.Settings.SeasonalParallax, L.Settings.SeasonalDecorations),
        new(L.Settings.Accent),
        new(L.Settings.IconAppearance),
        new(L.Settings.Wallpaper),
        new(L.Settings.PhoneCase),
        new(L.Home.Looks, L.Home.HomeScreen),
        new(L.Home.ShowAppNames, L.Home.HomeScreen),
        new(L.Home.ResetLayout, L.Home.HomeScreen),
    };

    public string Title => Loc.T(L.Settings.Appearance);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    public string? GuideAnchor => "settings.row.appearance";
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private const float CardGap = Metrics.Space.Xl;
    private const float HeaderGap = Metrics.Space.Sm;
    private const float WallpaperRowHeight = 132f;
    private const float WallpaperPreviewHeight = 88f;
    private const float WallpaperPreviewGap = 24f;
    private const float WallpaperLabelGap = 6f;
    private const float WallpaperPadY = 12f;
    private const float FallbackWallpaperAspect = 0.5f;
    private const float MinimumWallpaperAspect = 0.1f;
    private static readonly ThemeMode[] ModeOrder = { ThemeMode.Light, ThemeMode.Dark, ThemeMode.Auto };
    private static readonly int[] GridRowOptions = { 5, 6, 7 };
    private readonly string[] modeLabels = new string[ModeOrder.Length];
    private readonly string[] densityLabels = new string[GridRowOptions.Length];
    private LanguageInfo? labelsLanguage;
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly ISettingsNavigator navigator;
    private readonly PhotoLibrary photos;
    private readonly ConfirmService confirm;
    private readonly WallpaperLibrary wallpapers;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly HomeLookService looks;

    public AppearancePage(Configuration configuration, ThemeProvider themes, ISettingsNavigator navigator,
        PhotoLibrary photos, ConfirmService confirm, WallpaperLibrary wallpapers,
        WallpaperImageCache wallpaperImages, HomeLookService looks)
    {
        this.looks = looks;
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
        var scale = UiScale.Current;
        var theme = context.Theme;
        EnsureLabels();
        using (AppSurface.Begin(body))
        {
            DrawThemeCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawAccentCard(theme, scale);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawIconCard(theme);
            ImGui.Dummy(new Vector2(0f, HeaderGap * scale));
            DrawWallpaperCard(theme, scale);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawCaseCard(theme);
            ImGui.Dummy(new Vector2(0f, HeaderGap * scale));
            DrawHomeCard(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void EnsureLabels()
    {
        if (ReferenceEquals(labelsLanguage, Loc.Current))
        {
            return;
        }

        labelsLanguage = Loc.Current;
        modeLabels[0] = Loc.T(L.Settings.ThemeLight);
        modeLabels[1] = Loc.T(L.Settings.ThemeDark);
        modeLabels[2] = Loc.T(L.Settings.ThemeAuto);
        densityLabels[0] = Loc.T(L.Home.GridComfortable);
        densityLabels[1] = Loc.T(L.Home.GridStandard);
        densityLabels[2] = Loc.T(L.Home.GridCompact);
    }

    private void DrawThemeCard(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Settings.Theme), theme);
        var decorated = configuration.SeasonalDecorations;
        var card = GroupCard.Begin(theme, decorated ? 4 : 2);
        var modeRow = card.NextRow();
        UiAnchors.Report("settings.appearance.theme", modeRow);
        var modeIndex = SegmentStrip.Draw("settings.themeMode", modeRow, modeLabels, CurrentModeIndex(), theme);
        var seasonal = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.SeasonalDecorations),
            configuration.SeasonalDecorations, theme, hint: Loc.T(L.Settings.SeasonalDecorationsHint));
        var nameFont = decorated
            ? SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.SeasonalNameFont), configuration.SeasonalNameFont,
                theme, hint: Loc.T(L.Settings.SeasonalNameFontHint))
            : configuration.SeasonalNameFont;
        var parallax = decorated
            ? SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.SeasonalParallax), configuration.SeasonalParallax,
                theme, hint: Loc.T(L.Settings.SeasonalParallaxHint))
            : configuration.SeasonalParallax;
        card.End();
        if (seasonal != configuration.SeasonalDecorations || nameFont != configuration.SeasonalNameFont ||
            parallax != configuration.SeasonalParallax)
        {
            configuration.SeasonalDecorations = seasonal;
            configuration.SeasonalNameFont = nameFont;
            configuration.SeasonalParallax = parallax;
            configuration.Save();
        }

        var mode = ModeOrder[modeIndex];
        if (mode == configuration.ThemeMode)
        {
            return;
        }

        configuration.ThemeMode = mode;
        ApplyTheme();
    }

    private void DrawAccentCard(PhoneTheme theme, float scale)
    {
        var accentLabel = Loc.T(L.Settings.Accent);
        var cardWidth = ImGui.GetContentRegionAvail().X - 2f * Metrics.Space.Lg * scale;
        var stacked = SwatchStrip.NeedsTwoRows(accentLabel, ThemeCatalog.Accents.Count + 1, cardWidth);
        var card = GroupCard.Begin(theme, stacked ? 2 : 1);
        var customAccent = ThemeCatalog.IsCustomAccent(configuration.AccentName);
        var accentIndex = SwatchStrip.Draw(card.NextRow(stacked ? 2 : 1), accentLabel, ThemeCatalog.Accents,
            customAccent ? -1 : ThemeCatalog.IndexOf(ThemeCatalog.Accents, configuration.AccentName), theme, stacked,
            ThemeCatalog.ResolveAccent(configuration.AccentName), customAccent);
        card.End();
        if (accentIndex == ThemeCatalog.Accents.Count)
        {
            navigator.Open(new AccentPage(configuration, themes));
            return;
        }

        if (accentIndex < 0)
        {
            return;
        }

        var accentName = ThemeCatalog.Accents[accentIndex].Name;
        if (accentName == configuration.AccentName)
        {
            return;
        }

        configuration.AccentName = accentName;
        ApplyTheme();
    }

    private void DrawIconCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, IconAppearancePicker.Height);
        var iconAppearance = IconAppearancePicker.Draw(card.NextRow(IconAppearancePicker.Height),
            configuration.IconAppearance, theme);
        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.IconAppearanceHint), theme);
        if (iconAppearance == configuration.IconAppearance)
        {
            return;
        }

        configuration.IconAppearance = iconAppearance;
        configuration.Save();
    }

    private void DrawWallpaperCard(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Settings.Wallpaper), theme);
        var card = GroupCard.Begin(theme, WallpaperRowHeight);
        var row = card.NextRow(WallpaperRowHeight);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            SettingsRow.DrawRowHighlight(row, theme);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var drawList = ImGui.GetWindowDrawList();
        var aspect = WallpaperAspect();
        var previewHeight = WallpaperPreviewHeight * scale;
        var previewWidth = previewHeight * aspect;
        var halfGap = WallpaperPreviewGap * scale * 0.5f;
        var centerX = row.Min.X + (row.Width - SettingsRow.ChevronReserve(scale)) * 0.5f;
        var top = row.Min.Y + WallpaperPadY * scale;
        var lightRect = new Rect(new Vector2(centerX - halfGap - previewWidth, top),
            new Vector2(centerX - halfGap, top + previewHeight));
        var darkRect = new Rect(new Vector2(centerX + halfGap, top),
            new Vector2(centerX + halfGap + previewWidth, top + previewHeight));
        DrawWallpaperPreview(drawList, lightRect, configuration.LightWallpaperId, Loc.T(L.Wallpaper.Light), aspect,
            theme, scale);
        DrawWallpaperPreview(drawList, darkRect, configuration.DarkWallpaperId, Loc.T(L.Wallpaper.Dark), aspect, theme,
            scale);
        SettingsRow.DrawChevron(drawList, new Vector2(row.Max.X, row.Center.Y), scale, theme.TextMuted);
        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        card.End();
        if (!clicked)
        {
            return;
        }

        navigator.Open(new WallpaperPage(configuration, themes, navigator, photos, wallpapers, wallpaperImages));
    }

    private void DrawWallpaperPreview(ImDrawListPtr drawList, Rect rect, string wallpaperId, string label,
        float aspect, PhoneTheme theme, float scale)
    {
        var radius = Metrics.Radius.Md * scale;
        WallpaperRenderer.DrawSingle(drawList, rect, radius, wallpapers.Resolve(wallpaperId), aspect, 1f,
            theme.SurfaceMuted);
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(theme.Separator),
            Metrics.Stroke.Hairline * scale);
        var maxWidth = rect.Width + WallpaperPreviewGap * scale - Metrics.Space.Sm * scale;
        var fitted = Typography.FitText(label, maxWidth, TextStyles.Footnote);
        var size = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(rect.Center.X - size.X * 0.5f, rect.Max.Y + WallpaperLabelGap * scale),
            fitted, theme.TextMuted, TextStyles.Footnote);
    }

    private float WallpaperAspect()
    {
        var aspect = wallpapers.CurrentTargetAspect;
        return aspect > MinimumWallpaperAspect ? aspect : FallbackWallpaperAspect;
    }

    private void DrawCaseCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var opened = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.PhoneCase),
            CatalogLabels.PhoneCase(configuration.PhoneCaseName), theme);
        card.End();
        if (opened)
        {
            navigator.Open(new PhoneCasePage(configuration, themes, navigator));
        }
    }

    private void DrawHomeCard(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Home.HomeScreen), theme);
        var card = GroupCard.Begin(theme, 4);
        if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Home.Looks), LooksPage.NameOf(looks.Active), theme))
        {
            navigator.Open(new LooksPage(looks, wallpapers, navigator, confirm));
        }

        var showAppNames = SettingsRow.Bool(card.NextRow(), Loc.T(L.Home.ShowAppNames), configuration.ShowAppNames,
            theme);
        if (showAppNames != configuration.ShowAppNames)
        {
            configuration.ShowAppNames = showAppNames;
            configuration.Save();
        }

        var densityIndex = SegmentStrip.Draw("settings.homeGrid", card.NextRow(), densityLabels,
            DensityIndex(configuration.HomeGridRows), theme);
        var rows = GridRowOptions[densityIndex];
        if (rows != configuration.HomeGridRows)
        {
            configuration.HomeGridRows = rows;
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
