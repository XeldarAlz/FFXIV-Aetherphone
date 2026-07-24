using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float ControlRowHeight = 42f;
    private const float PinnedCardHeight = 58f;
    private const float ChipHeight = 32f;
    private const double SearchDebounceSeconds = 0.6;
    private const int SectionFallbackRebuildSeconds = 30;

    private readonly List<AdDto> openSection = new();
    private readonly List<AdDto> latestSection = new();
    private readonly string[] chipLabels = new string[AdCategories.Count];
    private readonly bool[] chipActive = new bool[AdCategories.Count];
    private readonly string[] scopeLabels = new string[3];
    private AdDto[] lastDirectory = Array.Empty<AdDto>();
    private long nextSectionRebuildUnix;
    private string browseSearch = string.Empty;
    private string browseSearchApplied = string.Empty;
    private double browseSearchEditedAt;
    private bool browseOpenNow;

    private void DrawBrowse(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var nowUnix = NowUnix();
        DrawBrowseHeader(area, scale);
        var controlsTop = area.Min.Y + AppHeader.Height * scale;
        DrawScopeRow(area, controlsTop, scale);
        var body = new Rect(new Vector2(area.Min.X, controlsTop + ControlRowHeight * scale), area.Max);
        EnsureBrowseSections(nowUnix);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawSearchField(scale);
            if (store.Mine.Length > 0)
            {
                DrawMinePinned(scale);
            }

            DrawCategoryFilters(scale);
            if (openSection.Count > 0)
            {
                ui.SectionHeading(Loc.T(L.YellowPages.OpenSection), 6f);
                DrawCards(openSection, nowUnix, scale);
            }

            if (latestSection.Count == 0)
            {
                DrawBrowseEmpty(body, scale);
            }
            else
            {
                ui.SectionHeading(Loc.T(L.YellowPages.LatestSection), 6f);
                DrawCards(latestSection, nowUnix, scale);
                DrawLoadMore(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }

        if (store.LiveMineCount < 3 && ComposeFab.Draw(body, "##yellowPagesPostFab", ui.Accent,
                FontAwesomeIcon.Plus.ToIconString(), Loc.T(L.YellowPages.PostAd)))
        {
            ResetComposeForm();
            router.Push(YellowPagesRoute.Compose);
        }
    }

    private void DrawBrowseHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName,
            AppPalettes.YellowPages.TitleInk, 1.3f, FontWeight.Bold);
        if (ui.IconButton(new Vector2(area.Min.X + 22f * scale, rowCenterY), 14f * scale,
                FontAwesomeIcon.Heart.ToIconString(), AppPalettes.YellowPages.BodyInk, AppSkin.Transparent, 0.85f))
        {
            store.RefreshSaved();
            router.Push(YellowPagesRoute.Saved);
        }

        var actionCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (store.Syncing || store.DirectoryLoading)
        {
            LoadingPulse.Spinner(actionCenter, 8f * scale, ui.Accent);
            return;
        }

        if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(),
                AppPalettes.YellowPages.BodyInk, AppSkin.Transparent, 0.9f))
        {
            store.SyncNow();
            RefreshBrowse();
        }
    }

    private void DrawScopeRow(Rect area, float top, float scale)
    {
        var inset = 16f * scale;
        var row = new Rect(new Vector2(area.Min.X + inset, top),
            new Vector2(area.Max.X - inset, top + ControlRowHeight * scale));
        scopeLabels[0] = Loc.T(L.YellowPages.ScopeRegion);
        scopeLabels[1] = Loc.T(L.YellowPages.ScopeMyDc);
        scopeLabels[2] = Loc.T(L.YellowPages.ScopeEverywhere);
        var selected = Math.Clamp(configuration.YellowPagesScope, AdScopes.Region, AdScopes.Everywhere);
        var next = SegmentStrip.Draw("##yellowPagesScope", row, scopeLabels, selected, AppPalettes.YellowPages);
        if (next != selected)
        {
            configuration.YellowPagesScope = next;
            configuration.Save();
            RefreshBrowse();
        }
    }

    private void DrawSearchField(float scale)
    {
        ui.Field(Loc.T(L.YellowPages.SearchLabel), "##yellowPagesSearch", ref browseSearch, 60, false);
        if (!string.Equals(browseSearch, browseSearchApplied, StringComparison.Ordinal))
        {
            if (browseSearchEditedAt == 0d)
            {
                browseSearchEditedAt = ImGui.GetTime();
            }

            if (ImGui.GetTime() - browseSearchEditedAt > SearchDebounceSeconds)
            {
                browseSearchApplied = browseSearch;
                browseSearchEditedAt = 0d;
                RefreshBrowse();
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void DrawMinePinned(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PinnedCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Lg * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var tileSide = 32f * scale;
        var tileCenter = new Vector2(card.Min.X + 14f * scale + tileSide * 0.5f, card.Center.Y);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), FontAwesomeIcon.Bullhorn);
        var textLeft = tileCenter.X + tileSide * 0.5f + 12f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 10f * scale), Loc.T(L.YellowPages.YourAds),
            AppPalettes.YellowPages.TitleInk, TextStyles.Headline);
        var summary = Loc.T(L.YellowPages.YourAdsCount, store.LiveMineCount);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 31f * scale), summary, ui.Accent,
            TextStyles.FootnoteEmphasized);
        AppSkin.Icon(drawList, new Vector2(card.Max.X - 20f * scale, card.Center.Y),
            FontAwesomeIcon.ChevronRight.ToIconString(), AppPalettes.YellowPages.MutedInk, 0.7f);
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            router.Push(YellowPagesRoute.Mine);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawCategoryFilters(float scale)
    {
        var intents = AdIntents.All;
        for (var intentIndex = 0; intentIndex < intents.Length; intentIndex++)
        {
            var categories = AdCategories.ForIntent(intents[intentIndex]);
            ui.SectionLabel(Loc.T(AdIntents.Label(intents[intentIndex])));
            for (var index = 0; index < categories.Length; index++)
            {
                chipLabels[index] = Loc.T(AdCategories.Label(categories[index]));
                chipActive[index] = (configuration.YellowPagesCategoryFilter & (1 << categories[index])) != 0;
            }

            var tapped = DrawChipFlow(categories.Length, scale);
            if (tapped >= 0)
            {
                configuration.YellowPagesCategoryFilter ^= 1 << categories[tapped];
                configuration.Save();
                RefreshBrowse();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    private int DrawChipFlow(int count, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var gap = Metrics.Space.Sm * scale;
        var chipHeight = ChipHeight * scale;
        var lineAdvance = chipHeight + gap;
        var cursorX = origin.X;
        var lineTop = origin.Y;
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var label = chipLabels[index];
            var chipWidth = Typography.Measure(label, 0.85f, FontWeight.Medium).X + 26f * scale;
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                lineTop += lineAdvance;
            }

            var centerY = lineTop + chipHeight * 0.5f;
            if (ui.FlowChip(ref cursorX, centerY, gap, label, chipActive[index]))
            {
                tapped = index;
            }
        }

        var totalHeight = lineTop + chipHeight - origin.Y;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight));
        return tapped;
    }

    private void DrawCards(List<AdDto> items, long nowUnix, float scale)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var ad = items[index];
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = AdCard.Height(ad, width, scale);
            var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            if (ImGui.IsRectVisible(card.Min, card.Max)
                && AdCard.Draw(card, ad, images, lodestone, theme, ui, nowUnix))
            {
                OpenDetail(ad.Id);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + AdCard.Gap * scale));
        }
    }

    private void DrawLoadMore(float scale)
    {
        if (!store.DirectoryHasMore)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 36f * scale;
        if (store.DirectoryLoadingMore)
        {
            LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), 9f * scale,
                ui.Accent);
        }
        else
        {
            var label = Loc.T(L.YellowPages.LoadMore);
            var buttonWidth = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 44f * scale;
            var rect = new Rect(new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y),
                new Vector2(origin.X + (width + buttonWidth) * 0.5f, origin.Y + height));
            if (ui.GhostButton(rect, label))
            {
                store.LoadMoreDirectory();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawBrowseEmpty(Rect body, float scale)
    {
        if (store.DirectoryLoading && !store.DirectoryLoadedOnce)
        {
            var origin = ImGui.GetCursorScreenPos();
            LoadingPulse.Draw(new Vector2(body.Center.X, origin.Y + 90f * scale), 13f * scale, ui.Accent,
                AppPalettes.YellowPages.MutedInk, Loc.T(L.Common.Loading));
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 160f * scale));
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.YellowPages.EmptyTitle),
            Loc.T(L.YellowPages.EmptyHint));
    }

    private void EnsureBrowseSections(long nowUnix)
    {
        var directory = store.Directory;
        if (ReferenceEquals(directory, lastDirectory) && nowUnix < nextSectionRebuildUnix)
        {
            return;
        }

        lastDirectory = directory;
        openSection.Clear();
        latestSection.Clear();
        var nextBoundary = long.MaxValue;
        for (var index = 0; index < directory.Length; index++)
        {
            var ad = directory[index];
            if (ad.Archetype == AdArchetypes.Place)
            {
                var state = AdText.OpenState(ad, nowUnix);
                if (state.IsOpen)
                {
                    openSection.Add(ad);
                    if (state.ClosesAtUnix > 0)
                    {
                        nextBoundary = Math.Min(nextBoundary, state.ClosesAtUnix);
                    }

                    continue;
                }

                if (state.NextOpeningUnix > 0)
                {
                    nextBoundary = Math.Min(nextBoundary, state.NextOpeningUnix);
                }
            }

            latestSection.Add(ad);
        }

        nextSectionRebuildUnix = nextBoundary == long.MaxValue
            ? nowUnix + SectionFallbackRebuildSeconds
            : Math.Min(nextBoundary, nowUnix + SectionFallbackRebuildSeconds);
    }
}
