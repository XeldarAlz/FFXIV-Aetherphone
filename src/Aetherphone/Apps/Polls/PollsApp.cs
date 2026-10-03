using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Polls;

internal sealed partial class PollsApp : IPhoneApp
{
    private const float RefreshSeconds = 60f;
    private const float CardGap = 14f;
    private const float BannerHeight = 26f;
    private const float BottomPad = 24f;
    private const int SkeletonCards = 2;
    private const int SkeletonOptions = 3;

    public string Id => "polls";
    public string DisplayName => Loc.T(L.Apps.Polls);
    public string Glyph => "Po";
    public int BadgeCount => store.UnvotedCount;
    public bool HasBadge => true;

    private readonly PollsStore store;
    private readonly AppSkin ui = new(AppPalettes.Polls);
    private readonly PullToRefresh listRefresh = new();
    private readonly Dictionary<string, PollMotion> motions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PollText> texts = new(StringComparer.Ordinal);
    private readonly List<PollDto> orderedPolls = new();
    private readonly Action refreshAll;

    private PollDto[]? orderedSource;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private PollVoteFailure? announcedFailure;
    private string widestPercent = string.Empty;
    private string widestPercentLang = string.Empty;
    private float sinceRefresh;

    public PollsApp(AethernetSession session, PollsClient client, AppInstaller installer, RealtimeSignalBus signals)
    {
        store = new PollsStore(session, client, installer.Gate("polls"), signals);
        refreshAll = RefreshAll;
    }

    public void OnOpened()
    {
        sinceRefresh = 0f;
        RefreshAll();
    }

    private void RefreshAll()
    {
        orderedSource = null;
        store.Refresh();
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var area = context.Content;
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(area, theme, scale));
        ui.Body(area);

        var navBar = AppHeader.BeginLargeTitle(context, false);
        var body = navBar.Body;
        if (store.IsSignedIn)
        {
            DrawSignedIn(body, scale);
        }
        else
        {
            TourHolds.Hold(Id);
            DrawSignedOut(body);
        }

        AppHeader.EndLargeTitle(in navBar, context, "polls.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawSignedOut(Rect body)
    {
        using (AppSurface.Begin(body))
        {
            if (PollsArt.StateScreen(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.Polls.SignInTitle),
                    Loc.T(L.Polls.SignInRequired), Loc.T(L.Polls.OpenSettings)))
            {
                navigation.Open("settings");
            }
        }
    }

    private void DrawSignedIn(Rect body, float scale)
    {
        TickRefresh();
        SyncOrder();
        AnnounceFailure();

        using var surface = AppSurface.Begin(body);
        listRefresh.Draw(body, surface.Pull, surface.Dragging, store.Loading, ui.MutedInk, refreshAll);
        if (!store.LoadedOnce)
        {
            TourHolds.Hold(Id);
            if (store.ListFailed && !store.Loading)
            {
                DrawLoadFailed(body);
                return;
            }

            DrawSkeletons(scale);
            return;
        }

        if (store.ListFailed)
        {
            DrawRefreshBanner(scale);
        }

        if (orderedPolls.Count == 0)
        {
            TourHolds.Hold(Id);
            DrawEmpty(body);
            return;
        }

        TourHolds.Release(Id);
        DrawCards(scale);
        if (store.LoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
        }
        else if (store.HasMore && InfiniteScroll.ReachedBottom())
        {
            store.LoadMore();
        }

        ImGui.Dummy(new Vector2(0f, BottomPad * scale));
    }

    private void DrawCards(float scale)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var width = ImGui.GetContentRegionAvail().X;
        for (var index = 0; index < orderedPolls.Count; index++)
        {
            DrawCard(orderedPolls[index], width, scale, nowUnix, index == 0);
        }
    }

    private void DrawSkeletons(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PollsArt.SkeletonHeight(SkeletonOptions, scale);
        var phase = (float)ImGui.GetTime();
        for (var index = 0; index < SkeletonCards; index++)
        {
            var origin = ImGui.GetCursorScreenPos();
            PollsArt.SkeletonCard(drawList, ui, origin, width, height, SkeletonOptions, scale, phase);
            ImGui.Dummy(new Vector2(width, height + CardGap * scale - ImGui.GetStyle().ItemSpacing.Y));
        }
    }

    private void DrawLoadFailed(Rect body)
    {
        var reason = store.ListFailureText;
        if (!PollsArt.StateScreen(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Common.LoadFailed),
                reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry)))
        {
            return;
        }

        UiFeedback.Play(UiSound.Refresh);
        RefreshAll();
    }

    private void DrawRefreshBanner(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = BannerHeight * scale;
        var centerY = origin.Y + height * 0.5f;
        var iconSize = Typography.LineHeight(TextStyles.Footnote) * 0.7f;
        PollsArt.Glyph(drawList, new Vector2(origin.X + iconSize * 0.5f, centerY), FontAwesomeIcon.ExclamationCircle,
            ui.MutedInk, iconSize);
        var text = Loc.T(L.Polls.RefreshFailed);
        var textLeft = origin.X + iconSize + Metrics.Space.Xs * scale;
        var fitted = Typography.FitText(text, ImGui.GetContentRegionAvail().X - (textLeft - origin.X),
            TextStyles.Footnote);
        var textHeight = Typography.Measure(fitted, TextStyles.Footnote).Y;
        Typography.Draw(drawList, new Vector2(textLeft, centerY - textHeight * 0.5f), fitted, ui.MutedInk,
            TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        ImGui.Dummy(new Vector2(0f, height));
    }

    private void DrawEmpty(Rect body)
    {
        PollsArt.StateScreen(body, ui, FontAwesomeIcon.CheckSquare, Loc.T(L.Polls.EmptyOpenTitle),
            Loc.T(L.Polls.EmptyOpenHint), string.Empty);
    }

    private void TickRefresh()
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private void SyncOrder()
    {
        var source = store.Polls;
        if (ReferenceEquals(source, orderedSource))
        {
            return;
        }

        orderedSource = source;
        PollRules.Order(source, orderedPolls);
    }

    private void AnnounceFailure()
    {
        var failure = store.VoteFailure;
        if (failure is null || ReferenceEquals(failure, announcedFailure))
        {
            return;
        }

        announcedFailure = failure;
        UiFeedback.Play(UiSound.Caution);
    }

    private string WidestPercent()
    {
        var code = Loc.Current.Code;
        if (widestPercentLang != code)
        {
            widestPercentLang = code;
            widestPercent = Loc.T(L.Polls.Percent, 100);
        }

        return widestPercent;
    }

    private PollText TextFor(PollDto poll, long nowUnix)
    {
        if (!texts.TryGetValue(poll.Id, out var text))
        {
            text = new PollText();
            texts[poll.Id] = text;
        }

        text.Sync(poll, nowUnix);
        return text;
    }

    private PollMotion MotionFor(PollDto poll)
    {
        if (!motions.TryGetValue(poll.Id, out var motion))
        {
            motion = new PollMotion();
            motions[poll.Id] = motion;
        }

        if (motion.Fills.Length != poll.Options.Length)
        {
            motion.Fills = new Spring[poll.Options.Length];
            motion.Selection = new Spring[poll.Options.Length];
            motion.Primed = false;
        }

        return motion;
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
