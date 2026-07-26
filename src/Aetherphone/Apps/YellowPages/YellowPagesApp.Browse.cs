using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
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
    private const float SearchRowHeight = 46f;
    private const float RailCardWidth = 132f;
    private const float RailCardHeight = 148f;
    private const float RailThumbHeight = 78f;
    private const float IntentTileHeight = 104f;
    private const float ScopePillHeight = 26f;
    private const double SearchDebounceSeconds = 0.6;
    private const int SectionFallbackRebuildSeconds = 30;
    private const long OpeningSoonLeadSeconds = 8L * 3600L;

    private readonly List<AdDto> openSection = new();
    private readonly List<AdDto> latestSection = new();
    private readonly ChipRail intentRail = new();
    private readonly string[] chipLabels = new string[AdCategories.Count + 1];
    private readonly bool[] chipActive = new bool[AdCategories.Count + 1];
    private AdDto[] lastDirectory = Array.Empty<AdDto>();
    private AdDto[] pendingDirectory = Array.Empty<AdDto>();
    private bool awaitingDirectory;
    private long nextSectionRebuildUnix;
    private string browseSearch = string.Empty;
    private string browseSearchApplied = string.Empty;
    private double browseSearchEditedAt;
    private int railStart;

    private void DrawBrowse(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var nowUnix = NowUnix();
        EnsureDirectoryFilter(YellowPagesScreen.Browse, 0);
        DrawBrowseHeader(area, scale);
        var controlsTop = area.Min.Y + AppHeader.Height * scale;
        DrawSearchRow(area, controlsTop, scale);
        var body = new Rect(new Vector2(area.Min.X, controlsTop + SearchRowHeight * scale), area.Max);
        EnsureBrowseSections(nowUnix);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            if (openSection.Count > 0)
            {
                ui.SectionHeading(Loc.T(L.YellowPages.OpenSection));
                DrawOpenRail(nowUnix, scale);
            }

            ui.SectionHeading(Loc.T(L.YellowPages.BrowseSection));
            DrawIntentTiles(scale);
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
    }

    private void DrawIntent(Rect area, int intent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        EnsureDirectoryFilter(YellowPagesScreen.Intent, IntentMask(intent));
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(AdIntents.Label(intent)), backToBrowse);
        DrawScopePill(area, scale);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var loading = DirectoryStale();
        var directory = loading ? Array.Empty<AdDto>() : store.Directory;
        var nowUnix = NowUnix();
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawIntentChips(intent, scale);
            if (directory.Length == 0)
            {
                DrawIntentEmpty(body, loading, scale);
            }
            else
            {
                for (var index = 0; index < directory.Length; index++)
                {
                    DrawCard(directory[index], nowUnix, scale);
                }

                DrawLoadMore(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawBrowseHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var inset = 16f * scale;
        var actionCenter = new Vector2(area.Max.X - inset - 11f * scale, rowCenterY);
        if (store.Syncing || store.DirectoryLoading)
        {
            LoadingPulse.Spinner(actionCenter, 8f * scale, ui.Accent);
        }
        else if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(),
                     AppPalettes.YellowPages.MutedInk, AppSkin.Transparent, 0.82f, Loc.T(L.Common.Refresh),
                     HoverLabelSide.Below))
        {
            store.SyncNow();
            RefreshBrowse();
        }

        var rulesCenter = new Vector2(actionCenter.X - 26f * scale, rowCenterY);
        if (ui.IconButton(rulesCenter, 14f * scale, FontAwesomeIcon.QuestionCircle.ToIconString(),
                AppPalettes.YellowPages.MutedInk, AppSkin.Transparent, 0.82f, Loc.T(L.Conduct.Eyebrow),
                HoverLabelSide.Below))
        {
            conduct.ShowRules(Id);
        }

        var pillRect = ScopePillRect(new Vector2(rulesCenter.X - 22f * scale, rowCenterY), scale);
        DrawScopePillAt(pillRect, scale);
        DrawTabTitle(area, DisplayName, area.Max.X - pillRect.Min.X, scale);
    }

    private void DrawTabTitle(Rect area, string title, float rightReserve, float scale)
    {
        var inset = 16f * scale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fitted = Typography.FitText(title, area.Width - inset * 2f - rightReserve, TextStyles.Title2);
        var titleSize = Typography.Measure(fitted, TextStyles.Title2);
        Typography.Draw(new Vector2(area.Min.X + inset, rowCenterY - titleSize.Y * 0.5f), fitted,
            AppPalettes.YellowPages.TitleInk, TextStyles.Title2);
    }

    private void DrawScopePill(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var rect = ScopePillRect(new Vector2(area.Max.X - 16f * scale, rowCenterY), scale);
        DrawScopePillAt(rect, scale);
    }

    private Rect ScopePillRect(Vector2 rightCenter, float scale)
    {
        var width = Typography.Measure(ScopePillLabel(), TextStyles.FootnoteEmphasized).X + 34f * scale;
        var half = ScopePillHeight * scale * 0.5f;
        return new Rect(new Vector2(rightCenter.X - width, rightCenter.Y - half),
            new Vector2(rightCenter.X, rightCenter.Y + half));
    }

    private void DrawScopePillAt(Rect rect, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var label = ScopePillLabel();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(hovered ? ui.HoverTint : AppPalettes.YellowPages.FieldSurface));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.45f)), 1f);
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 12f * scale, rect.Center.Y - labelSize.Y * 0.5f), label,
            ui.Accent, TextStyles.FootnoteEmphasized);
        AppSkin.Icon(drawList, new Vector2(rect.Max.X - 11f * scale, rect.Center.Y),
            FontAwesomeIcon.CaretDown.ToIconString(), Palette.WithAlpha(ui.Accent, 0.8f), 0.7f);
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

    private void DrawSearchRow(Rect area, float top, float scale)
    {
        var inset = 16f * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + inset, top + 4f * scale),
            new Vector2(area.Max.X - inset, top + (SearchRowHeight - 6f) * scale));
        SearchField.Draw(searchRect, "##yellowPagesSearch", Loc.T(L.YellowPages.SearchLabel), ref browseSearch,
            AppPalettes.YellowPages, 60);
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
            RefreshBrowse();
        }
    }

    private Core.Localization.LocString ScopeLabel()
    {
        return configuration.YellowPagesScope switch
        {
            AdScopes.DataCenter => L.YellowPages.ScopeMyDc,
            AdScopes.Everywhere => L.YellowPages.ScopeEverywhere,
            _ => L.YellowPages.ScopeRegion,
        };
    }

    private void DrawOpenRail(long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cardWidth = RailCardWidth * scale;
        var cardHeight = RailCardHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var fit = Math.Max(1, (int)((width + gap) / (cardWidth + gap)));
        if (railStart > Math.Max(0, openSection.Count - fit))
        {
            railStart = Math.Max(0, openSection.Count - fit);
        }

        var shown = Math.Min(fit, openSection.Count - railStart);
        for (var index = 0; index < shown; index++)
        {
            var ad = openSection[railStart + index];
            var min = new Vector2(origin.X + (cardWidth + gap) * index, origin.Y);
            if (DrawRailCard(drawList, ad, min, new Vector2(min.X + cardWidth, min.Y + cardHeight), nowUnix, scale))
            {
                OpenDetail(ad.Id);
            }
        }

        if (railStart > 0)
        {
            DrawRailChevron(drawList, new Vector2(origin.X + 12f * scale, origin.Y + cardHeight * 0.5f),
                FontAwesomeIcon.ChevronLeft, () => railStart--, scale);
        }

        if (railStart + fit < openSection.Count)
        {
            DrawRailChevron(drawList, new Vector2(origin.X + width - 12f * scale, origin.Y + cardHeight * 0.5f),
                FontAwesomeIcon.ChevronRight, () => railStart++, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Lg * scale));
    }

    private bool DrawRailCard(ImDrawListPtr drawList, AdDto ad, Vector2 min, Vector2 max, long nowUnix, float scale)
    {
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, min, max, rounding, elevated: true);
        var thumbMax = new Vector2(max.X, min.Y + RailThumbHeight * scale);
        var thumb = string.IsNullOrEmpty(ad.MediaUrl) ? null : images.Get(ad.MediaUrl);
        if (thumb is not null)
        {
            var (uv0, uv1) = ImageFit.Cover(thumb.Size.X, thumb.Size.Y, max.X - min.X, thumbMax.Y - min.Y);
            drawList.AddImageRounded(thumb.Handle, min, thumbMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersTop);
        }
        else
        {
            Squircle.FillVerticalGradient(drawList, min, thumbMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.26f)),
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
            AppSkin.Icon(drawList, (min + thumbMax) * 0.5f, AdCategories.Icon(ad.Category).ToIconString(),
                Palette.WithAlpha(ui.Accent, 0.85f), 1f);
        }

        var pad = 10f * scale;
        var name = Typography.FitText(ad.Title, max.X - min.X - pad * 2f, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + pad, thumbMax.Y + 9f * scale), name,
            AppPalettes.YellowPages.TitleInk, TextStyles.SubheadlineEmphasized);
        var place = Typography.FitText(AdText.PlaceLine(ad), max.X - min.X - pad * 2f, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(min.X + pad, thumbMax.Y + 28f * scale), place,
            AppPalettes.YellowPages.MutedInk, TextStyles.Caption1);
        var state = AdText.OpenState(ad, nowUnix);
        var statusTop = thumbMax.Y + 46f * scale;
        if (state.IsOpen)
        {
            DrawPill(drawList, new Vector2(min.X + pad, statusTop), Loc.T(L.YellowPages.OpenNow), AdCard.OpenGreen,
                new Vector4(0.03f, 0.08f, 0.05f, 1f), scale);
        }
        else if (state.NextOpeningUnix > 0)
        {
            DrawPill(drawList, new Vector2(min.X + pad, statusTop),
                Loc.T(L.YellowPages.OpensAt, TimeText.Clock(state.NextOpeningUnix)),
                Palette.WithAlpha(ui.Accent, 0.20f), ui.Accent, scale);
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, min, max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static float DrawPill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 fill, Vector4 ink,
        float scale) =>
        DrawPill(drawList, topLeft, label, fill, ink, TextStyles.Caption1, scale);

    private static float DrawPill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 fill, Vector4 ink,
        in TextStyle style, float scale)
    {
        var labelSize = Typography.Measure(label, style);
        var max = topLeft + labelSize + new Vector2(18f * scale, 8f * scale);
        Squircle.Fill(drawList, topLeft, max, (max.Y - topLeft.Y) * 0.5f, ImGui.GetColorU32(fill));
        Typography.Draw(drawList, topLeft + new Vector2(9f * scale, 4f * scale), label, ink, style);
        return max.X - topLeft.X;
    }

    private void DrawRailChevron(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Action step,
        float scale)
    {
        var radius = 12f * scale;
        var hovered = UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.72f : 0.55f)), 28);
        AppSkin.Icon(drawList, center, icon.ToIconString(), new Vector4(1f, 1f, 1f, 0.92f), 0.62f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - new Vector2(radius, radius), center + new Vector2(radius, radius), hovered))
        {
            step();
        }
    }

    private void DrawIntentTiles(float scale)
    {
        var intents = AdIntents.All;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = Metrics.Space.Sm * scale;
        var tileWidth = (width - gap * (intents.Length - 1)) / intents.Length;
        var tileHeight = IntentTileHeight * scale;
        for (var index = 0; index < intents.Length; index++)
        {
            var min = new Vector2(origin.X + (tileWidth + gap) * index, origin.Y);
            var max = new Vector2(min.X + tileWidth, min.Y + tileHeight);
            if (DrawIntentTile(drawList, intents[index], min, max, tileWidth, scale))
            {
                OpenIntent(intents[index]);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tileHeight + Metrics.Space.Lg * scale));
    }

    private bool DrawIntentTile(ImDrawListPtr drawList, int intent, Vector2 min, Vector2 max, float tileWidth,
        float scale)
    {
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, min, max, rounding, elevated: true);
        var glyphCenter = new Vector2((min.X + max.X) * 0.5f, min.Y + 27f * scale);
        drawList.AddCircleFilled(glyphCenter, 17f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.18f)), 32);
        AppSkin.Icon(drawList, glyphCenter, IntentIcon(intent).ToIconString(), ui.Accent, 0.92f);
        var textWidth = tileWidth - 12f * scale;
        Typography.DrawWrappedCentered(drawList, Loc.T(AdIntents.Label(intent)), TextStyles.FootnoteEmphasized,
            AppPalettes.YellowPages.TitleInk, new Vector2(glyphCenter.X, min.Y + 50f * scale), textWidth);
        var count = Loc.T(L.YellowPages.IntentCategories, AdCategories.ForIntent(intent).Length);
        Typography.DrawCentered(drawList, new Vector2(glyphCenter.X, max.Y - 15f * scale), count,
            AppPalettes.YellowPages.MutedInk, TextStyles.Caption1);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, min, max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static FontAwesomeIcon IntentIcon(int intent) =>
        intent switch
        {
            AdIntents.Hire => FontAwesomeIcon.Hammer,
            AdIntents.Join => FontAwesomeIcon.Users,
            _ => FontAwesomeIcon.MapMarkedAlt,
        };

    private void DrawIntentChips(int intent, float scale)
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
        var tapped = intentRail.Draw(ui, chipLabels.AsSpan(0, count), chipActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
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

    private void DrawCards(List<AdDto> items, long nowUnix, float scale)
    {
        for (var index = 0; index < items.Count; index++)
        {
            DrawCard(items[index], nowUnix, scale);
        }
    }

    private void DrawCard(AdDto ad, long nowUnix, float scale)
    {
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

    private void DrawIntentEmpty(Rect body, bool loading, float scale)
    {
        if (!loading)
        {
            DrawBrowseEmpty(body, scale);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        LoadingPulse.Draw(new Vector2(body.Center.X, origin.Y + 60f * scale), 13f * scale, ui.Accent,
            AppPalettes.YellowPages.MutedInk, Loc.T(L.Common.Loading));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 130f * scale));
    }

    private void DrawBrowseEmpty(Rect body, float scale)
    {
        if (store.DirectoryLoading && !store.DirectoryLoadedOnce)
        {
            var origin = ImGui.GetCursorScreenPos();
            LoadingPulse.Draw(new Vector2(body.Center.X, origin.Y + 60f * scale), 13f * scale, ui.Accent,
                AppPalettes.YellowPages.MutedInk, Loc.T(L.Common.Loading));
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 130f * scale));
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.YellowPages.EmptyTitle),
            Loc.T(L.YellowPages.EmptyHint));
    }

    private void OpenIntent(int intent)
    {
        railStart = 0;
        intentRail.Reset();
        RefreshIntent(intent);
        router.Push(YellowPagesRoute.ForIntent(intent));
    }

    private void RefreshBrowse()
    {
        BeginDirectorySwap();
        store.RefreshDirectory(0, false, browseSearchApplied);
    }

    private void RefreshIntent(int intent)
    {
        BeginDirectorySwap();
        store.RefreshDirectory(IntentMask(intent), false, browseSearchApplied);
    }

    private int IntentMask(int intent)
    {
        var whole = AdCategories.MaskFor(AdCategories.ForIntent(intent));
        var picked = configuration.YellowPagesCategoryFilter & whole;
        return picked != 0 ? picked : whole;
    }

    private void EnsureDirectoryFilter(YellowPagesScreen screen, int wanted)
    {
        if (router.Current.Screen != screen || router.IsTransitioning || store.DirectoryLoading
            || store.DirectoryCategories == wanted)
        {
            return;
        }

        if (screen == YellowPagesScreen.Intent)
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

        confirm.Ask(new Core.Confirm.ConfirmRequest
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
        var upcoming = new List<AdDto>();
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
                        upcoming.Add(ad);
                        continue;
                    }
                }
            }

            latestSection.Add(ad);
        }

        openSection.AddRange(upcoming);
        nextSectionRebuildUnix = nextBoundary == long.MaxValue
            ? nowUnix + SectionFallbackRebuildSeconds
            : Math.Min(nextBoundary, nowUnix + SectionFallbackRebuildSeconds);
    }
}
