using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.NotificationApps);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Th;
    public Vector4 Tint => SlateTint;
    public ReadOnlySpan<SettingsEntry> Entries => AppSettingsPage.Searchable;
    private const float SearchTopGap = Metrics.Space.Sm;
    private const float ListGap = 18f;
    private const float EmptyStateTop = 40f;
    private static readonly Vector4 SlateTint = new(0.44f, 0.50f, 0.58f, 1f);
    private readonly InstalledAppList apps;
    private readonly AppSettingsPages pages;
    private readonly ISettingsNavigator navigator;
    private readonly Configuration configuration;
    private bool[] matches = Array.Empty<bool>();
    private string query = string.Empty;
    private string filteredQuery = string.Empty;
    private LanguageInfo? filteredLanguage;
    private int filteredRevision = -1;

    public AppsPage(InstalledAppList apps, AppSettingsPages pages, ISettingsNavigator navigator,
        Configuration configuration)
    {
        this.apps = apps;
        this.pages = pages;
        this.navigator = navigator;
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, SearchTopGap * scale));
            SettingsSearchField.Draw("##appsSearch", Loc.T(L.Common.Search), ref query, theme, scale);
            RefreshFilter();
            ImGui.Dummy(new Vector2(0f, ListGap * scale));
            var entries = apps.Entries;
            var count = VisibleCount(entries);
            if (count == 0)
            {
                DrawEmptyState(theme, scale);
                return;
            }

            var card = GroupCard.Begin(theme, count);
            card.SeparatorInset = SettingsRow.AppTileTextInset;
            for (var index = 0; index < entries.Length; index++)
            {
                if (!IsVisible(entries, index))
                {
                    continue;
                }

                var entry = entries[index];
                if (SettingsRow.AppLink(card.NextRow(), entry.AppId, entry.Accent, entry.Name,
                        AppNotificationSummary.For(configuration, entry), theme))
                {
                    navigator.Open(pages.For(entry));
                }
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void RefreshFilter()
    {
        var entries = apps.Entries;
        var trimmed = query.AsSpan().Trim();
        if (filteredRevision == apps.Revision && ReferenceEquals(filteredLanguage, Loc.Current) &&
            trimmed.SequenceEqual(filteredQuery))
        {
            return;
        }

        filteredRevision = apps.Revision;
        filteredLanguage = Loc.Current;
        filteredQuery = trimmed.Length == query.Length ? query : trimmed.ToString();
        if (matches.Length < entries.Length)
        {
            matches = new bool[entries.Length];
        }

        var compare = Loc.Culture.CompareInfo;
        for (var index = 0; index < entries.Length; index++)
        {
            matches[index] = SettingsSearch.Matches(compare, entries[index].Name, filteredQuery);
        }
    }

    private bool IsVisible(ReadOnlySpan<AppSettingsEntry> entries, int index) =>
        index < matches.Length && matches[index] && apps.IsInstalled(entries[index].AppId);

    private int VisibleCount(ReadOnlySpan<AppSettingsEntry> entries)
    {
        var count = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (IsVisible(entries, index))
            {
                count++;
            }
        }

        return count;
    }

    private static void DrawEmptyState(PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, EmptyStateTop * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var maxWidth = MathF.Max(1f, width - 2f * Metrics.Space.Lg * scale);
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y),
            Loc.T(L.Spotlight.NoResults), theme.TextMuted, TextStyles.Footnote, maxWidth);
        ImGui.Dummy(new Vector2(width, height));
    }
}
