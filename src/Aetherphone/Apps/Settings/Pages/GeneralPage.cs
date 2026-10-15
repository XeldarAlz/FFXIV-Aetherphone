using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class GeneralPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.ShowInGpose),
        new(L.Settings.ShowInCutscenes),
        new(L.Settings.ImportScreenshots),
        new(L.Settings.MonthlyAlbums),
        new(L.Settings.NativeFileDialog),
        new(L.Settings.ShowSensitive),
        new(L.Settings.MarketContextMenu),
        new(L.Settings.LinkpearlContextMenu),
        new(L.Settings.ArrowKeysStepPosts),
        new(L.Settings.AutoTranslate),
        new(L.Settings.Use24HourClock, L.Settings.ClockFormat),
        new(L.Settings.OpenOnStartup, L.Settings.Startup),
        new(L.Settings.OpenMinimized, L.Settings.Startup),
        new(L.Settings.ServerBar),
    };

    private const float ServerBarLabelShare = 0.4f;

    private static readonly LocString[] ServerBarStyles =
    {
        L.Settings.ServerBarIconAndText,
        L.Settings.ServerBarText,
        L.Settings.ServerBarIcon,
    };

    public string Title => Loc.T(L.Settings.General);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.SlidersH;
    public Vector4 Tint => new(0.52f, 0.54f, 0.60f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly Configuration configuration;
    private readonly TranslationService translation;
    private readonly ConfirmService confirm;
    private readonly string[] serverBarLabels = new string[ServerBarStyles.Length];
    private bool editingServerBarText;

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
            var card = GroupCard.Begin(theme, 9 + translationRow);
            var showInGpose = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ShowInGpose),
                configuration.ShowInGpose, theme, null, Loc.T(L.Settings.ShowInGposeHint));
            var showInCutscenes = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ShowInCutscenes),
                configuration.ShowInCutscenes, theme, null, Loc.T(L.Settings.ShowInCutscenesHint));
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
            var arrowKeysStepPosts = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ArrowKeysStepPosts),
                configuration.ArrowKeysStepPosts, theme, null, Loc.T(L.Settings.ArrowKeysStepPostsHint));
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

            if (showInCutscenes != configuration.ShowInCutscenes)
            {
                configuration.ShowInCutscenes = showInCutscenes;
                Plugin.PluginInterface.UiBuilder.DisableCutsceneUiHide = showInCutscenes;
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

            if (arrowKeysStepPosts != configuration.ArrowKeysStepPosts)
            {
                configuration.ArrowKeysStepPosts = arrowKeysStepPosts;
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

            SettingsSection.Header(Loc.T(L.Settings.ServerBar), theme, Loc.T(L.Settings.ServerBarHint));
            DrawServerBar(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void DrawServerBar(PhoneTheme theme, float scale)
    {
        for (var index = 0; index < ServerBarStyles.Length; index++)
        {
            serverBarLabels[index] = Loc.T(ServerBarStyles[index]);
        }

        var card = GroupCard.Begin(theme, 2);
        DrawServerBarPreview(card.NextRow(), theme, scale);
        var picked = SegmentStrip.Draw("settings.serverBar", card.NextRow(), serverBarLabels,
            (int)configuration.ServerBarStyle, theme);
        card.End();
        if (picked == (int)configuration.ServerBarStyle)
        {
            return;
        }

        configuration.ServerBarStyle = (ServerBarStyle)picked;
        configuration.Save();
        Plugin.RefreshServerBar();
    }

    private void DrawServerBarPreview(Rect row, PhoneTheme theme, float scale)
    {
        var label = Loc.T(L.Settings.ServerBarPreview);
        var labelHeight = Typography.Measure(label, TextStyles.BodyEmphasized).Y;
        var labelWidth = Marquee.DrawLeftAuto("settings.serverBarPreview", label, row.Min.X,
            row.Center.Y - labelHeight * 0.5f, row.Width * ServerBarLabelShare, TextStyles.BodyEmphasized,
            theme.TextStrong);
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = Metrics.Size.IconTile * scale;
        var style = configuration.ServerBarStyle;
        if (style == ServerBarStyle.Icon)
        {
            GameFontIcon.Draw(drawList, new Vector2(row.Max.X - iconSize * 0.5f, row.Center.Y), ServerBarEntry.Icon,
                iconSize);
            return;
        }

        var left = row.Min.X + labelWidth + Metrics.Space.Md * scale;
        if (style == ServerBarStyle.IconAndText)
        {
            GameFontIcon.Draw(drawList, new Vector2(left + iconSize * 0.5f, row.Center.Y), ServerBarEntry.Icon,
                iconSize);
            left += iconSize + Metrics.Space.Sm * scale;
        }

        var halfHeight = Metrics.Size.FieldHeight * scale * 0.5f;
        var field = new Rect(new Vector2(left, row.Center.Y - halfHeight),
            new Vector2(row.Max.X, row.Center.Y + halfHeight));
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        var text = configuration.ServerBarText;
        if (GlassField.Text(field, "##settings.serverBarText", ServerBarEntry.DefaultText, ref text, theme, scale,
                ServerBarEntry.MaxTextLength, false, ImGuiInputTextFlags.None))
        {
            configuration.ServerBarText = text;
            Plugin.RefreshServerBar();
        }

        var active = ImGui.IsItemActive();
        if (editingServerBarText && !active)
        {
            configuration.Save();
        }

        editingServerBarText = active;
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
