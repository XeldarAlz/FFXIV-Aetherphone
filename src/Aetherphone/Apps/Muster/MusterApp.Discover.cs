using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const int DataCenterButton = 0;
    private const int RulesButton = 1;
    private const int PosterTitleLines = 2;
    private const int SkeletonCards = 3;
    private const float ScopeStripHeight = 34f;
    private const float ScopeGap = 10f;
    private const float RailGap = 6f;
    private const float SkeletonHeight = 150f;
    private const float StateTop = 40f;
    private const float LoadMoreHeight = 44f;
    private const float LoadMoreWidth = 180f;

    private readonly NavBarButton[] discoverButtons = new NavBarButton[2];
    private readonly string[] scopeLabels = new string[3];
    private readonly string[] chipLabels = new string[MusterCategories.All.Length + 1];
    private readonly bool[] chipActive = new bool[MusterCategories.All.Length + 1];
    private readonly ChipRail categoryRail = new();
    private readonly PullToRefresh discoverRefresh = new();
    private Action? refreshEverything;
    private bool cardAnchorReported;

    private void DrawDiscover(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var nowUnix = NowUnix();
        cardAnchorReported = false;
        refreshEverything ??= RefreshEverything;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("muster.discover"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            discoverRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging,
                store.Syncing || store.DirectoryLoading, ui.MutedInk, refreshEverything);
            DrawScopeRow(scale);
            DrawCategoryRail(scale);
            DrawDiscoverBody(navBar.Body, nowUnix, scale);
        }

        discoverButtons[DataCenterButton] =
            new NavBarButton(IconGlyph.Of(FontAwesomeIcon.GlobeAmericas), Loc.T(L.Muster.DataCenterSection));
        discoverButtons[RulesButton] =
            new NavBarButton(IconGlyph.Of(FontAwesomeIcon.QuestionCircle), Loc.T(L.Conduct.Eyebrow));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "muster.discover.nav", DisplayName,
            NavBarStyle.From(ui), discoverButtons);
        if (pressed == DataCenterButton)
        {
            UiFeedback.Play(UiSound.Tap);
            router.Push(MusterRoute.DataCenter(DisplayName));
        }
        else if (pressed == RulesButton)
        {
            UiFeedback.Play(UiSound.Tap);
            conduct.ShowRules(Id);
        }
    }

    private void DrawScopeRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + ScopeStripHeight * scale));
        var pinned = configuration.MusterDataCenterId != 0
            ? MusterDataCenters.Name(configuration.MusterDataCenterId)
            : string.Empty;
        scopeLabels[0] = pinned.Length > 0 ? pinned : Loc.T(L.Muster.ScopeMyDc);
        scopeLabels[1] = Loc.T(L.Muster.ScopeRegion);
        scopeLabels[2] = Loc.T(L.Muster.ScopeEverywhere);
        UiAnchors.Report("muster.scope", row);
        var selected = Math.Clamp(configuration.MusterScope, MusterScopes.MyDataCenter, MusterScopes.Everywhere);
        var next = SegmentStrip.Draw("##musterScope", row, scopeLabels, selected, Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary),
            ui.Accent, ui.MutedInk, AccentRing.Ink, ScopeStripHeight, TextStyles.SubheadlineEmphasized.Scale);
        if (next != selected && next >= 0)
        {
            UiFeedback.Play(UiSound.Tap);
            configuration.MusterScope = next;
            configuration.Save();
            store.RefreshDirectory();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (ScopeStripHeight + ScopeGap) * scale));
    }

    private void DrawCategoryRail(float scale)
    {
        var categories = MusterCategories.All;
        var mask = configuration.MusterCategoryFilter;
        chipLabels[0] = Loc.T(L.Muster.FilterAll);
        chipActive[0] = mask == 0;
        for (var index = 0; index < categories.Length; index++)
        {
            chipLabels[index + 1] = Loc.T(MusterCategories.Label(categories[index]));
            chipActive[index + 1] = (mask & (1 << categories[index])) != 0;
        }

        var tapped = categoryRail.Draw(ui, chipLabels, chipActive, "muster.categories");
        ImGui.Dummy(new Vector2(0f, RailGap * scale));
        if (tapped < 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        configuration.MusterCategoryFilter = tapped == 0 ? 0 : mask ^ (1 << categories[tapped - 1]);
        configuration.Save();
        store.RefreshDirectory();
    }

    private void ClearCategoryFilter()
    {
        UiFeedback.Play(UiSound.Tap);
        configuration.MusterCategoryFilter = 0;
        configuration.Save();
        store.RefreshDirectory();
    }

    private void DrawDiscoverBody(Rect body, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        float bottom;
        if (sections.DiscoverCount > 0)
        {
            bottom = DrawDiscoverSections(drawList, origin, width, nowUnix, scale);
        }
        else if (store.DirectoryLoading && !store.DirectoryLoadedOnce)
        {
            bottom = DrawSkeleton(drawList, origin, width, scale);
        }
        else
        {
            bottom = DrawDiscoverState(drawList, origin, width, scale);
        }

        MusterArt.Reserve(origin, width, bottom);
        DrawLoadMore(scale);
        ImGui.Dummy(new Vector2(0f, MusterArt.BottomPad * scale));
    }

    private float DrawDiscoverSections(ImDrawListPtr drawList, Vector2 origin, float width, long nowUnix,
        float scale)
    {
        var cursorY = origin.Y;
        var currentDataCenterId = store.CurrentDataCenterId;
        cursorY = DrawPosterSection(drawList, sections.Friends, Loc.T(L.Muster.FriendsSection), origin.X, cursorY,
            width, nowUnix, currentDataCenterId, scale);
        cursorY = DrawPosterSection(drawList, sections.Live, Loc.T(L.Muster.HappeningNow), origin.X, cursorY, width,
            nowUnix, currentDataCenterId, scale);
        return DrawPosterSection(drawList, sections.Soon, Loc.T(L.Muster.StartingSoon), origin.X, cursorY, width,
            nowUnix, currentDataCenterId, scale);
    }

    private float DrawPosterSection(ImDrawListPtr drawList, List<MusterDto> items, string title, float left,
        float top, float width, long nowUnix, int currentDataCenterId, float scale)
    {
        if (items.Count == 0)
        {
            return top;
        }

        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, title, ui.TitleInk);
        for (var index = 0; index < items.Count; index++)
        {
            var muster = items[index];
            var poster = PosterFor(muster, nowUnix, currentDataCenterId);
            var height = MusterArt.PosterHeight(in poster, width, PosterTitleLines, scale);
            var min = new Vector2(left, cursorY);
            if (!cardAnchorReported && ImGui.IsRectVisible(min, min + new Vector2(width, height)))
            {
                cardAnchorReported = true;
                UiAnchors.Report("muster.card", new Rect(min, min + new Vector2(width, height)));
            }

            if (MusterArt.Poster(drawList, KeyFor("poster", muster.Id), in poster, min, width, PosterTitleLines,
                    theme, images, lodestone, true, scale))
            {
                OpenDetail(muster.Id);
            }

            cursorY += height + MusterArt.CardGap * scale;
        }

        return cursorY + (MusterArt.SectionGap - MusterArt.CardGap) * scale;
    }

    private MusterPoster PosterFor(MusterDto muster, long nowUnix, int currentDataCenterId)
    {
        var description = muster.Description.Length > 0
            ? muster.Description
            : Loc.T(MusterCategories.Label(muster.Category));
        return new MusterPoster
        {
            Category = muster.Category,
            Eyebrow = Loc.T(MusterCategories.Label(muster.Category)),
            Title = description,
            Status = labels.Countdown(muster, nowUnix),
            Live = muster.StartsAtUnix <= nowUnix,
            Going = IsGoing(muster),
            HostName = MusterText.HostLabel(muster),
            HostWorld = muster.HostWorld,
            HostFrameId = muster.HostFrameId,
            Identity = MusterText.Identity(muster),
            Place = MusterText.Place(muster),
            Count = labels.Count(muster),
            Full = muster.MaxAttendees > 0 && muster.RsvpCount >= muster.MaxAttendees,
            OtherDataCenter = currentDataCenterId != 0 && muster.DataCenterId != 0
                && muster.DataCenterId != currentDataCenterId,
        };
    }

    private float DrawSkeleton(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var cursorY = origin.Y + CardSectionHeader.HeightUnits * scale;
        for (var index = 0; index < SkeletonCards; index++)
        {
            var min = new Vector2(origin.X, cursorY);
            var max = new Vector2(origin.X + width, cursorY + SkeletonHeight * scale);
            MusterArt.Skeleton(drawList, ui, min, max, scale);
            cursorY = max.Y + MusterArt.CardGap * scale;
        }

        return cursorY;
    }

    private float DrawDiscoverState(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var centerX = origin.X + width * 0.5f;
        var top = origin.Y + StateTop * scale;
        float bottom;
        if (store.DirectoryFailed)
        {
            bottom = MusterArt.StateScreen(drawList, ui, centerX, top, width, FontAwesomeIcon.CloudDownloadAlt,
                Loc.T(L.Common.LoadFailed), Loc.T(L.Common.LoadFailedHint), scale);
            if (MusterArt.StateAction(ui, centerX, bottom, width, Loc.T(L.Common.Retry), scale, out bottom))
            {
                RefreshEverything();
            }

            return bottom;
        }

        if (configuration.MusterCategoryFilter != 0)
        {
            bottom = MusterArt.StateScreen(drawList, ui, centerX, top, width, FontAwesomeIcon.Filter,
                Loc.T(L.Muster.FilteredEmptyTitle), Loc.T(L.Muster.FilteredEmptyHint), scale);
            if (MusterArt.StateAction(ui, centerX, bottom, width, Loc.T(L.Muster.ShowAllActivities), scale,
                    out bottom))
            {
                ClearCategoryFilter();
            }

            return bottom;
        }

        bottom = MusterArt.StateScreen(drawList, ui, centerX, top, width, FontAwesomeIcon.Bullhorn,
            Loc.T(L.Muster.EmptyTitle), Loc.T(L.Muster.EmptyHint), scale);
        if (store.Mine is not null)
        {
            return bottom;
        }

        if (MusterArt.StateAction(ui, centerX, bottom, width, Loc.T(L.Muster.StartMuster), scale, out bottom))
        {
            OpenCreate();
        }

        return bottom;
    }

    private void DrawLoadMore(float scale)
    {
        if (!store.DirectoryHasMore || sections.DiscoverCount == 0)
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
                ui.Accent);
        }
        else
        {
            var buttonWidth = MathF.Min(LoadMoreWidth * scale, width);
            var rect = new Rect(new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y),
                new Vector2(origin.X + (width + buttonWidth) * 0.5f, origin.Y + height));
            if (ui.GhostButton(rect, Loc.T(L.Muster.LoadMore)))
            {
                UiFeedback.Play(UiSound.Tap);
                store.LoadMoreDirectory();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
