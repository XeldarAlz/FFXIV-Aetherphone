using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class RootSettingsPage : ISettingsPage
{
    private readonly struct EntryMatch
    {
        public readonly ISettingsPage Page;
        public readonly int EntryIndex;

        public EntryMatch(ISettingsPage page, int entryIndex)
        {
            Page = page;
            EntryIndex = entryIndex;
        }
    }

    public string Title => Loc.T(L.Settings.Title);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Cog;
    public Vector4 Tint => new(0.56f, 0.57f, 0.63f, 1f);
    private const float SearchTopGap = Metrics.Space.Sm;
    private const float BlockGap = 18f;
    private const float CardGap = Metrics.Space.Xl;
    private const float EmptyStateTop = 40f;
    private static readonly Vector4 DiscordTint = new(0.345f, 0.396f, 0.949f, 1f);
    private static readonly Vector4 WebsiteTint = new(0.13f, 0.63f, 0.60f, 1f);
    private static readonly Vector4 DoNotDisturbTint = new(0.36f, 0.40f, 0.92f, 1f);
    private static readonly Vector4 IdleScrollTint = new(0.20f, 0.70f, 0.62f, 1f);
    private static readonly Vector4 SilentTint = new(0.95f, 0.40f, 0.65f, 1f);
    private static readonly string VersionLabel = string.Concat(AepConstants.Name, "  ", AepConstants.Version);
    private readonly ISettingsNavigator navigator;
    private readonly IReadOnlyList<ISettingsPage[]> groups;
    private readonly bool[][] matches;
    private readonly EntryMatch[] entryMatches;
    private readonly Configuration configuration;
    private readonly ISettingsPage accountPage;
    private readonly ISettingsPage supportPage;
    private readonly ProfileCard profileCard;
    private readonly InstalledAppList apps;
    private readonly AppSettingsPages appPages;
    private bool[] appMatches = Array.Empty<bool>();
    private int entryMatchCount;
    private string query = string.Empty;
    private string filteredQuery = string.Empty;
    private LanguageInfo? filteredLanguage;
    private int filteredRevision = -1;

    public RootSettingsPage(ISettingsNavigator navigator, IReadOnlyList<ISettingsPage[]> groups,
        Configuration configuration, ISettingsPage accountPage, ISettingsPage supportPage, ProfileCard profileCard,
        InstalledAppList apps, AppSettingsPages appPages)
    {
        this.navigator = navigator;
        this.groups = groups;
        this.configuration = configuration;
        this.accountPage = accountPage;
        this.supportPage = supportPage;
        this.profileCard = profileCard;
        this.apps = apps;
        this.appPages = appPages;
        matches = new bool[groups.Count][];
        var entryCount = 0;
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var pages = groups[groupIndex];
            matches[groupIndex] = new bool[pages.Length];
            Array.Fill(matches[groupIndex], true);
            for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
            {
                entryCount += pages[pageIndex].Entries.Length;
            }
        }

        entryMatches = new EntryMatch[entryCount];
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, SearchTopGap * scale));
            SettingsSearchField.Draw("##settingsSearch", Loc.T(L.Settings.SearchHint), ref query, theme, scale);
            UiAnchors.Report("settings.search", new Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
            RefreshFilter();
            if (filteredQuery.Length > 0)
            {
                DrawSearchResults(theme, scale);
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
                return;
            }

            ImGui.Dummy(new Vector2(0f, BlockGap * scale));
            var accountOpened = profileCard.Draw(theme);
            UiAnchors.Report("settings.account", new Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
            if (accountOpened)
            {
                navigator.Open(accountPage);
            }

            ImGui.Dummy(new Vector2(0f, BlockGap * scale));
            if (SupportRow.Draw(theme, supportPage.Title))
            {
                navigator.Open(supportPage);
            }

            ImGui.Dummy(new Vector2(0f, BlockGap * scale));
            DrawQuickSwitches(theme);
            DrawGroups(theme, scale);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawLinks(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawVersion(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void RefreshFilter()
    {
        var appEntries = apps.Entries;
        var trimmed = query.AsSpan().Trim();
        if (ReferenceEquals(filteredLanguage, Loc.Current) && filteredRevision == apps.Revision &&
            trimmed.SequenceEqual(filteredQuery))
        {
            return;
        }

        filteredLanguage = Loc.Current;
        filteredRevision = apps.Revision;
        filteredQuery = trimmed.Length == query.Length ? query : trimmed.ToString();
        var compare = Loc.Culture.CompareInfo;
        entryMatchCount = 0;
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var pages = groups[groupIndex];
            var groupMatches = matches[groupIndex];
            for (var index = 0; index < pages.Length; index++)
            {
                var page = pages[index];
                groupMatches[index] = SettingsSearch.MatchesPage(compare, page.Title, page.Summary, filteredQuery);
                if (page.IsHidden)
                {
                    continue;
                }

                var entries = page.Entries;
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    if (SettingsSearch.MatchesEntry(compare, entries[entryIndex], filteredQuery))
                    {
                        entryMatches[entryMatchCount++] = new EntryMatch(page, entryIndex);
                    }
                }
            }
        }

        if (appMatches.Length < appEntries.Length)
        {
            appMatches = new bool[appEntries.Length];
        }

        for (var index = 0; index < appEntries.Length; index++)
        {
            appMatches[index] = SettingsSearch.MatchesApp(compare, appEntries[index].Name, filteredQuery);
        }
    }

    private void DrawSearchResults(PhoneTheme theme, float scale)
    {
        var drawn = DrawGroups(theme, scale);
        drawn |= DrawEntryResults(theme, scale);
        drawn |= DrawAppResults(theme, scale);
        if (drawn)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, EmptyStateTop * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var maxWidth = MathF.Max(1f, width - 2f * Metrics.Space.Lg * scale);
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y),
            Loc.T(L.Settings.NoResults), theme.TextMuted, TextStyles.Footnote, maxWidth);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool DrawEntryResults(PhoneTheme theme, float scale)
    {
        if (entryMatchCount == 0)
        {
            return false;
        }

        ImGui.Dummy(new Vector2(0f, CardGap * scale));
        var card = GroupCard.Begin(theme, entryMatchCount);
        for (var index = 0; index < entryMatchCount; index++)
        {
            var match = entryMatches[index];
            var page = match.Page;
            var label = Loc.T(page.Entries[match.EntryIndex].Label);
            if (SettingsRow.Disclosure(card.NextRow(), label, page.Title, theme))
            {
                SettingsHighlight.Begin(label);
                navigator.Open(page);
            }
        }

        card.End();
        return true;
    }

    private bool DrawAppResults(PhoneTheme theme, float scale)
    {
        var entries = apps.Entries;
        var count = AppMatchCount(entries);
        if (count == 0)
        {
            return false;
        }

        ImGui.Dummy(new Vector2(0f, CardGap * scale));
        var card = GroupCard.Begin(theme, count);
        card.SeparatorInset = SettingsRow.AppTileTextInset;
        for (var index = 0; index < entries.Length; index++)
        {
            if (!IsAppMatch(index))
            {
                continue;
            }

            var entry = entries[index];
            if (SettingsRow.AppLink(card.NextRow(), entry.AppId, entry.Accent, entry.Name,
                    AppNotificationSummary.For(configuration, entry), theme))
            {
                navigator.Open(appPages.For(entry));
            }
        }

        card.End();
        return true;
    }

    private bool IsAppMatch(int index) => index < appMatches.Length && appMatches[index];

    private int AppMatchCount(ReadOnlySpan<AppSettingsEntry> entries)
    {
        var count = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (IsAppMatch(index))
            {
                count++;
            }
        }

        return count;
    }

    private void DrawQuickSwitches(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 3);
        card.SeparatorInset = SettingsRow.TileTextInset;
        var doNotDisturb = SettingsRow.Switch(card.NextRow(), FontAwesomeIcon.Moon, DoNotDisturbTint,
            Loc.T(L.Settings.DoNotDisturb), configuration.DoNotDisturb, theme);
        if (doNotDisturb != configuration.DoNotDisturb)
        {
            configuration.DoNotDisturb = doNotDisturb;
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

    private bool DrawGroups(PhoneTheme theme, float scale)
    {
        var drawn = false;
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var pages = groups[groupIndex];
            var groupMatches = matches[groupIndex];
            var count = VisibleCount(pages, groupMatches);
            if (count == 0)
            {
                continue;
            }

            drawn = true;
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            var card = GroupCard.Begin(theme, count);
            card.SeparatorInset = SettingsRow.TileTextInset;
            for (var index = 0; index < pages.Length; index++)
            {
                var page = pages[index];
                if (!groupMatches[index] || page.IsHidden)
                {
                    continue;
                }

                var row = card.NextRow();
                if (page.GuideAnchor is { } anchorKey && IsFullyVisible(row))
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

        return drawn;
    }

    private static bool IsFullyVisible(Rect row)
    {
        var windowTop = ImGui.GetWindowPos().Y;
        var windowBottom = windowTop + ImGui.GetWindowSize().Y;
        return row.Min.Y >= windowTop && row.Max.Y <= windowBottom;
    }

    private static int VisibleCount(ISettingsPage[] pages, bool[] groupMatches)
    {
        var count = 0;
        for (var index = 0; index < pages.Length; index++)
        {
            if (groupMatches[index] && !pages[index].IsHidden)
            {
                count++;
            }
        }

        return count;
    }

    private static void DrawLinks(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 2);
        card.SeparatorInset = SettingsRow.TileTextInset;
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
        var size = Typography.Measure(VersionLabel, TextStyles.Footnote);
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + (available - size.X) * 0.5f, origin.Y),
            VersionLabel, theme.TextMuted, TextStyles.Footnote);
        ImGui.Dummy(new Vector2(available, size.Y));
    }
}
