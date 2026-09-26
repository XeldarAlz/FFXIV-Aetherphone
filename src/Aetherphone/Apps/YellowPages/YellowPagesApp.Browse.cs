using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float SearchRowHeight = 52f;
    private const float SearchPillHeight = 40f;
    private const float RailCardWidth = 156f;
    private const float RailCardHeight = 176f;
    private const float RailCardRounding = 16f;
    private const float IntentTileHeight = 92f;
    private const float IntentTileGap = 8f;
    private const float ScopePillHeight = 28f;
    private const float DirectionRowHeight = 46f;
    private const float LoadMoreHeight = 40f;
    private const double SearchDebounceSeconds = 0.6;
    private const int SectionFallbackRebuildSeconds = 30;
    private const long OpeningSoonLeadSeconds = 8L * 3600L;

    private static readonly TextStyle RailTitleStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle RailMetaStyle = TextStyles.Caption1;
    private static readonly TextStyle IntentLabelStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle ScopePillStyle = TextStyles.FootnoteEmphasized;

    private readonly List<AdDto> openSection = new();
    private readonly List<AdDto> latestSection = new();
    private readonly ChipRail categoryRail = new();
    private readonly string[] chipLabels = new string[AdCategories.Count + 1];
    private readonly bool[] chipActive = new bool[AdCategories.Count + 1];
    private readonly string[] directionLabels = new string[3];
    private AdDto[] lastDirectory = Array.Empty<AdDto>();
    private AdDto[] pendingDirectory = Array.Empty<AdDto>();
    private bool awaitingDirectory;
    private long nextSectionRebuildUnix;
    private string browseSearch = string.Empty;
    private string browseSearchApplied = string.Empty;
    private double browseSearchEditedAt;
    private int railStart;

    private AdCardContext CardContext(long nowUnix) => new(images, nowUnix, configuration.YellowPagesCompactCards);

    private void DrawBrowse(Rect area)
    {
        var scale = UiScale.Current;
        var nowUnix = NowUnix();
        EnsureDirectoryFilter(YellowPagesScreen.Root, 0, AdDirections.Any);
        DrawBrowseTopBar(area);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            DrawSearchRow(scale);
            EnsureBrowseSections(nowUnix);
            if (openSection.Count > 0)
            {
                DrawSectionLabel(Loc.T(L.YellowPages.OpenSection));
                DrawOpenRail(nowUnix, scale);
            }

            DrawSectionLabel(Loc.T(L.YellowPages.BrowseSection));
            DrawIntentRow(scale);
            if (latestSection.Count == 0)
            {
                DrawBrowseEmpty(listRect, scale);
            }
            else
            {
                DrawSectionLabel(Loc.T(L.YellowPages.LatestSection));
                DrawCards(latestSection, nowUnix);
                DrawLoadMore(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawBrowseTopBar(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var wordmark = DisplayName;
        var wordmarkSize = Typography.Measure(wordmark, WordmarkStyle);
        var wordmarkLeft = area.Min.X + CellPadX * scale;
        Typography.Draw(drawList, new Vector2(wordmarkLeft, rowCenterY - wordmarkSize.Y * 0.5f), wordmark,
            Ink.TitleInk, WordmarkStyle);
        if (store.Syncing || store.DirectoryLoading)
        {
            LoadingPulse.Spinner(new Vector2(wordmarkLeft + wordmarkSize.X + 14f * scale, rowCenterY), 7f * scale,
                Ink.Accent);
        }

        if (DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Refresh, Loc.T(L.Common.Refresh)))
        {
            store.SyncNow();
            RefreshBrowse();
        }

        var optionsCenter = SocialChrome.HeaderSlot(area, 1);
        if (DrawHeaderIcon(drawList, optionsCenter, PhoneIcons.AdjustmentsHorizontal, Loc.T(L.YellowPages.OptionsTitle),
                optionsMenu.IsOpenFor("yellowpages.options")))
        {
            optionsMenu.Toggle("yellowpages.options",
                new Rect(optionsCenter - new Vector2(18f * scale, 18f * scale), optionsCenter + new Vector2(18f * scale, 18f * scale)));
        }

        var pillRight = optionsCenter.X - SocialChrome.HeaderIconRadius * scale - 8f * scale;
        DrawScopePill(new Vector2(pillRight, rowCenterY), scale);
    }

    private void DrawScopePill(Vector2 rightCenter, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var label = ScopePillLabel();
        var labelSize = Typography.Measure(label, ScopePillStyle);
        var width = labelSize.X + 30f * scale;
        var half = ScopePillHeight * scale * 0.5f;
        var rect = new Rect(new Vector2(rightCenter.X - width, rightCenter.Y - half),
            new Vector2(rightCenter.X, rightCenter.Y + half));
        UiAnchors.Report("yellowpages.scope", rect);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var open = scopeMenu.IsOpenFor("yellowpages.scope");
        Squircle.Fill(drawList, rect.Min, rect.Max, half,
            ImGui.GetColorU32(open ? Ink.AccentWash : hovered ? Ink.ChipHover : Ink.ChipFill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, half,
            ImGui.GetColorU32(open ? Palette.WithAlpha(Ink.AccentLink, 0.6f) : Ink.ChipStroke), 1f);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 11f * scale, rect.Center.Y - labelSize.Y * 0.5f), label,
            Ink.AccentLink, ScopePillStyle);
        PhoneIcon.Draw(drawList, new Vector2(rect.Max.X - 11f * scale, rect.Center.Y), PhoneIcons.ChevronDown,
            Palette.WithAlpha(Ink.AccentLink, 0.85f), 12f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            scopeMenu.Toggle("yellowpages.scope", rect);
        }
    }

    private string ScopePillLabel()
    {
        var scope = Loc.T(ScopeLabel());
        return configuration.YellowPagesAfterDark ? $"{scope} · {Loc.T(L.YellowPages.AfterDarkChip)}" : scope;
    }

    private LocString ScopeLabel()
    {
        return configuration.YellowPagesScope switch
        {
            AdScopes.DataCenter => L.YellowPages.ScopeMyDc,
            AdScopes.Everywhere => L.YellowPages.ScopeEverywhere,
            _ => L.YellowPages.ScopeRegion,
        };
    }

    private void DrawSearchRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var top = origin.Y + (SearchRowHeight - SearchPillHeight) * scale * 0.5f;
        var searchRect = new Rect(new Vector2(origin.X + pad, top),
            new Vector2(origin.X + width - pad, top + SearchPillHeight * scale));
        UiAnchors.Report("yellowpages.search", searchRect);
        SearchField.Draw(searchRect, "##yellowPagesSearch", Loc.T(L.YellowPages.SearchLabel), ref browseSearch,
            AppPalettes.YellowPages, 60);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, SearchRowHeight * scale));
        if (string.Equals(browseSearch, browseSearchApplied, StringComparison.Ordinal))
        {
            return;
        }

        if (browseSearchEditedAt == 0d)
        {
            browseSearchEditedAt = ImGui.GetTime();
        }

        if (ImGui.GetTime() - browseSearchEditedAt > SearchDebounceSeconds)
        {
            browseSearchApplied = browseSearch;
            browseSearchEditedAt = 0d;
            RefreshCurrentList();
        }
    }

    private void DrawOpenRail(long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var cardWidth = RailCardWidth * scale;
        var cardHeight = RailCardHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var usable = width - pad * 2f;
        var fit = Math.Max(1, (int)((usable + gap) / (cardWidth + gap)));
        if (railStart > Math.Max(0, openSection.Count - fit))
        {
            railStart = Math.Max(0, openSection.Count - fit);
        }

        var shown = Math.Min(fit, openSection.Count - railStart);
        for (var index = 0; index < shown; index++)
        {
            var ad = openSection[railStart + index];
            var min = new Vector2(origin.X + pad + (cardWidth + gap) * index, origin.Y);
            if (DrawRailCard(drawList, ad, min, new Vector2(min.X + cardWidth, min.Y + cardHeight), nowUnix, scale))
            {
                OpenDetail(ad.Id);
            }
        }

        if (railStart > 0)
        {
            DrawRailChevron(drawList, new Vector2(origin.X + pad + 14f * scale, origin.Y + cardHeight * 0.5f),
                PhoneIcons.ChevronLeft, -1, scale);
        }

        if (railStart + fit < openSection.Count)
        {
            DrawRailChevron(drawList, new Vector2(origin.X + width - pad - 14f * scale, origin.Y + cardHeight * 0.5f),
                PhoneIcons.ChevronRight, 1, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Md * scale));
    }

    private bool DrawRailCard(ImDrawListPtr drawList, AdDto ad, Vector2 min, Vector2 max, long nowUnix, float scale)
    {
        var rounding = RailCardRounding * scale;
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("yellowpages.rail." + ad.Id, pressed, 0.97f);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * press;
        min = center - half;
        max = center + half;
        Elevation.Card(drawList, min, max, rounding, scale, hovered ? 0.55f : 0.35f);
        var texture = string.IsNullOrEmpty(ad.MediaUrl) ? null : images.Get(ad.MediaUrl);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            Squircle.FillImage(drawList, min, max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }
        else
        {
            YellowPagesKit.CoverFallback(drawList, min, max, rounding, YellowPagesKit.AccentOf(ad), ad.Category, 1.6f);
        }

        Squircle.FillVerticalGradient(drawList, new Vector2(min.X, max.Y - (max.Y - min.Y) * 0.62f), max, rounding,
            ImGui.GetColorU32(YellowPagesKit.ScrimClear), ImGui.GetColorU32(YellowPagesKit.ScrimDeep));
        if (hovered)
        {
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(Ink.AccentLink, 0.7f)),
                1.2f * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = 12f * scale;
        var state = AdText.OpenState(ad, nowUnix);
        if (state.IsOpen)
        {
            YellowPagesKit.LivePill(drawList, new Vector2(min.X + pad, min.Y + pad), Loc.T(L.YellowPages.OpenNow),
                scale);
        }
        else if (state.NextOpeningUnix > 0)
        {
            YellowPagesKit.Pill(drawList, new Vector2(min.X + pad, min.Y + pad),
                Loc.T(L.YellowPages.OpensAt, TimeText.Clock(state.NextOpeningUnix)), YellowPagesKit.OverlayFill,
                YellowPagesKit.White, scale, PhoneIcons.Clock);
        }

        var textWidth = max.X - min.X - pad * 2f;
        var metaHeight = Typography.LineHeight(RailMetaStyle);
        var titleHeight = Typography.LineHeight(RailTitleStyle);
        var metaTop = max.Y - pad - metaHeight;
        var titleTop = metaTop - titleHeight - 2f * scale;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("yellowpages.rail.title.", ad.Id), ad.Title, min.X + pad, titleTop,
            textWidth, RailTitleStyle, YellowPagesKit.White);
        Typography.Draw(drawList, new Vector2(min.X + pad, metaTop),
            Typography.FitText(AdText.PlaceLine(ad), textWidth, RailMetaStyle), Palette.WithAlpha(YellowPagesKit.White, 0.78f),
            RailMetaStyle);
        return UiInteract.Click(min, max, hovered);
    }

    private void DrawRailChevron(ImDrawListPtr drawList, Vector2 center, string glyph, int step, float scale)
    {
        var radius = 14f * scale;
        var hovered = UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? YellowPagesKit.OverlayHover : YellowPagesKit.OverlayFill), 28);
        PhoneIcon.Draw(drawList, center, glyph, YellowPagesKit.White, 16f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - new Vector2(radius, radius), center + new Vector2(radius, radius), hovered))
        {
            railStart += step;
        }
    }

    private void DrawIntentRow(float scale)
    {
        var intents = AdIntents.All;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var gap = IntentTileGap * scale;
        var tileWidth = (width - pad * 2f - gap * (intents.Length - 1)) / intents.Length;
        var tileHeight = IntentTileHeight * scale;
        for (var index = 0; index < intents.Length; index++)
        {
            var min = new Vector2(origin.X + pad + (tileWidth + gap) * index, origin.Y);
            var max = new Vector2(min.X + tileWidth, min.Y + tileHeight);
            if (DrawIntentTile(drawList, intents[index], min, max, scale))
            {
                OpenIntent(intents[index]);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tileHeight + Metrics.Space.Md * scale));
    }

    private bool DrawIntentTile(ImDrawListPtr drawList, int intent, Vector2 min, Vector2 max, float scale)
    {
        var rounding = 16f * scale;
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("yellowpages.intent." + intent, pressed, 0.96f);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * press;
        min = center - half;
        max = center + half;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(hovered ? Ink.ChipHover : Ink.ChipFill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Ink.ChipStroke), 1f);
        var tint = intent == AdIntents.Wanted ? YellowPagesKit.WantedTint : Ink.Accent;
        var glyphCenter = new Vector2(center.X, min.Y + 30f * scale);
        var glyphRadius = 17f * scale;
        AccentGloss.Circle(drawList, glyphCenter, glyphRadius, Palette.Lighten(tint, 0.18f), Palette.Darken(tint, 0.2f),
            scale, hovered ? 0.9f : 0.35f, 0f);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(AdIntents.Icon(intent)), YellowPagesKit.White, 0.82f);
        var label = Loc.T(AdIntents.Label(intent));
        Typography.DrawWrappedCentered(drawList, label, IntentLabelStyle, Ink.TitleInk,
            new Vector2(center.X, glyphCenter.Y + glyphRadius + 8f * scale), max.X - min.X - 10f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void DrawCategory(Rect area, int intent)
    {
        var scale = UiScale.Current;
        var direction = DirectionFor(intent);
        EnsureDirectoryFilter(YellowPagesScreen.Category, IntentMask(intent), direction);
        var pillReserve = Typography.Measure(ScopePillLabel(), ScopePillStyle).X / scale + 38f;
        SocialChrome.DrawScreenHeader(area, Loc.T(AdIntents.Label(intent)), Ink, back, ScreenTitleStyle, pillReserve,
            string.Empty, true, false);
        DrawScopePill(new Vector2(area.Max.X - CellPadX * scale, area.Min.Y + AppHeader.Height * scale * 0.5f), scale);
        DrawHairline(ImGui.GetWindowDrawList(), area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var loading = DirectoryStale();
        var directory = loading ? Array.Empty<AdDto>() : store.Directory;
        var nowUnix = NowUnix();
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawCategoryChips(intent, scale);
            if (AdIntents.SupportsDirection(intent))
            {
                DrawDirectionRow(intent, scale);
            }

            if (directory.Length == 0)
            {
                DrawIntentEmpty(listRect, loading, scale);
            }
            else
            {
                var context = CardContext(nowUnix);
                for (var index = 0; index < directory.Length; index++)
                {
                    if (AdCard.Draw(directory[index], context))
                    {
                        OpenDetail(directory[index].Id);
                    }
                }

                DrawLoadMore(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawCategoryChips(int intent, float scale)
    {
        var categories = AdCategories.ForIntent(intent);
        var intentMask = AdCategories.MaskFor(categories);
        var mask = configuration.YellowPagesCategoryFilter;
        chipLabels[0] = Loc.T(L.YellowPages.FilterAll);
        chipActive[0] = (mask & intentMask) == 0;
        for (var index = 0; index < categories.Length; index++)
        {
            chipLabels[index + 1] = Loc.T(AdCategories.Label(categories[index]));
            chipActive[index + 1] = (mask & (1 << categories[index])) != 0;
        }

        var count = categories.Length + 1;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var row = new Rect(new Vector2(origin.X + CellPadX * scale, origin.Y),
            new Vector2(origin.X + width - CellPadX * scale, origin.Y + ChipRail.RowHeight * scale));
        var tapped = categoryRail.Draw(row, ui, chipLabels.AsSpan(0, count), chipActive.AsSpan(0, count));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ChipRail.RowHeight * scale + Metrics.Space.Sm * scale));
        if (tapped < 0)
        {
            return;
        }

        configuration.YellowPagesCategoryFilter = tapped == 0
            ? mask & ~intentMask
            : mask ^ (1 << categories[tapped - 1]);
        configuration.Save();
        RefreshIntent(intent);
    }

    private void DrawDirectionRow(int intent, float scale)
    {
        directionLabels[0] = Loc.T(L.YellowPages.DirectionAll);
        directionLabels[1] = Loc.T(L.YellowPages.DirectionOffers);
        directionLabels[2] = Loc.T(L.YellowPages.DirectionWanted);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var row = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + DirectionRowHeight * scale));
        var picked = SegmentStrip.Draw("yellowpages.direction", row, directionLabels, configuration.YellowPagesDirection,
            AppPalettes.YellowPages, 32f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, DirectionRowHeight * scale));
        if (picked < 0 || picked == configuration.YellowPagesDirection)
        {
            return;
        }

        configuration.YellowPagesDirection = picked;
        configuration.Save();
        RefreshIntent(intent);
    }

    private void DrawCards(List<AdDto> items, long nowUnix)
    {
        var context = CardContext(nowUnix);
        for (var index = 0; index < items.Count; index++)
        {
            if (AdCard.Draw(items[index], context))
            {
                OpenDetail(items[index].Id);
            }
        }
    }

    private void DrawLoadMore(float scale)
    {
        if (!store.DirectoryHasMore)
        {
            return;
        }

        if (!store.DirectoryLoadingMore && InfiniteScroll.ReachedBottom())
        {
            store.LoadMoreDirectory();
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = LoadMoreHeight * scale;
        if (store.DirectoryLoadingMore)
        {
            LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), 9f * scale,
                Ink.Accent);
        }
        else
        {
            var label = Loc.T(L.YellowPages.LoadMore);
            var buttonWidth = Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + 44f * scale;
            var rect = new Rect(new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y + 6f * scale),
                new Vector2(origin.X + (width + buttonWidth) * 0.5f, origin.Y + height - 6f * scale));
            if (SocialPill.Flat(ImGui.GetWindowDrawList(), rect, label, Ink.ChipFill, Ink.ChipHover, Ink.ChipStroke,
                    Ink.TitleInk, TextStyles.SubheadlineEmphasized, rect.Height * 0.5f))
            {
                store.LoadMoreDirectory();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawIntentEmpty(Rect listRect, bool loading, float scale)
    {
        if (!loading)
        {
            DrawBrowseEmpty(listRect, scale);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        LoadingPulse.Draw(new Vector2(listRect.Center.X, origin.Y + 60f * scale), 13f * scale, Ink.Accent,
            Ink.MutedInk, Loc.T(L.Common.Loading));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ScrollLayout.StableContentWidth(), 130f * scale));
    }

    private void DrawBrowseEmpty(Rect listRect, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var area = new Rect(new Vector2(listRect.Min.X, origin.Y), listRect.Max);
        if (store.DirectoryLoading && !store.DirectoryLoadedOnce)
        {
            LoadingPulse.Draw(new Vector2(listRect.Center.X, origin.Y + 60f * scale), 13f * scale, Ink.Accent,
                Ink.MutedInk, Loc.T(L.Common.Loading));
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, 130f * scale));
            return;
        }

        if (store.DirectoryFailed)
        {
            DrawEmptyState(area, Loc.T(L.Common.LoadFailed), Loc.T(L.Common.LoadFailedHint));
            var retryLabel = Loc.T(L.Common.Retry);
            var retryWidth = Typography.Measure(retryLabel, TextStyles.SubheadlineEmphasized).X + 44f * scale;
            var retryRect = new Rect(new Vector2(listRect.Center.X - retryWidth * 0.5f, origin.Y + 150f * scale),
                new Vector2(listRect.Center.X + retryWidth * 0.5f, origin.Y + 186f * scale));
            if (SocialPill.Accent(ImGui.GetWindowDrawList(), retryRect, retryLabel, Ink, TextStyles.SubheadlineEmphasized,
                    retryRect.Height * 0.5f))
            {
                RefreshCurrentList();
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, 210f * scale));
            return;
        }

        var searching = browseSearchApplied.Length > 0;
        DrawEmptyState(area, Loc.T(searching ? L.YellowPages.NoResultsTitle : L.YellowPages.EmptyTitle),
            Loc.T(searching ? L.YellowPages.NoResultsHint : L.YellowPages.EmptyHint));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 200f * scale));
    }

    private void OpenIntent(int intent)
    {
        railStart = 0;
        categoryRail.Reset();
        RefreshIntent(intent);
        router.Push(YellowPagesRoute.ForIntent(intent));
    }

    private void RefreshBrowse()
    {
        BeginDirectorySwap();
        store.RefreshDirectory(0, false, browseSearchApplied, AdDirections.Any);
    }

    private void RefreshIntent(int intent)
    {
        BeginDirectorySwap();
        store.RefreshDirectory(IntentMask(intent), false, browseSearchApplied, DirectionFor(intent));
    }

    private int DirectionFor(int intent)
    {
        if (intent == AdIntents.Wanted)
        {
            return AdDirections.Wanted;
        }

        return AdIntents.SupportsDirection(intent) ? configuration.YellowPagesDirection : AdDirections.Any;
    }

    private int IntentMask(int intent)
    {
        var whole = AdCategories.MaskFor(AdCategories.ForIntent(intent));
        var picked = configuration.YellowPagesCategoryFilter & whole;
        return picked != 0 ? picked : whole;
    }

    private void EnsureDirectoryFilter(YellowPagesScreen screen, int wantedMask, int wantedDirection)
    {
        if (router.Current.Screen != screen || router.IsTransitioning || store.DirectoryLoading
            || (store.DirectoryCategories == wantedMask && store.DirectoryDirection == wantedDirection))
        {
            return;
        }

        if (screen == YellowPagesScreen.Category)
        {
            RefreshIntent(router.Current.Intent);
            return;
        }

        RefreshBrowse();
    }

    private void BeginDirectorySwap()
    {
        pendingDirectory = store.Directory;
        awaitingDirectory = true;
    }

    private bool DirectoryStale()
    {
        if (!awaitingDirectory)
        {
            return false;
        }

        if (store.DirectoryLoading && ReferenceEquals(store.Directory, pendingDirectory))
        {
            return true;
        }

        awaitingDirectory = false;
        pendingDirectory = Array.Empty<AdDto>();
        return false;
    }

    private void ToggleAfterDark()
    {
        if (configuration.YellowPagesAfterDark)
        {
            configuration.YellowPagesAfterDark = false;
            configuration.Save();
            RefreshCurrentList();
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.YellowPages.AfterDarkConfirmTitle),
            Message = Loc.T(L.YellowPages.AfterDarkConfirmBody),
            ConfirmLabel = Loc.T(L.YellowPages.AfterDarkConfirmYes),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Common.Loading),
            FailedMessage = string.Empty,
            ConfirmAsync = done =>
            {
                configuration.YellowPagesAfterDark = true;
                configuration.Save();
                RefreshCurrentList();
                done(true);
            },
        });
    }

    private void EnsureBrowseSections(long nowUnix)
    {
        if (DirectoryStale())
        {
            return;
        }

        var directory = store.Directory;
        if (ReferenceEquals(directory, lastDirectory) && nowUnix < nextSectionRebuildUnix)
        {
            return;
        }

        lastDirectory = directory;
        openSection.Clear();
        latestSection.Clear();
        railStart = 0;
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
                    if (state.NextOpeningUnix - nowUnix <= OpeningSoonLeadSeconds)
                    {
                        openSection.Add(ad);
                        continue;
                    }
                }
            }

            latestSection.Add(ad);
        }

        ApplySort(latestSection, nowUnix);
        nextSectionRebuildUnix = nextBoundary == long.MaxValue
            ? nowUnix + SectionFallbackRebuildSeconds
            : Math.Min(nextBoundary, nowUnix + SectionFallbackRebuildSeconds);
    }

    private void ApplySort(List<AdDto> items, long nowUnix)
    {
        switch (configuration.YellowPagesSort)
        {
            case AdSorts.EndingSoon:
                items.Sort(static (left, right) => left.ExpiresAtUnix.CompareTo(right.ExpiresAtUnix));
                break;
            case AdSorts.OpenFirst:
                items.Sort((left, right) =>
                {
                    var leftOpen = AdText.OpenState(left, nowUnix).IsOpen ? 0 : 1;
                    var rightOpen = AdText.OpenState(right, nowUnix).IsOpen ? 0 : 1;
                    var byOpen = leftOpen.CompareTo(rightOpen);
                    return byOpen != 0 ? byOpen : right.RenewedAtUnix.CompareTo(left.RenewedAtUnix);
                });
                break;
        }
    }
}
