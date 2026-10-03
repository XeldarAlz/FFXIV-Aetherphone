using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float SearchRowHeight = 52f;
    private const float SearchPillHeight = 40f;
    private const float FeaturedAspect = 0.66f;
    private const float FeaturedMaxHeight = 236f;
    private const float FeaturedPeek = 22f;
    private const float FeaturedGap = 10f;
    private const float FeaturedDotsHeight = 22f;
    private const float FeaturedSwipeSlop = 10f;
    private const float AutoAdvanceSeconds = 6f;
    private const float CategoryGap = 8f;
    private const float CategoryIconRadius = 17f;
    private const float CategoryInset = 12f;
    private const float CategoryLabelGap = 8f;
    private const float RailGap = 10f;
    private const float DirectoryRowHeight = 58f;
    private const int MaxRailCards = 12;

    private static readonly TextStyle CategoryLabelStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle CategoryCountStyle = TextStyles.Caption1;

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

    private readonly Pager featuredPager = new();
    private readonly VenueRail laterRail = new();
    private readonly VenueRail nearRail = new();
    private readonly string[] categoryCounts = new string[VenueCategories.Count];
    private readonly string[] categoryFirstLines = new string[VenueCategories.Count];
    private readonly string[] categorySecondLines = new string[VenueCategories.Count];
    private float categoryLinesWidth = -1f;
    private int categoryLinesRevision = -1;
    private int categoryLabelRevision;
    private int categoryLineCount = 1;
    private Action retryAction = null!;
    private Action seeLiveAction = null!;
    private Action seeLaterAction = null!;
    private Action seeNearAction = null!;
    private Action seeFeaturedAction = null!;
    private bool featuredPressed;
    private Vector2 featuredPressPos;
    private float featuredIdle;
    private string liveHeading = string.Empty;
    private string laterHeading = string.Empty;
    private string nearHeading = string.Empty;
    private string directoryLabel = string.Empty;
    private string searchHeading = string.Empty;

    private void EnsureActions()
    {
        if (retryAction is not null)
        {
            return;
        }

        retryAction = () => venues.EnsureFresh(true);
        seeLiveAction = () => activeTab = VenueTab.Live;
        seeLaterAction = () => OpenList(VenueListKind.LaterToday);
        seeNearAction = () => OpenList(VenueListKind.NearYou);
        seeFeaturedAction = () =>
        {
            if (sections.FeaturedIsLive)
            {
                activeTab = VenueTab.Live;
                return;
            }

            OpenList(VenueListKind.LaterToday);
        };
    }

    private void ResetDiscover()
    {
        featuredPager.SnapTo(0, 1);
        featuredPressed = false;
        featuredIdle = 0f;
        laterRail.Reset();
        nearRail.Reset();
    }

    private void RebuildSectionLabels()
    {
        var culture = Loc.Culture;
        categoryLabelRevision++;
        var live = Loc.Upper(Loc.T(L.Venues.LiveNowLabel));
        liveHeading = $"{live} · {sections.Live.Count.ToString(culture)}";
        laterHeading = Loc.Upper(Loc.T(L.Venues.LaterToday));
        nearHeading = culture.TextInfo.ToUpper(Loc.T(L.Venues.NearWorld, CurrentWorld()));
        directoryLabel = Loc.T(L.Venues.BrowseAll, venues.Events.Count.ToString("N0", culture));
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            categoryCounts[category] = sections.CategoryCount(category).ToString("N0", culture);
        }
    }

    private void DrawDiscoverTab(Rect body)
    {
        EnsureActions();
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawSearchRow(scale);
            if (search.Length > 0)
            {
                DrawSearchResults();
            }
            else if (!DrawLoadingOrFailure(body))
            {
                if (sections.Featured.Count > 0)
                {
                    DrawSectionHeading(sections.FeaturedIsLive ? liveHeading : laterHeading, scale,
                        Loc.T(L.Venues.SeeAll), seeFeaturedAction);
                    DrawFeatured(scale);
                }

                DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Categories)), scale);
                DrawCategoryGrid(scale);
                if (sections.LaterRail.Count > 0)
                {
                    DrawSectionHeading(laterHeading, scale, Loc.T(L.Venues.SeeAll), seeLaterAction);
                    DrawRail(laterRail, sections.LaterRail, laterText, scale);
                }

                if (sections.NearRail.Count > 0)
                {
                    DrawSectionHeading(nearHeading, scale, Loc.T(L.Venues.SeeAll), seeNearAction);
                    DrawRail(nearRail, sections.NearRail, nearText, scale);
                }

                DrawDirectoryRow(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawSearchRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var top = origin.Y + (SearchRowHeight - SearchPillHeight) * scale * 0.5f;
        var searchRect = new Rect(new Vector2(origin.X + pad, top),
            new Vector2(origin.X + width - pad, top + SearchPillHeight * scale));
        var before = search;
        SearchField.Draw(searchRect, "##venueSearch", Loc.T(L.Venues.Search), ref search, AppPalettes.Venues, 80);
        if (!ReferenceEquals(before, search))
        {
            visibleCards = PageSize;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, SearchRowHeight * scale));
    }

    private void DrawSearchResults()
    {
        var scale = UiScale.Current;
        var nowUtc = DateTime.UtcNow;
        var key = new VenueQueryKey(venues.Version, VenueTimeFilter.All, configuration.VenueSourceFilter,
            ResolveScope().DataCenters, false, favoritesStamp, tagsStamp, search, CurrentMinute(nowUtc),
            World: ResolveScope().World, HideAdult: configuration.VenueHideAdult);
        if (searchQuery.Update(key, venues.Events, configuration.VenueFavorites, selectedTags, nowUtc))
        {
            searchText.Fill(searchQuery.Feed, nowUtc);
            searchHeading = Loc.Culture.TextInfo.ToUpper(
                Loc.T(L.Venues.VenueCount, searchQuery.Feed.Count.ToString("N0", Loc.Culture)));
        }

        if (searchQuery.Feed.Count == 0)
        {
            DrawEmptyState(PhoneIcons.Search, Loc.T(L.Venues.NoVenues), Loc.T(L.Venues.EmptyHint));
            return;
        }

        DrawSectionHeading(searchHeading, scale);
        DrawFeedList(searchQuery.Feed, searchText);
    }

    private void DrawFeatured(float scale)
    {
        var featured = sections.Featured;
        var count = featured.Count;
        if (count == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var cardWidth = width - pad * 2f - (count > 1 ? FeaturedPeek * scale : 0f);
        var height = MathF.Min(cardWidth * FeaturedAspect, FeaturedMaxHeight * scale);
        var stride = cardWidth + FeaturedGap * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        featuredPager.Step(delta, count);
        DriveFeaturedGesture(row, stride, count, delta, scale);
        var interactive = !featuredPager.Dragging;
        var liftRoom = new Vector2(0f, (VenueCard.HoverLift + 1f) * scale);
        drawList.PushClipRect(row.Min - liftRoom, row.Max + liftRoom, true);
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + pad + (index - featuredPager.Value) * stride;
            if (left + cardWidth < row.Min.X || left > row.Max.X)
            {
                continue;
            }

            var venue = featured[index];
            var card = new Rect(new Vector2(left, origin.Y), new Vector2(left + cardWidth, origin.Y + height));
            HandleCardAction(VenueCard.DrawFeatured(drawList, card, venue, featuredText[index], IsFavorite(venue.Id),
                art, Ink, row, interactive), venue);
        }

        drawList.PopClipRect();
        var active = Math.Clamp((int)MathF.Round(featuredPager.Value), 0, count - 1);
        PhotoCarousel.DrawDots(drawList,
            new Vector2(origin.X + width * 0.5f, origin.Y + height + FeaturedDotsHeight * scale * 0.5f), count, active,
            width * 0.5f, Ink.TitleInk);
        AdvanceFeatured(row, count, delta);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + FeaturedDotsHeight * scale));
    }

    private void DriveFeaturedGesture(Rect row, float stride, int count, float delta, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (count > 1 && !featuredPressed && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            UiInteract.Hover(row.Min, row.Max) && !UiInteract.InputBlocked)
        {
            featuredPressed = true;
            featuredPressPos = mouse;
        }

        if (!featuredPressed)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (featuredPager.Dragging)
            {
                featuredPager.Release(stride, count);
                UiInteract.BlockThisFrame();
            }

            featuredPressed = false;
            return;
        }

        var move = mouse - featuredPressPos;
        if (!featuredPager.Dragging && MathF.Abs(move.X) > FeaturedSwipeSlop * scale &&
            MathF.Abs(move.X) > MathF.Abs(move.Y))
        {
            featuredPager.Begin(featuredPressPos.X);
            UiInteract.CancelPendingTap();
        }

        if (featuredPager.Dragging)
        {
            featuredPager.Drag(mouse.X, stride, count, delta);
            featuredIdle = 0f;
            UiInteract.BlockThisFrame();
        }
    }

    private void AdvanceFeatured(Rect row, int count, float delta)
    {
        if (count <= 1 || featuredPager.Dragging || UiInteract.Hover(row.Min, row.Max))
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

    private void DrawCategoryGrid(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var gap = CategoryGap * scale;
        var tileWidth = (width - pad * 2f - gap) * 0.5f;
        EnsureCategoryLines(tileWidth - CategoryInset * 2f * scale);
        var tileHeight = (CategoryInset * 2f + CategoryIconRadius * 2f + CategoryLabelGap) * scale +
                         categoryLineCount * Typography.LineHeight(CategoryLabelStyle);
        var rows = (VenueCategories.Count + 1) / 2;
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            var column = category % 2;
            var rowIndex = category / 2;
            var min = new Vector2(origin.X + pad + column * (tileWidth + gap), origin.Y + rowIndex * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (category == 0)
            {
                UiAnchors.Report("venues.category.first", tile);
            }

            if (DrawCategoryTile(drawList, tile, category, scale))
            {
                OpenList(VenueListKind.Category, category);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * tileHeight + (rows - 1) * gap + Metrics.Space.Xs * scale));
    }

    private bool DrawCategoryTile(ImDrawListPtr drawList, Rect rest, int category, float scale)
    {
        var tint = CategoryTints[category];
        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        var rounding = 16f * scale;
        var tile = VenueCard.Lift(drawList, rest, CategoryPressIds[category], hovered, hovered, rounding, scale,
            out var eased);
        Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.20f + 0.12f * eased)),
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.08f + 0.10f * eased)));
        Squircle.Stroke(drawList, tile.Min, tile.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.34f + 0.30f * eased)), 1f * scale);
        var inset = CategoryInset * scale;
        var radius = CategoryIconRadius * scale;
        var iconCenter = new Vector2(tile.Min.X + inset + radius, tile.Min.Y + inset + radius);
        drawList.AddCircleFilled(iconCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.92f)), 32);
        PhoneIcon.Draw(drawList, iconCenter, CategoryGlyphs[category], MediaOverlay.White, 18f * scale);
        var count = categoryCounts[category] ?? string.Empty;
        var countSize = Typography.Measure(count, CategoryCountStyle);
        Typography.Draw(drawList, new Vector2(tile.Max.X - inset - countSize.X, iconCenter.Y - countSize.Y * 0.5f),
            count, Ink.MutedInk, CategoryCountStyle);
        var labelTop = iconCenter.Y + radius + CategoryLabelGap * scale;
        var lineHeight = Typography.LineHeight(CategoryLabelStyle);
        Typography.Draw(drawList, new Vector2(tile.Min.X + inset, labelTop), categoryFirstLines[category],
            Ink.TitleInk, CategoryLabelStyle);
        if (categorySecondLines[category].Length > 0)
        {
            Typography.Draw(drawList, new Vector2(tile.Min.X + inset, labelTop + lineHeight),
                categorySecondLines[category], Ink.TitleInk, CategoryLabelStyle);
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
        categoryLineCount = 1;
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            var label = Loc.T(CategoryLabels[category]);
            SplitLabel(label, width, out categoryFirstLines[category], out categorySecondLines[category]);
            if (categorySecondLines[category].Length > 0)
            {
                categoryLineCount = 2;
            }
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

    private void DrawRail(VenueRail rail, IReadOnlyList<VenueEvent> venuesInRail, VenueTextList text, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var cardWidth = VenueCard.RailWidth * scale;
        var cardHeight = VenueCard.RailHeight * scale;
        var gap = RailGap * scale;
        var count = Math.Min(venuesInRail.Count, MaxRailCards);
        var content = pad * 2f + count * cardWidth + (count - 1) * gap;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + cardHeight));
        rail.Begin(row, content);
        var interactive = rail.Interactive;
        var liftRoom = new Vector2(0f, (VenueCard.HoverLift + 1f) * scale);
        drawList.PushClipRect(row.Min - liftRoom, row.Max + liftRoom, true);
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + pad + index * (cardWidth + gap) - rail.Offset;
            if (left + cardWidth < row.Min.X || left > row.Max.X)
            {
                continue;
            }

            var venue = venuesInRail[index];
            var rest = new Rect(new Vector2(left, origin.Y), new Vector2(left + cardWidth, origin.Y + cardHeight));
            if (VenueCard.DrawRail(drawList, rest, venue, text[index], art, Ink, row, interactive))
            {
                OpenDetail(venue);
            }
        }

        rail.DrawArrows(drawList, row, content, pad);
        drawList.PopClipRect();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Xs * scale));
    }

    private void DrawDirectoryRow(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var top = origin.Y + Metrics.Space.Lg * scale;
        var card = new Rect(new Vector2(origin.X + pad, top),
            new Vector2(origin.X + width - pad, top + DirectoryRowHeight * scale));
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, elevated: true);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, ImGui.GetColorU32(Ink.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radius = CategoryIconRadius * scale;
        var iconCenter = new Vector2(card.Min.X + 14f * scale + radius, card.Center.Y);
        drawList.AddCircleFilled(iconCenter, radius, ImGui.GetColorU32(Ink.AccentWash), 32);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.World, Ink.AccentLink, 18f * scale);
        var textLeft = iconCenter.X + radius + 12f * scale;
        var chevronX = card.Max.X - 18f * scale;
        var labelHeight = Typography.LineHeight(CategoryLabelStyle);
        Typography.Draw(drawList, new Vector2(textLeft, card.Center.Y - labelHeight * 0.5f),
            Typography.FitText(directoryLabel, MathF.Max(1f, chevronX - 14f * scale - textLeft), CategoryLabelStyle),
            Ink.TitleInk, CategoryLabelStyle);
        PhoneIcon.Draw(drawList, new Vector2(chevronX, card.Center.Y), PhoneIcons.ChevronRight, Ink.FaintInk,
            16f * scale);
        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenList(VenueListKind.Directory);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Max.Y - origin.Y));
    }
}
