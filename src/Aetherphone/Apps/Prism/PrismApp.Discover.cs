using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int GridColumns = 2;
    private const int PlaceholderRows = 2;
    private const float TileShadeShare = 0.4f;
    private const float HeaderDotShare = 0.32f;
    private const float HeaderDotRimUnits = 1.5f;
    private const float ContestRowUnits = 56f;
    private const float ContestThumbUnits = 48f;
    private const float EmptyGlyphUnits = 28f;
    private const float EmptyDiscUnits = 34f;
    private const float EmptyActionUnits = 132f;
    private const float EmptyTextUnits = 300f;

    private static readonly LocString[] ScopeNames = { L.Prism.ScopeNew, L.Prism.ScopePopular, L.Prism.ScopeFollowing };
    private static readonly GlamScope[] ShownScopes = { GlamScope.New, GlamScope.Popular, GlamScope.Following };
    private static readonly LocString[] FilterNames = { L.Prism.FitsMe, L.Prism.FilterModded, L.Prism.FilterVanilla };
    private static readonly Vector4 TileShade = new(0f, 0f, 0f, 0.7f);
    private static readonly Vector4 TileClear = new(0f, 0f, 0f, 0f);

    private readonly PullToRefresh discoverPull = new();
    private readonly ChipRail scopeRail = new();
    private readonly ChipRail filterRail = new();
    private readonly string[] scopeLabels = new string[ScopeNames.Length];
    private readonly bool[] scopeActive = new bool[ScopeNames.Length];
    private readonly string[] filterLabels = new string[FilterNames.Length];
    private readonly bool[] filterActive = new bool[FilterNames.Length];
    private GlamScope discoverScope = GlamScope.New;
    private int discoverFilter;
    private bool todayResolved;
    private Action? refreshDiscoverNow;
    private CachedText contestCaption;

    private void DrawDiscover(ImDrawListPtr drawList, Rect content, float scale)
    {
        content = AppArea(content, scale);
        if (!glams.IsSignedIn)
        {
            DrawSignedOut(drawList, content, scale);
            return;
        }

        if (searchOpen)
        {
            DrawSearch(drawList, content, scale);
            return;
        }

        var top = DrawDiscoverHeader(drawList, content, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        var lane = glams.Lane(discoverScope);
        using var surface = BeginBody(body);
        discoverPull.Draw(body, surface.Pull, surface.Dragging, lane.Loading, PrismArt.Muted,
            refreshDiscoverNow ??= RefreshDiscoverAlways);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var outer = Metrics.Space.Xxs * scale;
        var cursor = DrawDayHero(list, origin.X, origin.Y, width, scale, out var hero);
        cursor = DrawContestRow(list, origin.X, cursor, width, scale);
        var items = lane.Items;
        var first = hero is not null && items.Length > 0 && items[0].Id == hero.Id ? 1 : 0;
        var following = discoverScope == GlamScope.Following;
        cursor = DrawLaneGrid(list, origin.X + outer, cursor, width - outer * 2f, lane,
            following ? L.Prism.FollowingEmptyHint : L.Prism.EmptyFeedHint, scale, first,
            Loc.T(following ? L.Prism.FollowingEmptyTitle : L.Prism.EmptyFeedTitle));
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMore(discoverScope);
        }
    }

    private float DrawDiscoverHeader(ImDrawListPtr drawList, Rect content, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var radius = tap * 0.5f;
        var top = content.Min.Y + Metrics.Space.Xs * scale;
        var heart = new Vector2(content.Max.X - radius, top + radius);
        var search = heart - new Vector2(tap, 0f);
        var left = content.Min.X + Metrics.Space.Lg * scale;
        DrawScopeTabs(drawList, new Rect(new Vector2(left, top),
            new Vector2(search.X - radius - Metrics.Space.Xs * scale, top + tap)), scale);
        if (DrawHeaderIcon(drawList, search, radius, PhoneIcons.Search, Loc.T(L.Common.Search)))
        {
            OpenSearch();
        }

        if (DrawHeaderIcon(drawList, heart, radius, PhoneIcons.Heart, Loc.T(L.Prism.Activity)))
        {
            OpenActivity();
        }

        if (social.UnseenCount(Id) > 0)
        {
            var dot = heart + new Vector2(radius, -radius) * HeaderDotShare;
            drawList.AddCircleFilled(dot, (ActivityDotUnits + HeaderDotRimUnits) * scale,
                ImGui.GetColorU32(PrismArt.Back));
            drawList.AddCircleFilled(dot, ActivityDotUnits * scale, ImGui.GetColorU32(PrismArt.Fore));
        }

        var chipTop = top + tap + Metrics.Space.Xxs * scale;
        var chips = new Rect(new Vector2(left, chipTop),
            new Vector2(content.Max.X - Metrics.Space.Lg * scale, chipTop + ChipRail.RowHeight * scale));
        for (var index = 0; index < FilterNames.Length; index++)
        {
            filterLabels[index] = Loc.T(FilterNames[index]);
            filterActive[index] = index == 0 ? glams.FitsBody : discoverFilter == index;
        }

        var tapped = filterRail.Draw(chips, ui, filterLabels, filterActive);
        if (tapped == 0)
        {
            var (race, gender) = glams.FitsBody ? (0, 0) : WornBody();
            glams.SetBody(race, gender);
            pendingScroll = 0f;
            RefreshDiscover(true);
        }
        else if (tapped > 0)
        {
            SetDiscoverFilter(discoverFilter == tapped ? 0 : tapped);
        }

        return chips.Max.Y + Metrics.Space.Sm * scale;
    }

    private bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, string tip) =>
        RoundButton.Icon(drawList, center, radius, glyph, controls, ButtonStyle.Plain, tip, HoverLabelSide.Below);

    private void DrawScopeTabs(ImDrawListPtr drawList, Rect row, float scale)
    {
        var gap = Metrics.Space.Lg * scale;
        var total = -gap;
        for (var index = 0; index < ScopeNames.Length; index++)
        {
            scopeLabels[index] = Loc.T(ScopeNames[index]);
            scopeActive[index] = discoverScope == ShownScopes[index];
            total += Typography.Measure(scopeLabels[index], PrismType.Title).X + gap;
        }

        var picked = -1;
        if (total > row.Width)
        {
            var half = ChipRail.RowHeight * scale * 0.5f;
            picked = scopeRail.Draw(new Rect(new Vector2(row.Min.X, row.Center.Y - half),
                new Vector2(row.Max.X, row.Center.Y + half)), ui, scopeLabels, scopeActive);
        }
        else
        {
            var x = row.Min.X;
            for (var index = 0; index < ScopeNames.Length; index++)
            {
                var size = Typography.Measure(scopeLabels[index], PrismType.Title);
                var hit = new Rect(new Vector2(x - gap * 0.5f, row.Min.Y),
                    new Vector2(x + size.X + gap * 0.5f, row.Max.Y));
                var hovered = UiInteract.Hover(hit.Min, hit.Max);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                Typography.Draw(drawList, new Vector2(x, row.Center.Y - size.Y * 0.5f), scopeLabels[index],
                    scopeActive[index] || hovered ? PrismArt.Fore : PrismArt.Muted, PrismType.Title);
                if (UiInteract.Click(hit.Min, hit.Max, hovered))
                {
                    picked = index;
                }

                x += size.X + gap;
            }
        }

        if (picked < 0 || scopeActive[picked])
        {
            return;
        }

        discoverScope = ShownScopes[picked];
        pendingScroll = 0f;
        RefreshDiscover(false);
    }

    private void SetDiscoverFilter(int filter)
    {
        discoverFilter = filter;
        glams.SetVanillaOnly(filter switch
        {
            1 => false,
            2 => true,
            _ => null,
        });
        pendingScroll = 0f;
        RefreshDiscover(true);
    }

    private void RefreshDiscoverAlways() => RefreshDiscover(true);

    private void RefreshDiscover(bool always)
    {
        if (always || glams.Lane(discoverScope).NeverLoaded)
        {
            glams.Refresh(discoverScope);
        }

        if (always || glams.Lane(GlamScope.Today).NeverLoaded)
        {
            glams.Refresh(GlamScope.Today);
        }
    }

    private float DrawDayHero(ImDrawListPtr drawList, float left, float top, float width, float scale,
        out GlamDto? hero)
    {
        hero = null;
        var today = glams.Lane(GlamScope.Today);
        if (today.Items.Length == 0)
        {
            if (!today.Loading)
            {
                todayResolved = true;
            }

            if (todayResolved)
            {
                return top;
            }
        }

        var photo = new Rect(new Vector2(left, top), new Vector2(left + width, top + width / GlamStore.PhotoRatio));
        if (today.Items.Length == 0)
        {
            PrismArt.Placeholder(drawList, photo, false, scale);
            return photo.Max.Y;
        }

        var glam = today.Items[0];
        hero = glam;
        if (!ImGui.IsRectVisible(photo.Min, photo.Max))
        {
            return photo.Max.Y;
        }

        DrawLook(drawList, photo, Cover(glam), false, native: true);
        var inset = Metrics.Space.Lg * scale;
        var gap = Metrics.Space.Xxs * scale;
        var textLeft = left + inset;
        var textWidth = width - inset * 2f;
        var caption = Typography.LineHeight(PrismType.Caption);
        var byline = Typography.LineHeight(PrismType.Headline);
        var block = caption + gap + PrismType.HeroHeight(glam.Title, textWidth) + gap + byline;
        PrismArt.Scrim(drawList, photo, block, scale);
        var y = photo.Max.Y - inset - block;
        Typography.Draw(drawList, new Vector2(textLeft, y),
            Typography.FitText(Loc.T(L.Prism.GlamOfTheDay), textWidth, PrismType.Caption), PrismArt.PhotoMuted,
            PrismType.Caption);
        y += caption + gap;
        y += PrismType.DrawHero(drawList, new Vector2(textLeft, y), glam.Title, textWidth, PrismArt.PhotoFore) + gap;
        var bylineMiddle = y + byline * 0.5f;
        var handleEnd = DrawCountByline(drawList, textLeft, textLeft + textWidth, bylineMiddle, AuthorLine(glam.Author),
            PhoneIcons.Heart, glam.Likes > 0 ? NumberText.Compact(glam.Likes) : string.Empty, PrismArt.PhotoFore,
            PrismArt.PhotoFore, scale);
        if (UiInteract.Click(photo.Min, photo.Max, HoverWash(drawList, photo)))
        {
            OpenGlam(glam);
        }

        var reach = Metrics.Size.TapTarget * scale * 0.5f;
        ClickProfile(new Rect(new Vector2(textLeft, bylineMiddle - reach),
            new Vector2(handleEnd, bylineMiddle + reach)), glam.Author);

        return photo.Max.Y;
    }

    private void ClickProfile(Rect hit, GlamAuthorDto? author)
    {
        if (author is { Id.Length: > 0 } && UiInteract.HoverClick(hit.Min, hit.Max))
        {
            OpenProfile(author);
        }
    }

    private float DrawContestRow(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (store.Week is not { Id.Length: > 0 } week ||
            week.Phase != PrismPhases.Entering && !PrismPhases.Judgeable(week.Phase))
        {
            return top + Metrics.Space.Xxs * scale;
        }

        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + ContestRowUnits * scale));
        var hovered = HoverWash(drawList, row);
        if (ImGui.IsRectVisible(row.Min, row.Max))
        {
            DrawContestRowFace(drawList, row, week, hovered, scale);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenTab(PrismTab.Week);
        }

        return row.Max.Y + Metrics.Space.Xxs * scale;
    }

    private void DrawContestRowFace(ImDrawListPtr drawList, Rect row, PrismWeekDto week, bool hovered, float scale)
    {
        var inset = Metrics.Space.Lg * scale;
        var thumbHeight = ContestThumbUnits * scale;
        var thumb = new Rect(new Vector2(row.Min.X + inset, row.Center.Y - thumbHeight * 0.5f),
            new Vector2(row.Min.X + inset + thumbHeight * GlamStore.PhotoRatio, row.Center.Y + thumbHeight * 0.5f));
        var cover = ContestCover(week);
        if (cover.Length > 0)
        {
            DrawLook(drawList, thumb, cover, false);
        }
        else
        {
            PrismArt.Placeholder(drawList, thumb, false, scale);
            PhoneIcon.Draw(drawList, thumb.Center, PhoneIcons.Crown, PrismArt.Muted, GridGlyphUnits * scale);
        }

        var label = Loc.T(week.Phase == PrismPhases.Final ? L.Prism.JudgeFinal
            : week.Phase != PrismPhases.Entering ? L.Prism.JudgeHeat
            : week.MyEntry is null ? L.Prism.Enter : L.Prism.YourLook);
        var labelWidth = ChipRail.WidthFor(Typography.Measure(label, ChipRail.LabelStyle).X,
            ChipRail.DefaultLabelPadding, scale);
        var chipHalf = ChipRail.ChipHeight * scale * 0.5f;
        var right = row.Max.X - inset;
        ChipRail.PaintChip(drawList, new Rect(new Vector2(right - labelWidth, row.Center.Y - chipHalf),
            new Vector2(right, row.Center.Y + chipHalf)), label, false, hovered, controls);
        var textLeft = thumb.Max.X + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, right - labelWidth - Metrics.Space.Md * scale - textLeft);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        var y = row.Center.Y - (captionHeight + Typography.LineHeight(PrismType.Headline)) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, y),
            Typography.FitText(ContestCaption(week), room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        var themeStyle = FitStyle(week.Title, room, PrismType.Headline);
        var theme = Typography.FitText(week.Title, room, themeStyle);
        Typography.Draw(drawList, new Vector2(textLeft, y + captionHeight + (Typography.LineHeight(PrismType.Headline) -
            Typography.LineHeight(themeStyle)) * 0.5f), theme, PrismArt.Fore, themeStyle);
    }

    private string ContestCover(PrismWeekDto week) =>
        CoverPlacing(store.Results) is { ImageUrl.Length: > 0 } winner
            ? winner.ImageUrl
            : week.MyEntry?.ImageUrl ?? string.Empty;

    private string ContestCaption(PrismWeekDto week)
    {
        var entering = week.Phase == PrismPhases.Entering;
        var deadline = DeadlineValue(week);
        var key = HashCode.Combine(entering, deadline, Loc.Current.Code);
        if (contestCaption.IsCurrent(key))
        {
            return contestCaption.Value;
        }

        return contestCaption.Store(key, deadline.Length == 0
            ? Loc.T(L.Prism.TabWeek)
            : Loc.T(entering ? L.Prism.ContestCloses : L.Prism.ContestJudgingCloses, deadline));
    }

    private float DrawLaneGrid(ImDrawListPtr drawList, float left, float top, float width, FeedLane<GlamDto> lane,
        LocString emptyHint, float scale, int first = 0, string emptyTitle = "")
    {
        var items = lane.Items;
        if (items.Length == 0 && lane.Loading)
        {
            return DrawGridPlaceholders(drawList, left, top, width, scale);
        }

        if (items.Length == 0)
        {
            var failed = lane.Failed;
            var action = failed ? Loc.T(L.Common.Retry) : LaneAction();
            var area = new Rect(new Vector2(left, top + Metrics.Space.Xl * scale),
                new Vector2(left + width, top + Metrics.Space.Xl * scale));
            var bottom = DrawEmpty(drawList, area, failed ? PhoneIcons.Refresh : PhoneIcons.Photo,
                failed ? Loc.T(L.Common.LoadFailed) : emptyTitle, Loc.T(failed ? L.Common.LoadFailedHint : emptyHint),
                action, ButtonStyle.Solid, scale, out var acted);
            if (acted && failed)
            {
                RetryLane(lane);
            }
            else if (acted)
            {
                RunLaneAction();
            }

            return bottom + PrismArt.BottomMargin(scale);
        }

        var gridBottom = DrawGlamGrid(drawList, left, top, width, items, scale, first);
        if (lane.LoadingMore)
        {
            var resume = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(left, gridBottom));
            InfiniteScroll.DrawLoadingRow(left + width * 0.5f, PrismArt.Muted);
            gridBottom = ImGui.GetCursorScreenPos().Y;
            ImGui.SetCursorScreenPos(resume);
        }

        return gridBottom + PrismArt.BottomMargin(scale);
    }

    private float DrawGlamGrid(ImDrawListPtr drawList, float left, float top, float width, GlamDto[] items, float scale,
        int first = 0)
    {
        var shown = Math.Max(0, items.Length - first);
        for (var index = 0; index < shown; index++)
        {
            var photo = GridCell(left, top, width, index, scale);
            if (!ImGui.IsRectVisible(photo.Min, photo.Max))
            {
                continue;
            }

            var glam = items[first + index];
            var count = glam.Likes > 0 ? NumberText.Compact(glam.Likes) : string.Empty;
            if (DrawGridTile(drawList, photo, Cover(glam), AuthorLine(glam.Author), PhoneIcons.Heart, count, scale))
            {
                OpenGlam(glam);
            }

            var countWidth = count.Length > 0
                ? Typography.Measure(count, PrismType.Caption).X + (GridGlyphUnits + Metrics.Space.Sm) * scale
                : 0f;
            var band = GridBand(photo, scale);
            ClickProfile(new Rect(band.Min, new Vector2(band.Max.X - countWidth, photo.Max.Y)), glam.Author);
        }

        return top + GridHeight(width, shown, scale);
    }

    private static Rect GridCell(float left, float top, float width, int index, float scale)
    {
        var gap = Metrics.Space.Xxs * scale;
        var column = (width - gap * (GridColumns - 1)) / GridColumns;
        var min = new Vector2(left + index % GridColumns * (column + gap),
            top + index / GridColumns * GridPitch(width, scale));
        return new Rect(min, min + new Vector2(column, column / GlamStore.PhotoRatio));
    }

    private static float GridHeight(float width, int count, float scale) =>
        (count + GridColumns - 1) / GridColumns * GridPitch(width, scale);

    private static float GridPitch(float width, float scale)
    {
        var gap = Metrics.Space.Xxs * scale;
        return (width - gap * (GridColumns - 1)) / GridColumns / GlamStore.PhotoRatio + gap;
    }

    private float DrawGridPlaceholders(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        for (var index = 0; index < PlaceholderRows * GridColumns; index++)
        {
            PrismArt.Placeholder(drawList, GridCell(left, top, width, index, scale), false, scale);
        }

        return top + GridHeight(width, PlaceholderRows * GridColumns, scale) + PrismArt.BottomMargin(scale);
    }

    private string LaneAction()
    {
        if (!glams.IsSignedIn || screen is not (PrismScreen.Discover or PrismScreen.Me))
        {
            return string.Empty;
        }

        return Loc.T(screen == PrismScreen.Discover && discoverScope == GlamScope.Following ? L.Prism.ScopePopular
            : screen == PrismScreen.Me && showSaved ? L.Prism.TabDiscover
            : L.Prism.PostGlam);
    }

    private void RunLaneAction()
    {
        if (screen == PrismScreen.Discover && discoverScope == GlamScope.Following)
        {
            discoverScope = GlamScope.Popular;
            RefreshDiscover(false);
        }
        else if (screen == PrismScreen.Me && showSaved)
        {
            OpenTab(PrismTab.Discover);
        }
        else
        {
            StartCompose();
        }
    }

    private float DrawEmpty(ImDrawListPtr drawList, Rect area, string glyph, string title, string hint, string action,
        ButtonStyle actionStyle, float scale, out bool acted)
    {
        acted = false;
        var textWidth = MathF.Max(1f, MathF.Min(area.Width - Metrics.Space.Xxl * 2f * scale, EmptyTextUnits * scale));
        var disc = EmptyDiscUnits * scale;
        var titleLine = Typography.LineHeight(PrismType.Headline);
        var titleHeight = title.Length > 0 ? titleLine + Metrics.Space.Xs * scale : 0f;
        var wrap = PrismType.BalancedWidth(hint, PrismType.Body, textWidth);
        var hintHeight = Typography.MeasureWrappedBlock(hint, PrismType.Body, wrap).Y;
        var buttonHeight = action.Length > 0 ? Metrics.Space.Lg * scale + Button.LargeHeight * scale : 0f;
        var height = disc * 2f + Metrics.Space.Md * scale + titleHeight + hintHeight + buttonHeight;
        var top = MathF.Max(area.Min.Y, area.Center.Y - height * 0.5f);
        var center = area.Center.X;
        var discCenter = new Vector2(center, top + disc);
        drawList.AddCircleFilled(discCenter, disc, ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary)));
        PhoneIcon.Draw(drawList, discCenter, glyph, PrismArt.Muted, EmptyGlyphUnits * scale);
        top += disc * 2f + Metrics.Space.Md * scale;
        if (title.Length > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(center, top + titleLine * 0.5f),
                Typography.FitText(title, textWidth, PrismType.Headline), PrismArt.Fore, PrismType.Headline);
            top += titleHeight;
        }

        top += Typography.DrawWrappedCentered(new Vector2(center, top), hint, PrismArt.Muted, PrismType.Body, wrap);
        if (action.Length == 0)
        {
            return top;
        }

        top += Metrics.Space.Lg * scale;
        var room = area.Width - Metrics.Space.Lg * 2f * scale;
        var half = MathF.Min(MathF.Max(ButtonWidth([action], room, scale), EmptyActionUnits * scale), room) * 0.5f;
        var button = new Rect(new Vector2(center - half, top),
            new Vector2(center + half, top + Button.LargeHeight * scale));
        acted = DrawButton(drawList, button, action, actionStyle, scale);
        return button.Max.Y;
    }

    private void RetryLane(FeedLane<GlamDto> lane)
    {
        lane.ClearFailure();
        if (!ReferenceEquals(lane, glams.List))
        {
            glams.Refresh(discoverScope);
        }
        else if (screen == PrismScreen.GlamList)
        {
            OpenList();
        }
        else if (glams.Profile?.User is { } author)
        {
            glams.OpenProfile(author, showSaved);
        }
    }

    private Rect AppArea(Rect content, float scale) =>
        followedTheme is { } theme ? SceneChrome.AppAreaFrom(content, theme, scale) : content;

    private static string Cover(GlamDto glam) => glam.Photos is { Length: > 0 } photos ? photos[0].Url : string.Empty;

    private void DrawSignedOut(ImDrawListPtr drawList, Rect content, float scale)
    {
        var area = new Rect(content.Min, new Vector2(content.Max.X, content.Max.Y - bottomChrome));
        DrawEmpty(drawList, area, PhoneIcons.User, Loc.T(L.Prism.SignInTitle), Loc.T(L.Prism.SignInBrowse),
            Loc.T(L.Prism.OpenSettings), ButtonStyle.Solid, scale, out var acted);
        if (acted)
        {
            navigation.Open("settings");
        }
    }
}
