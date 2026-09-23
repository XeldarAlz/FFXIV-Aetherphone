using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class RootSettingsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Title);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Cog;
    public Vector4 Tint => new(0.56f, 0.57f, 0.63f, 1f);
    public bool OwnsChrome => true;
    private const float TitleBand = 60f;
    private const float BlockGap = 18f;
    private const float CardGap = Metrics.Space.Xl;
    private static readonly Vector4 DiscordTint = new(0.345f, 0.396f, 0.949f, 1f);
    private static readonly Vector4 WebsiteTint = new(0.13f, 0.63f, 0.60f, 1f);
    private static readonly Vector4 DoNotDisturbTint = new(0.36f, 0.40f, 0.92f, 1f);
    private static readonly Vector4 LockTint = new(0.52f, 0.54f, 0.60f, 1f);
    private static readonly Vector4 IdleScrollTint = new(0.20f, 0.70f, 0.62f, 1f);
    private static readonly Vector4 SilentTint = new(0.95f, 0.40f, 0.65f, 1f);
    private readonly ISettingsNavigator navigator;
    private readonly IReadOnlyList<ISettingsPage[]> groups;
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly ISettingsPage accountPage;

    public RootSettingsPage(ISettingsNavigator navigator, IReadOnlyList<ISettingsPage[]> groups,
        Configuration configuration, AethernetSession session, RemoteImageCache images, LodestoneService lodestone,
        ISettingsPage accountPage)
    {
        this.navigator = navigator;
        this.groups = groups;
        this.configuration = configuration;
        this.session = session;
        this.images = images;
        this.lodestone = lodestone;
        this.accountPage = accountPage;
    }

    public void Draw(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        DrawTitle(area, theme, scale);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + TitleBand * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var accountOpened = SettingsHero.Draw(theme, session, images, lodestone);
            UiAnchors.Report("settings.account", new Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
            if (accountOpened)
            {
                navigator.Open(accountPage);
            }

            ImGui.Dummy(new Vector2(0f, (BlockGap - SupportButton.GlowPadding) * scale));
            if (SupportButton.Draw(Loc.T(L.Settings.SupportAetherphone), theme, Loc.T(L.Settings.SupportHint)))
            {
                UrlActions.OpenInBrowser(AepConstants.PatreonUrl);
            }

            ImGui.Dummy(new Vector2(0f, (BlockGap - SupportButton.GlowPadding) * scale));
            DrawQuickSwitches(theme);
            DrawGroups(theme, scale);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawLinks(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawVersion(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private static void DrawTitle(Rect area, PhoneTheme theme, float scale)
    {
        var title = Loc.T(L.Settings.Title);
        var size = Typography.Measure(title, TextStyles.LargeTitle);
        var origin = new Vector2(area.Min.X + Metrics.Space.Lg * scale,
            area.Min.Y + TitleBand * scale - size.Y - Metrics.Space.Md * scale);
        Typography.Draw(ImGui.GetWindowDrawList(), origin, title, theme.TextStrong, TextStyles.LargeTitle);
    }

    private void DrawQuickSwitches(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 4);
        var doNotDisturb = SettingsRow.Switch(card.NextRow(), FontAwesomeIcon.Moon, DoNotDisturbTint,
            Loc.T(L.Settings.DoNotDisturb), configuration.DoNotDisturb, theme);
        if (doNotDisturb != configuration.DoNotDisturb)
        {
            configuration.DoNotDisturb = doNotDisturb;
            configuration.Save();
        }

        var lockPosition = SettingsRow.Switch(card.NextRow(), FontAwesomeIcon.Thumbtack, LockTint,
            Loc.T(L.ControlCenter.LockPosition), configuration.LockPosition, theme,
            Loc.T(L.Settings.LockPositionHint));
        if (lockPosition != configuration.LockPosition)
        {
            configuration.LockPosition = lockPosition;
            configuration.Save();
        }

        var scrollWhileIdle = SettingsRow.Switch(card.NextRow(), FontAwesomeIcon.HandPointUp, IdleScrollTint,
            Loc.T(L.Settings.ScrollWhileIdle), configuration.ScrollWhileIdle, theme,
            Loc.T(L.Settings.ScrollWhileIdleHint));
        if (scrollWhileIdle != configuration.ScrollWhileIdle)
        {
            configuration.ScrollWhileIdle = scrollWhileIdle;
            configuration.Save();
        }

        var silentMode = SettingsRow.Switch(card.NextRow(), FontAwesomeIcon.BellSlash, SilentTint,
            Loc.T(L.Settings.SilentMode), configuration.SilentMode, theme, Loc.T(L.Settings.SilentModeHint));
        if (silentMode != configuration.SilentMode)
        {
            configuration.SilentMode = silentMode;
            configuration.Save();
        }

        card.End();
    }

    private void DrawGroups(PhoneTheme theme, float scale)
    {
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            var pages = groups[groupIndex];
            var card = GroupCard.Begin(theme, VisibleCount(pages));
            for (var index = 0; index < pages.Length; index++)
            {
                var page = pages[index];
                if (page.IsHidden)
                {
                    continue;
                }

                var row = card.NextRow();
                if (page.GuideAnchor is { } anchorKey)
                {
                    UiAnchors.Report(anchorKey, row);
                }

                if (SettingsRow.Link(row, page.Icon, page.Tint, page.Title, page.Summary, theme, page.ShowsBadge))
                {
                    navigator.Open(page);
                }
            }

            card.End();
        }
    }

    private static int VisibleCount(ISettingsPage[] pages)
    {
        var count = 0;
        for (var index = 0; index < pages.Length; index++)
        {
            if (!pages[index].IsHidden)
            {
                count++;
            }
        }

        return count;
    }

    private static void DrawLinks(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 2);
        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.Comments, DiscordTint, Loc.T(L.Settings.JoinDiscord),
                string.Empty, theme))
        {
            UrlActions.OpenInBrowser(AepConstants.DiscordUrl);
        }

        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.Globe, WebsiteTint, Loc.T(L.Settings.VisitWebsite),
                string.Empty, theme))
        {
            UrlActions.OpenInBrowser(AepConstants.WebsiteUrl);
        }

        card.End();
    }

    private static void DrawVersion(PhoneTheme theme)
    {
        var label = $"{AepConstants.Name}  {AepConstants.Version}";
        var size = Typography.Measure(label, TextStyles.Footnote);
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + (available - size.X) * 0.5f, origin.Y),
            label, theme.TextMuted, TextStyles.Footnote);
        ImGui.Dummy(new Vector2(available, size.Y));
    }
}
