using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const int SearchMaxLength = 80;
    private const float SearchGap = 12f;
    private const float FeaturedAspect = 0.66f;
    private const float FeaturedMaxHeight = 236f;
    private const float FeaturedPeek = 22f;
    private const float FeaturedGap = 10f;
    private const float DotsHeight = 24f;
    private const float DotRadius = 3.5f;
    private const float DotActiveWidth = 16f;
    private const float DotPitch = 16f;
    private const float AutoAdvanceSeconds = 6f;
    private const float CategoryGap = 10f;
    private const float CategoryAspect = 0.62f;
    private const float CategoryIconRadius = 15f;
    private const float CategoryInset = 12f;
    private const float CategoryRounding = 18f;
    private const float CategoryScrimShare = 0.75f;
    private const float RailGap = 10f;
    private const float DirectoryRowHeight = 58f;
    private const float DirectoryIconSide = 32f;
    private const int MaxRailCards = 12;
    private const int SkeletonRows = 5;

    private static readonly TextStyle CategoryLabelStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle CategoryCountStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 DotIdle = new(1f, 1f, 1f, 0.30f);

    private static readonly LocString[] CategoryLabels =
    {
        L.Venues.CategoryNightclubs, L.Venues.CategoryBars, L.Venues.CategoryCafes, L.Venues.CategoryTaverns,
        L.Venues.CategoryBathHouses, L.Venues.CategoryCasinos, L.Venues.CategoryRoleplay,
        L.Venues.CategoryPhotography,
    };

    private static readonly string[] CategoryGlyphs =
    {
        PhoneIcons.Music, PhoneIcons.GlassCocktail, PhoneIcons.Coffee, PhoneIcons.Beer, PhoneIcons.Bath,
        PhoneIcons.Dice, PhoneIcons.Masks, PhoneIcons.Camera,
    };

    private static readonly Vector4[] CategoryTints =
    {
        new(0.86f, 0.36f, 0.86f, 1f), new(0.95f, 0.60f, 0.24f, 1f), new(0.80f, 0.56f, 0.38f, 1f),
        new(0.88f, 0.74f, 0.30f, 1f), new(0.30f, 0.72f, 0.82f, 1f), new(0.30f, 0.76f, 0.46f, 1f),
        new(0.62f, 0.46f, 0.94f, 1f), new(0.38f, 0.58f, 0.96f, 1f),
    };

    private static readonly string[] CategoryPressIds =
    {
        "venues.category.0", "venues.category.1", "venues.category.2", "venues.category.3",
        "venues.category.4", "venues.category.5", "venues.category.6", "venues.category.7",
    };

    private readonly StridePager featuredPager = new();
    private readonly VenueRail laterRail = new();
    private readonly VenueRail nearRail = new();
    private readonly VenueRail recentRail = new();
    private readonly PullToRefresh discoverRefresh = new();
    private readonly string[] categoryCounts = new string[VenueCategories.Count];
    private readonly string[] categoryFirstLines = new string[VenueCategories.Count];
    private readonly string[] categorySecondLines = new string[VenueCategories.Count];
    private readonly string[] categoryInitials = new string[VenueCategories.Count];
    private float categoryLinesWidth = -1f;
    private int categoryLinesRevision = -1;
    private int categoryLabelRevision;
    private float featuredIdle;
    private string searchQueryText = string.Empty;
    private string liveHeading = string.Empty;
    private string laterHeading = string.Empty;
    private string nearHeading = string.Empty;
    private string directoryLabel = string.Empty;
    private string searchHeading = string.Empty;

    private void ResetDiscover()
    {
        featuredPager.Reset();
        featuredIdle = 0f;
        laterRail.Reset();
        nearRail.Reset();
        recentRail.Reset();
    }

    private void RebuildSectionLabels()
    {
        var culture = Loc.Culture;
        categoryLabelRevision++;
        liveHeading = $"{Loc.T(L.Venues.LiveNowLabel)} · {sections.Live.Count.ToString(culture)}";
        laterHeading = Loc.T(L.Venues.LaterToday);
        nearHeading = Loc.T(L.Venues.NearWorld, CurrentWorld());
        directoryLabel = Loc.T(L.Venues.BrowseAll, venues.Events.Count.ToString("N0", culture));
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            categoryCounts[category] = sections.CategoryCount(category).ToString("N0", culture);
            categoryInitials[category] = sections.CategoryCover(category) is { } cover
                ? VenueLabelCache.InitialOf(cover.Title)
                : string.Empty;
        }
    }

    private void DrawDiscoverTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        railOwnsPointer = false;
        using (UiScope.PushId("venues.discover"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            discoverRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, venues.Busy, ui.MutedInk,
                refreshAction);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawSearchField(drawList, origin, width, scale);
            cursorY = DrawScopeRow(drawList, new Vector2(origin.X, cursorY + SearchGap * scale), width, DisplayName,
                scale);
            if (searchQueryText.Length > 0)
            {
                cursorY = DrawSearchResults(drawList, new Vector2(origin.X, cursorY), width, scale);
            }
            else if (!DrawLoadState(drawList, new Vector2(origin.X, cursorY), width, navBar.Body, scale,
                         out var stateBottom))
            {
                cursorY = DrawDiscoverSections(drawList, new Vector2(origin.X, cursorY), width, scale);
            }
            else
            {
                cursorY = stateBottom;
            }

            VenuesArt.ReserveTo(origin, width, cursorY + VenuesArt.BottomPad * scale);
            if (railOwnsPointer)
            {
                surface.CancelDrag();
            }
        }

        EndRootTitle(in navBar, context, "venues.nav.discover", DisplayName);
    }

    private float DrawDiscoverSections(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var cursorY = origin.Y;
        var title = DisplayName;
        if (sections.Featured.Count > 0)
        {
            cursorY = Section(drawList, origin.X, cursorY, width, sections.FeaturedIsLive ? liveHeading : laterHeading,
                Loc.T(L.Venues.SeeAll), out var seeAll, scale);
            if (seeAll)
            {
                if (sections.FeaturedIsLive)
                {
                    SelectTab(VenueTab.Live);
                }
                else
                {
                    OpenList(VenueListKind.LaterToday, -1, title);
                }
            }

            cursorY = DrawFeatured(drawList, new Vector2(origin.X, cursorY), width, scale);
        }

        cursorY = Section(drawList, origin.X, cursorY, width, Loc.T(L.Venues.Categories), string.Empty, out _, scale);
        cursorY = DrawCategoryGrid(drawList, new Vector2(origin.X, cursorY), width, scale);
        if (sections.LaterRail.Count > 0)
        {
            cursorY = Section(drawList, origin.X, cursorY, width, laterHeading, Loc.T(L.Venues.SeeAll),
                out var seeLater, scale);
            if (seeLater)
            {
                OpenList(VenueListKind.LaterToday, -1, title);
            }

            cursorY = DrawRail(drawList, "##venues.rail.later", laterRail, sections.LaterRail, laterText,
                new Vector2(origin.X, cursorY), width, scale);
        }

        if (sections.NearRail.Count > 0)
        {
            cursorY = Section(drawList, origin.X, cursorY, width, nearHeading, Loc.T(L.Venues.SeeAll), out var seeNear,
                scale);
            if (seeNear)
            {
                OpenList(VenueListKind.NearYou, -1, title);
            }

            cursorY = DrawRail(drawList, "##venues.rail.near", nearRail, sections.NearRail, nearText,
                new Vector2(origin.X, cursorY), width, scale);
        }

        if (sections.Recents.Count > 0)
        {
            cursorY = Section(drawList, origin.X, cursorY, width, Loc.T(L.Venues.RecentlyViewed),
                Loc.T(L.Venues.Clear), out var clear, scale);
            if (clear)
            {
                ClearRecents();
            }

            cursorY = DrawRail(drawList, "##venues.rail.recent", recentRail, sections.Recents, recentText,
                new Vector2(origin.X, cursorY), width, scale);
        }

        return DrawDirectoryRow(drawList, new Vector2(origin.X, cursorY + VenuesArt.SectionGap * scale), width, scale);
    }

    private float Section(ImDrawListPtr drawList, float left, float top, float width, string title, string trailing,
        out bool trailingClicked, float scale)
    {
        var headerTop = top + VenuesArt.SectionGap * scale;
        var height = VenuesArt.SectionHeader(drawList, ui, new Vector2(left, headerTop), width, title, trailing,
            out trailingClicked, scale);
        return headerTop + height + VenuesArt.HeaderGap * scale;
    }

    private float DrawSearchField(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        var before = search;
        GlassField.Search(drawList, field, "##venueSearch", Loc.T(L.Venues.Search), ref search, theme, scale,
            SearchMaxLength, false);
        if (!ReferenceEquals(before, search))
        {
            searchQueryText = search.Trim();
            visibleCards = PageSize;
        }

        return field.Max.Y;
    }

    private float DrawScopeRow(ImDrawListPtr drawList, Vector2 origin, float width, string backTitle, float scale)
    {
        var height = VenuesArt.PillHeight * scale;
        var label = ResolveScope().Label;
        var filtersActive = FiltersActive;
        var filtersText = filtersActive ? FiltersLabel() : string.Empty;
        var filtersWidth = filtersActive
            ? VenuesArt.ActionWidth(filtersText, PhoneIcons.AdjustmentsHorizontal, false, height)
            : 0f;
        var scopeLimit = width - (filtersActive ? filtersWidth + VenuesArt.PillGap * scale : 0f);
        var scopeWidth = MathF.Min(VenuesArt.ActionWidth(label, PhoneIcons.MapPin, true, height), scopeLimit);
        var scopeRect = new Rect(origin, new Vector2(origin.X + scopeWidth, origin.Y + height));
        UiAnchors.Report("venues.scope", scopeRect);
        if (VenuesArt.Action(drawList, ui, scopeRect, ImGui.GetID("venues.scope"), label, PhoneIcons.MapPin, true,
                ButtonStyle.Gray))
        {
            OpenScope(backTitle);
        }

        if (filtersActive)
        {
            var left = scopeRect.Max.X + VenuesArt.PillGap * scale;
            var filtersRect = new Rect(new Vector2(left, origin.Y),
                new Vector2(left + filtersWidth, origin.Y + height));
            if (VenuesArt.Action(drawList, ui, filtersRect, ImGui.GetID("venues.filters.pill"), filtersText,
                    PhoneIcons.AdjustmentsHorizontal, false, ButtonStyle.Prominent))
            {
                OpenFilters(backTitle);
            }
        }

        return scopeRect.Max.Y;
    }

    private bool DrawLoadState(ImDrawListPtr drawList, Vector2 origin, float width, Rect body, float scale,
        out float bottom)
    {
        bottom = origin.Y;
        if (venues.Events.Count > 0)
        {
            return false;
        }

        if (venues.State is VenueState.Loading or VenueState.Idle)
        {
            var top = origin.Y + VenuesArt.SectionGap * scale;
            var rowHeight = VenuesArt.RowHeight * scale;
            var max = new Vector2(origin.X + width, top + rowHeight * SkeletonRows);
            ui.Card(drawList, new Vector2(origin.X, top), max, Metrics.Radius.Grouped * scale);
            var pad = VenuesArt.RowPad * scale;
            for (var rowIndex = 0; rowIndex < SkeletonRows; rowIndex++)
            {
                var rowTop = top + rowIndex * rowHeight;
                Skeleton.Row(drawList,
                    new Rect(new Vector2(origin.X + pad, rowTop), new Vector2(max.X - pad, rowTop + rowHeight)), scale);
            }

            bottom = max.Y;
            return true;
        }

        if (venues.State != VenueState.Failed)
        {
            return false;
        }

        var stateRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + VenuesArt.StateHeight(body, scale)));
        if (VenuesArt.StateScreen(drawList, ui, stateRect, PhoneIcons.World, Loc.T(L.Venues.Failed),
                Loc.T(L.Venues.FailedHint), Loc.T(L.Venues.Retry), scale))
        {
            UiFeedback.Play(UiSound.Refresh);
            refreshAction();
        }

        bottom = stateRect.Max.Y;
        return true;
    }

    private float DrawEmptyState(ImDrawListPtr drawList, Vector2 origin, float width, float height, string glyph,
        string title, string hint, string action, out bool actionClicked, float scale)
    {
        var stateRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        actionClicked = VenuesArt.StateScreen(drawList, ui, stateRect, glyph, title, hint, action, scale);
        return stateRect.Max.Y;
    }

    private float DrawSearchResults(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var nowUtc = DateTime.UtcNow;
        var scope = ResolveScope();
        var key = new VenueQueryKey(venues.Version, VenueTimeFilter.All, configuration.VenueSourceFilter,
            scope.DataCenters, false, favoritesStamp, tagsStamp, searchQueryText, CurrentMinute(nowUtc),
            World: scope.World, HideAdult: configuration.VenueHideAdult);
        if (searchQuery.Update(key, venues.Events, configuration.VenueFavorites, selectedTags, nowUtc))
        {
            searchText.Fill(searchQuery.Feed, nowUtc);
            searchHeading = Loc.T(L.Venues.VenueCount, searchQuery.Feed.Count.ToString("N0", Loc.Culture));
        }

        if (searchQuery.Feed.Count == 0)
        {
            return DrawEmptyState(drawList, origin, width, VenuesArt.StateMinHeight * scale, PhoneIcons.Search,
                Loc.T(L.Venues.NoVenues),
                Loc.T(L.Venues.EmptyHint), string.Empty, out _, scale);
        }

        var cursorY = Section(drawList, origin.X, origin.Y, width, searchHeading, string.Empty, out _, scale);
        var end = Math.Min(searchQuery.Feed.Count, visibleCards);
        cursorY = DrawRowCard(drawList, new Vector2(origin.X, cursorY), width, searchQuery.Feed, searchText, 0, end,
            DisplayName, scale);
        return DrawMoreSpinner(searchQuery.Feed.Count, end, origin.X, cursorY, width, scale);
    }

    private float DrawRowCard(ImDrawListPtr drawList, Vector2 origin, float width, IReadOnlyList<VenueEvent> feed,
        VenueTextList text, int start, int end, string backTitle, float scale)
    {
        var rowHeight = VenuesArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + (end - start) * rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var art = Art;
        for (var index = start; index < end; index++)
        {
            var top = origin.Y + (index - start) * rowHeight;
            var row = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (index == 0)
            {
                UiAnchors.Report("venues.card.first", row);
            }

            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var venue = feed[index];
            HandleCardAction(VenueCard.DrawRow(drawList, ui, row, venue, text[index], IsFavorite(venue.Id), art,
                scale), venue, backTitle);
            if (index < end - 1)
            {
                var hairlineLeft = row.Min.X + (VenuesArt.RowPad + VenuesArt.RowThumb + VenuesArt.TextGap) * scale;
                FeedCell.Hairline(drawList, hairlineLeft, row.Max.X, row.Max.Y, ui.Hairline);
            }
        }

        return max.Y;
    }

    private float DrawMoreSpinner(int total, int shown, float left, float top, float width, float scale)
    {
        if (total <= shown)
        {
            return top;
        }

        if (InfiniteScroll.ReachedBottom())
        {
            visibleCards += PageSize;
        }

        LoadingPulse.Spinner(new Vector2(left + width * 0.5f, top + 20f * scale), 7f * scale, ui.Accent);
        return top + 40f * scale;
    }

    private float DrawFeatured(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var featured = sections.Featured;
        var count = featured.Count;
        var cardWidth = width - (count > 1 ? FeaturedPeek * scale : 0f);
        var height = MathF.Min(cardWidth * FeaturedAspect, FeaturedMaxHeight * scale);
        var stride = cardWidth + FeaturedGap * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        featuredPager.Step(delta, count);
        featuredPager.Drive("##venues.featured", row, stride, count, delta, scale);
        railOwnsPointer |= featuredPager.Owning;
        var interactive = !featuredPager.Dragging;
        var liftRoom = new Vector2(VenueCard.HoverLift + 1f, VenueCard.HoverLift + 1f) * scale;
        drawList.PushClipRect(row.Min - liftRoom, new Vector2(row.Max.X + liftRoom.X, row.Max.Y + liftRoom.Y), true);
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + (index - featuredPager.Value) * stride;
            if (left + cardWidth < row.Min.X - liftRoom.X || left > row.Max.X + liftRoom.X)
            {
                continue;
            }

            var venue = featured[index];
            var card = new Rect(new Vector2(left, origin.Y), new Vector2(left + cardWidth, origin.Y + height));
            HandleCardAction(VenueCard.DrawFeatured(drawList, card, venue, featuredText[index], IsFavorite(venue.Id),
                art, ui, row, interactive), venue, DisplayName);
        }

        drawList.PopClipRect();
        var active = Math.Clamp((int)MathF.Round(featuredPager.Value), 0, count - 1);
        var dotsCenter = new Vector2(origin.X + width * 0.5f, origin.Y + height + DotsHeight * scale * 0.5f);
        var tappedDot = DrawDots(drawList, dotsCenter, count, active, scale);
        if (tappedDot >= 0 && tappedDot != active)
        {
            featuredIdle = 0f;
            featuredPager.AnimateTo(tappedDot, count);
            UiFeedback.Play(UiSound.Tap);
        }

        AdvanceFeatured(row, count, delta);
        return origin.Y + height + DotsHeight * scale;
    }

    private int DrawDots(ImDrawListPtr drawList, Vector2 center, int count, int active, float scale)
    {
        if (count <= 1)
        {
            return -1;
        }

        var pitch = DotPitch * scale;
        var left = center.X - pitch * (count - 1) * 0.5f;
        var half = new Vector2(pitch * 0.5f, DotsHeight * scale * 0.5f);
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var dotCenter = new Vector2(left + index * pitch, center.Y);
            var hovered = UiInteract.Hover(dotCenter - half, dotCenter + half);
            var radius = DotRadius * scale;
            var ink = index == active ? ui.TitleInk : hovered ? Palette.WithAlpha(ui.TitleInk, 0.6f) : DotIdle;
            if (index == active)
            {
                var halfWidth = DotActiveWidth * scale * 0.5f;
                drawList.AddRectFilled(new Vector2(dotCenter.X - halfWidth, dotCenter.Y - radius),
                    new Vector2(dotCenter.X + halfWidth, dotCenter.Y + radius), ImGui.GetColorU32(ink), radius);
            }
            else
            {
                drawList.AddCircleFilled(dotCenter, radius, ImGui.GetColorU32(ink), 16);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(dotCenter - half, dotCenter + half, hovered))
            {
                tapped = index;
            }
        }

        return tapped;
    }

    private void AdvanceFeatured(Rect row, int count, float delta)
    {
        if (count <= 1 || featuredPager.Owning || UiInteract.Hover(row.Min, row.Max))
        {
            featuredIdle = 0f;
            return;
        }

        featuredIdle += delta;
        if (featuredIdle < AutoAdvanceSeconds)
        {
            return;
        }

        featuredIdle = 0f;
        featuredPager.AnimateTo((featuredPager.Page + 1) % count, count);
    }

    private float DrawCategoryGrid(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gap = CategoryGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = tileWidth * CategoryAspect;
        EnsureCategoryLines(tileWidth - CategoryInset * 2f * scale);
        var rows = (VenueCategories.Count + 1) / 2;
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            var column = category % 2;
            var rowIndex = category / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + rowIndex * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (category == 0)
            {
                UiAnchors.Report("venues.category.first", tile);
            }

            if (DrawCategoryTile(drawList, tile, category, scale))
            {
                OpenList(VenueListKind.Category, category, DisplayName);
            }
        }

        return origin.Y + rows * tileHeight + (rows - 1) * gap;
    }

    private bool DrawCategoryTile(ImDrawListPtr drawList, Rect rest, int category, float scale)
    {
        var tint = CategoryTints[category];
        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        var rounding = CategoryRounding * scale;
        var tile = VenueCard.Lift(drawList, rest, CategoryPressIds[category], hovered, hovered, rounding, scale,
            out var eased);
        if (sections.CategoryCover(category) is { } cover && images.Get(cover.BannerUrl) is not null)
        {
            VenueImage.Cover(drawList, tile, rounding, cover, categoryInitials[category], Art);
            var scrimTop = new Vector2(tile.Min.X, tile.Max.Y - tile.Height * CategoryScrimShare);
            Squircle.FillVerticalGradient(drawList, scrimTop, tile.Max, rounding,
                ImGui.GetColorU32(MediaOverlay.ScrimClear), ImGui.GetColorU32(MediaOverlay.ScrimDeep));
        }
        else
        {
            Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, rounding,
                ImGui.GetColorU32(Palette.Darken(tint, 0.35f)), ImGui.GetColorU32(Palette.Darken(tint, 0.70f)));
        }

        VenueCard.DrawRim(drawList, tile, rounding, eased, scale);
        var inset = CategoryInset * scale;
        var radius = CategoryIconRadius * scale;
        var iconCenter = new Vector2(tile.Min.X + inset + radius, tile.Min.Y + inset + radius);
        drawList.AddCircleFilled(iconCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.92f)), 32);
        PhoneIcon.Draw(drawList, iconCenter, CategoryGlyphs[category], MediaOverlay.White, 16f * scale);
        var count = categoryCounts[category] ?? string.Empty;
        var countSize = Typography.Measure(count, CategoryCountStyle);
        Typography.Draw(drawList, new Vector2(tile.Max.X - inset - countSize.X, iconCenter.Y - countSize.Y * 0.5f),
            count, Palette.WithAlpha(MediaOverlay.White, 0.85f), CategoryCountStyle);
        var lineHeight = Typography.LineHeight(CategoryLabelStyle);
        var hasSecond = categorySecondLines[category].Length > 0;
        var labelTop = tile.Max.Y - inset - lineHeight * (hasSecond ? 2f : 1f);
        Typography.Draw(drawList, new Vector2(tile.Min.X + inset, labelTop), categoryFirstLines[category],
            MediaOverlay.White, CategoryLabelStyle);
        if (hasSecond)
        {
            Typography.Draw(drawList, new Vector2(tile.Min.X + inset, labelTop + lineHeight),
                categorySecondLines[category], MediaOverlay.White, CategoryLabelStyle);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rest.Min, rest.Max, hovered);
    }

    private void EnsureCategoryLines(float width)
    {
        if (MathF.Abs(width - categoryLinesWidth) < 0.5f && categoryLinesRevision == categoryLabelRevision)
        {
            return;
        }

        categoryLinesWidth = width;
        categoryLinesRevision = categoryLabelRevision;
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            SplitLabel(Loc.T(CategoryLabels[category]), width, out categoryFirstLines[category],
                out categorySecondLines[category]);
        }
    }

    private static void SplitLabel(string label, float width, out string first, out string second)
    {
        second = string.Empty;
        if (Typography.Measure(label, CategoryLabelStyle).X <= width)
        {
            first = label;
            return;
        }

        for (var index = label.Length - 1; index > 0; index--)
        {
            if (label[index] != ' ')
            {
                continue;
            }

            var head = label[..index];
            if (Typography.Measure(head, CategoryLabelStyle).X > width)
            {
                continue;
            }

            first = head;
            second = Typography.FitText(label[(index + 1)..], width, CategoryLabelStyle);
            return;
        }

        first = Typography.FitText(label, width, CategoryLabelStyle);
    }

    private float DrawRail(ImDrawListPtr drawList, string id, VenueRail rail, IReadOnlyList<VenueEvent> venuesInRail,
        VenueTextList text, Vector2 origin, float width, float scale)
    {
        var cardWidth = VenueCard.RailWidth * scale;
        var cardHeight = VenueCard.RailHeight * scale;
        var gap = RailGap * scale;
        var count = Math.Min(venuesInRail.Count, MaxRailCards);
        var content = count * cardWidth + (count - 1) * gap;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + cardHeight));
        rail.Begin(id, row, content);
        railOwnsPointer |= rail.Owning;
        var interactive = rail.Interactive;
        var liftRoom = new Vector2(0f, (VenueCard.HoverLift + 1f) * scale);
        drawList.PushClipRect(row.Min - liftRoom, row.Max + liftRoom, true);
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + index * (cardWidth + gap) - rail.Offset;
            if (left + cardWidth < row.Min.X || left > row.Max.X)
            {
                continue;
            }

            var venue = venuesInRail[index];
            var rest = new Rect(new Vector2(left, origin.Y), new Vector2(left + cardWidth, origin.Y + cardHeight));
            if (VenueCard.DrawRail(drawList, rest, venue, text[index], art, row, interactive))
            {
                OpenDetail(venue, DisplayName);
            }
        }

        rail.DrawArrows(drawList, row, content, 18f * scale);
        drawList.PopClipRect();
        return row.Max.Y;
    }

    private float DrawDirectoryRow(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + DirectoryRowHeight * scale));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var hovered = VenuesArt.RowWash(drawList, ui, card, scale);
        var side = DirectoryIconSide * scale;
        var tileMin = new Vector2(card.Min.X + VenuesArt.RowPad * scale, card.Center.Y - side * 0.5f);
        var tileMax = tileMin + new Vector2(side, side);
        IconTile.FillShaded(drawList, tileMin, tileMax, side * Metrics.Radius.TileFactor, IconTile.Surface(ui.Accent));
        PhoneIcon.Draw(drawList, (tileMin + tileMax) * 0.5f, PhoneIcons.World, AccentRing.Ink, 18f * scale);
        var textLeft = tileMax.X + VenuesArt.TextGap * scale;
        var chevronX = card.Max.X - 18f * scale;
        var labelHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, card.Center.Y - labelHeight * 0.5f),
            Typography.FitText(directoryLabel, MathF.Max(1f, chevronX - 14f * scale - textLeft),
                TextStyles.BodyEmphasized), ui.TitleInk, TextStyles.BodyEmphasized);
        PhoneIcon.Draw(drawList, new Vector2(chevronX, card.Center.Y), PhoneIcons.ChevronRight, ui.MutedInk,
            16f * scale);
        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenList(VenueListKind.Directory, -1, DisplayName);
        }

        return card.Max.Y;
    }
}
