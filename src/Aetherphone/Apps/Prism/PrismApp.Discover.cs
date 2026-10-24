using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum DiscoverFilter : byte
{
    FitsMe,
    Race,
    Male,
    Female,
    Modded,
    Vanilla,
}

internal sealed partial class PrismApp
{
    private const int GridColumns = 2;
    private const int ExploreColumns = 3;
    private const int PlaceholderRows = 2;
    private const float HeaderDotShare = 0.32f;
    private const float HeaderDotRimUnits = 1.5f;
    private const float ContestRowUnits = 120f;
    private const float ContestThumbUnits = 88f;
    private const float EmptyDiscUnits = 34f;
    private const float EmptyActionUnits = 132f;
    private const float EmptyTextUnits = 300f;
    private const float ScopeLineUnits = 2f;
    private const float ContestMarkUnits = 30f;
    private const float ContestMarkShare = 0.45f;
    private const float ContestTileUnits = 32f;
    private const int CountdownUnitsShown = 3;
    private const int RaceCount = 8;
    private const int MaleGender = 0;
    private const int FemaleGender = 1;

    private static readonly LocString[] ScopeNames = { L.Prism.ScopeNew, L.Prism.ScopePopular, L.Prism.ScopeFollowing };
    private static readonly GlamScope[] ShownScopes = { GlamScope.New, GlamScope.Popular, GlamScope.Following };
    private static readonly PrismFact[] FilterFacts =
    {
        PrismFact.FitsMe, PrismFact.Race, PrismFact.Male, PrismFact.Female, PrismFact.Modded, PrismFact.Vanilla,
    };

    private readonly PullToRefresh discoverPull = new();
    private readonly ChipRail scopeRail = new();
    private readonly string[] scopeLabels = new string[ScopeNames.Length];
    private readonly bool[] scopeActive = new bool[ScopeNames.Length];
    private readonly Spring[] filterFill = new Spring[FilterFacts.Length];
    private readonly Spring[] filterHover = new Spring[FilterFacts.Length];
    private readonly DropdownMenu raceMenu = new() { FadeOnClose = true, Compact = true };
    private readonly DropdownMenu.Item[] raceItems = new DropdownMenu.Item[RaceCount + 1];
    private GlamScope discoverScope = GlamScope.New;
    private bool fitsMe;
    private bool scopeLineShown;
    private readonly PrismCountdown rowCountdown = new();
    private Spring scopeLineLeft;
    private Spring scopeLineRight;
    private Action? refreshDiscoverNow;
    private CachedText contestCaption;

    private void DrawDiscover(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        content = AppArea(full, content);
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

        raceMenu.Gate();
        var top = DrawDiscoverHeader(drawList, content, scale);
        top = DrawFilterRow(drawList, content, top, scale) + Metrics.Space.Xs * scale;
        DrawDiscoverFeed(new Rect(new Vector2(content.Min.X, top), content.Max), scale);
        DrawRaceMenu(full);
    }

    private void DrawDiscoverFeed(Rect body, float scale)
    {
        var lane = glams.Lane(discoverScope);
        using var surface = BeginBody(body);
        discoverPull.Draw(body, surface.Pull, surface.Dragging, lane.Loading, PrismArt.Muted,
            refreshDiscoverNow ??= RefreshDiscoverAlways);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var outer = Metrics.Space.Xxs * scale;
        var cursor = DrawShowcase(list, origin.X, origin.Y + Metrics.Space.Xs * scale, width, scale);
        cursor = DrawContestRow(list, origin.X, cursor + Metrics.Space.Sm * scale, width, scale);
        var filtered = AnyFilterOn();
        var following = discoverScope == GlamScope.Following;
        cursor = DrawLaneGrid(list, origin.X + outer, cursor, width - outer * 2f, lane,
            filtered ? L.Prism.FiltersEmptyHint : following ? L.Prism.FollowingEmptyHint : L.Prism.EmptyFeedHint,
            scale,
            Loc.T(filtered ? L.Prism.NoMatches : following ? L.Prism.FollowingEmptyTitle : L.Prism.EmptyFeedTitle));
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        if (showcasePager.Owning)
        {
            surface.CancelDrag();
        }

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
        if (IconButton(drawList, search, PhoneIcons.Search, Loc.T(L.Common.Search), scale, HoverLabelSide.Below))
        {
            OpenSearch();
        }

        if (IconButton(drawList, heart, PhoneIcons.Heart, Loc.T(L.Prism.Activity), scale, HoverLabelSide.Below))
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

        return top + tap;
    }

    private float DrawFilterRow(ImDrawListPtr drawList, Rect content, float top, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var size = PrismArt.HeaderIconUnits * scale;
        var reach = new Vector2(tap * 0.5f, tap * 0.5f);
        var extent = new Vector2(size * 0.5f, size * 0.5f);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var first = content.Min.X + Metrics.Space.Lg * scale + size * 0.5f;
        var pitch = (content.Max.X - tap * 0.5f - first) / (FilterFacts.Length - 1);
        var tapped = -1;
        var tappedHit = default(Rect);
        for (var index = 0; index < FilterFacts.Length; index++)
        {
            var center = new Vector2(first + pitch * index, top + tap * 0.5f);
            var hit = new Rect(center - reach, center + reach);
            var hovered = UiInteract.Hover(hit.Min, hit.Max);
            var hover = Math.Clamp(filterHover[index].Step(hovered ? 1f : 0f, Motion.HoverLift, delta), 0f, 1f);
            var target = FilterOn((DiscoverFilter)index) ? 1f : 0f;
            var fill = Math.Clamp(filterFill[index].Step(target, Motion.Appear, delta), 0f, 1f);
            PrismArt.FactToggle(drawList, center, size, FilterFacts[index], fill, hover);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (hover > 0.02f && !raceMenu.Open)
            {
                HoverTooltip.Enqueue(new Rect(center - extent, center + extent), FilterLabel((DiscoverFilter)index),
                    hover, HoverLabelSide.Below);
            }

            if (UiInteract.Click(hit.Min, hit.Max, hovered))
            {
                tapped = index;
                tappedHit = hit;
            }
        }

        if (tapped >= 0)
        {
            TapFilter((DiscoverFilter)tapped, tappedHit);
        }

        return top + tap;
    }

    private bool FilterOn(DiscoverFilter filter) => filter switch
    {
        DiscoverFilter.FitsMe => fitsMe,
        DiscoverFilter.Race => glams.Race > 0,
        DiscoverFilter.Male => glams.Gender == MaleGender,
        DiscoverFilter.Female => glams.Gender == FemaleGender,
        DiscoverFilter.Modded => glams.VanillaOnly == false,
        _ => glams.VanillaOnly == true,
    };

    private string FilterLabel(DiscoverFilter filter) => filter switch
    {
        DiscoverFilter.FitsMe => Loc.T(L.Prism.FitsMe),
        DiscoverFilter.Race => RaceLabel(glams.Race, glams.Gender == FemaleGender) is { Length: > 0 } race
            ? race
            : Loc.T(L.Prism.FilterRace),
        DiscoverFilter.Male => Loc.T(L.Prism.GenderMale),
        DiscoverFilter.Female => Loc.T(L.Prism.GenderFemale),
        DiscoverFilter.Modded => Loc.T(L.Prism.FilterModded),
        _ => Loc.T(L.Prism.FilterVanilla),
    };

    private void TapFilter(DiscoverFilter filter, Rect anchor)
    {
        switch (filter)
        {
            case DiscoverFilter.FitsMe:
                ToggleFitsMe();
                break;
            case DiscoverFilter.Race:
                OpenRaceMenu(anchor);
                return;
            case DiscoverFilter.Male:
            case DiscoverFilter.Female:
                var gender = filter == DiscoverFilter.Male ? MaleGender : FemaleGender;
                fitsMe = false;
                glams.SetBody(glams.Race, glams.Gender == gender ? null : gender);
                break;
            default:
                var vanilla = filter == DiscoverFilter.Vanilla;
                glams.SetVanillaOnly(glams.VanillaOnly == vanilla ? null : vanilla);
                break;
        }

        RefreshFiltered();
    }

    private void ToggleFitsMe()
    {
        if (fitsMe)
        {
            fitsMe = false;
            glams.SetBody(0, null);
            return;
        }

        var (race, gender) = WornBody();
        if (race > 0)
        {
            fitsMe = true;
            glams.SetBody(race, gender);
        }
    }

    private void OpenRaceMenu(Rect anchor)
    {
        FillRaceItems();
        raceMenu.Header = Loc.T(L.Prism.FilterRace);
        raceMenu.Toggle("prism.race", anchor);
    }

    private void FillRaceItems()
    {
        var female = glams.Gender == FemaleGender;
        raceItems[0] = new DropdownMenu.Item(Loc.T(L.Prism.AnyRace), Selected: glams.Race == 0, DividerAfter: true);
        for (var race = 1; race <= RaceCount; race++)
        {
            raceItems[race] = new DropdownMenu.Item(gameData.RaceName((uint)race, female), Selected: glams.Race == race);
        }
    }

    private void DrawRaceMenu(Rect full)
    {
        var picked = raceMenu.Draw(full, DropdownMenu.Style.From(ui), raceItems);
        if (picked < 0 || picked == glams.Race)
        {
            return;
        }

        fitsMe = false;
        glams.SetBody(picked, glams.Gender);
        FillRaceItems();
        RefreshFiltered();
    }

    private bool AnyFilterOn() => glams.Race > 0 || glams.Gender is not null || glams.VanillaOnly is not null;

    private void ClearFilters()
    {
        fitsMe = false;
        glams.SetBody(0, null);
        glams.SetVanillaOnly(null);
        RefreshFiltered();
    }

    private void RefreshFiltered()
    {
        pendingScroll = 0f;
        glams.Refresh(discoverScope);
    }

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
            var lineLeft = x;
            var lineRight = x;
            var lineTop = row.Center.Y;
            for (var index = 0; index < ScopeNames.Length; index++)
            {
                var size = Typography.Measure(scopeLabels[index], PrismType.Title);
                if (scopeActive[index])
                {
                    lineLeft = x;
                    lineRight = x + size.X;
                    lineTop = row.Center.Y + size.Y * 0.5f + Metrics.Space.Xxs * scale;
                }

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

            DrawScopeLine(drawList, lineLeft, lineRight, lineTop, scale);
        }

        if (picked < 0 || scopeActive[picked])
        {
            return;
        }

        discoverScope = ShownScopes[picked];
        pendingScroll = 0f;
        RefreshDiscover(false);
    }

    private void DrawScopeLine(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        if (!scopeLineShown)
        {
            scopeLineLeft.SnapTo(left);
            scopeLineRight.SnapTo(right);
            scopeLineShown = true;
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        PrismLight.Strip(drawList, scopeLineLeft.Step(left, Motion.Release, delta),
            scopeLineRight.Step(right, Motion.Release, delta), top, MathF.Max(1f, ScopeLineUnits * scale),
            PrismLight.TabAlpha);
    }

    private void RefreshDiscoverAlways() => RefreshDiscover(true);

    private void RefreshDiscover(bool always)
    {
        if (always || glams.Lane(discoverScope).NeverLoaded)
        {
            glams.Refresh(discoverScope);
        }

        if (always || glams.Lane(GlamScope.Showcase).NeverLoaded)
        {
            glams.Refresh(GlamScope.Showcase);
        }
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
        if (OpenContest() is not { } week)
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
            RunContestRow(week);
        }

        return row.Max.Y + Metrics.Space.Xxs * scale;
    }

    private void RunContestRow(PrismWeekDto week)
    {
        if (store.Busy)
        {
            return;
        }

        if (PrismPhases.Judgeable(week.Phase))
        {
            StartJudging();
        }
        else if (week.MyEntry is not null)
        {
            Go(PrismScreen.Entry);
        }
        else if (HasTracks)
        {
            OpenTab(PrismTab.Week);
        }
        else
        {
            StartPick();
        }
    }

    private PrismWeekDto? OpenContest() =>
        store.Week is { Id.Length: > 0 } week &&
        (week.Phase == PrismPhases.Entering || PrismPhases.Judgeable(week.Phase))
            ? week
            : null;

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
            PrismLight.Week(drawList, thumb, week.Id, PrismArt.Page, PrismArt.Fore, ContestMarkUnits * scale,
                ContestMarkShare);
        }

        var deadline = PrismCountdown.Deadline(week);
        var remaining = PrismCountdown.Remaining(deadline);
        var entered = week.Phase == PrismPhases.Entering && week.MyEntry is not null;
        var label = Loc.T(week.Phase == PrismPhases.Final ? L.Prism.JudgeFinal
            : week.Phase != PrismPhases.Entering ? L.Prism.JudgeHeat
            : entered ? L.Prism.YourLook : L.Prism.Enter);
        var textLeft = thumb.Max.X + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, row.Max.X - inset - textLeft);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        var themeLine = Typography.LineHeight(PrismType.Title);
        var pillHeight = Button.RegularHeight * scale;
        var pillWidth = MathF.Min(Typography.Measure(label, Button.LabelStyle(pillHeight)).X + pillHeight, room);
        var tiles = remaining > 0 &&
                    room - pillWidth - Metrics.Space.Sm * scale >= ContestTileUnits * CountdownUnitsShown * scale;
        var gap = Metrics.Space.Xxs * scale;
        var y = row.Center.Y - (captionHeight + gap + themeLine + Metrics.Space.Sm * scale + pillHeight) * 0.5f;
        var caption = tiles
            ? Loc.T(week.Phase == PrismPhases.Entering ? L.Prism.TabWeek : DeadlineLabel(week.Phase))
            : ContestCaption(week);
        Typography.Draw(drawList, new Vector2(textLeft, y), Typography.FitText(caption, room, PrismType.Caption),
            PrismArt.Muted, PrismType.Caption);
        y += captionHeight + gap;
        var themeStyle = FitStyle(week.Title, room, PrismType.Title);
        Typography.Draw(drawList, new Vector2(textLeft, y + (themeLine - Typography.LineHeight(themeStyle)) * 0.5f),
            Typography.FitText(week.Title, room, themeStyle), PrismArt.Fore, themeStyle);
        y += themeLine + Metrics.Space.Sm * scale;
        var pill = new Rect(new Vector2(textLeft, y), new Vector2(textLeft + pillWidth, y + pillHeight));
        Button.DrawLabel(drawList, Button.Surface(drawList, pill, controls,
            entered ? ButtonStyle.Gray : ButtonStyle.Solid, ButtonRole.Normal, true, hovered,
            ImGui.GetID("prism.contestRow")), label);
        if (tiles)
        {
            rowCountdown.DrawCompact(drawList,
                new Rect(new Vector2(pill.Max.X + Metrics.Space.Sm * scale, pill.Min.Y),
                    new Vector2(row.Max.X - inset, pill.Max.Y)), deadline, scale);
        }
    }

    private string ContestCover(PrismWeekDto week) =>
        CoverPlacing(ShownResults()) is { ImageUrl.Length: > 0 } winner
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
        LocString emptyHint, float scale, string emptyTitle = "")
    {
        var items = lane.Items;
        if (items.Length == 0 && lane.Loading)
        {
            return DrawGridPlaceholders(drawList, left, top, width, scale, ExploreColumns) + PrismArt.BottomMargin(scale);
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

        var gridBottom = DrawGlamGrid(drawList, left, top, width, items, scale);
        if (lane.LoadingMore)
        {
            gridBottom = DrawLoadingRowBelow(left, width, gridBottom);
        }

        return gridBottom + PrismArt.BottomMargin(scale);
    }

    private static float DrawLoadingRowBelow(float left, float width, float top)
    {
        var resume = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(left, top));
        InfiniteScroll.DrawLoadingRow(left + width * 0.5f, PrismArt.Muted);
        var bottom = ImGui.GetCursorScreenPos().Y;
        ImGui.SetCursorScreenPos(resume);
        return bottom;
    }

    private float DrawGlamGrid(ImDrawListPtr drawList, float left, float top, float width, GlamDto[] items, float scale)
    {
        for (var index = 0; index < items.Length; index++)
        {
            var photo = GridCell(left, top, width, index, scale, ExploreColumns);
            if (!ImGui.IsRectVisible(photo.Min, photo.Max))
            {
                continue;
            }

            var glam = items[index];
            if (DrawGridTile(drawList, photo, Cover(glam), string.Empty, PhoneIcons.HeartFilled,
                    glam.Likes > 0 ? NumberText.Compact(glam.Likes) : string.Empty, scale))
            {
                OpenGlam(glam);
            }
        }

        return top + GridHeight(width, items.Length, scale, ExploreColumns);
    }

    private static Rect GridCell(float left, float top, float width, int index, float scale,
        int columns = GridColumns)
    {
        var gap = Metrics.Space.Xxs * scale;
        var column = (width - gap * (columns - 1)) / columns;
        var min = new Vector2(left + index % columns * (column + gap),
            top + index / columns * GridPitch(width, scale, columns));
        return new Rect(min, min + new Vector2(column, column / GlamStore.PhotoRatio));
    }

    private static float GridHeight(float width, int count, float scale, int columns = GridColumns) =>
        (count + columns - 1) / columns * GridPitch(width, scale, columns);

    private static float GridPitch(float width, float scale, int columns)
    {
        var gap = Metrics.Space.Xxs * scale;
        return (width - gap * (columns - 1)) / columns / GlamStore.PhotoRatio + gap;
    }

    private float DrawGridPlaceholders(ImDrawListPtr drawList, float left, float top, float width, float scale,
        int columns = GridColumns)
    {
        for (var index = 0; index < PlaceholderRows * columns; index++)
        {
            PrismArt.Placeholder(drawList, GridCell(left, top, width, index, scale, columns), false, scale);
        }

        return top + GridHeight(width, PlaceholderRows * columns, scale, columns);
    }

    private string LaneAction() =>
        Loc.T(AnyFilterOn() ? L.Prism.ClearFilters
            : discoverScope == GlamScope.Following ? L.Prism.ScopePopular
            : L.Prism.PostGlam);

    private void RunLaneAction()
    {
        if (AnyFilterOn())
        {
            ClearFilters();
        }
        else if (discoverScope == GlamScope.Following)
        {
            discoverScope = GlamScope.Popular;
            RefreshDiscover(false);
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
        PhoneIcon.Draw(drawList, discCenter, glyph, PrismArt.Muted, PrismArt.EmptyIconUnits * scale);
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

    private static Rect AppArea(Rect full, Rect content) =>
        new(new Vector2(full.Min.X, content.Min.Y), new Vector2(full.Max.X, content.Max.Y));

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
