using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Feedback;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Feedback;

internal sealed partial class FeedbackApp : IPhoneApp
{
    private const long CooldownSeconds = 60;
    private const int SendIdle = 0;
    private const int SendSucceeded = 1;
    private const int SendFailed = 2;
    private const float SectionGap = 22f;
    private const float HeaderGap = 10f;
    private const float CardPad = 14f;
    private const float BottomBreathing = 24f;

    public string Id => FeedbackStore.AppId;
    public string DisplayName => Loc.T(L.Apps.Feedback);
    public string Glyph => "Fb";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => store.UnseenCount;
    public bool HasBadge => true;

    private readonly FeedbackStore store;
    private readonly FeedbackDraft draft = new();
    private readonly FeedbackDeviceInfo deviceInfo = new();
    private readonly PhotoLibrary library;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly RemoteImageCache remoteImages;
    private readonly GameData gameData;
    private readonly FeedbackLauncher launcher;
    private readonly AppSkin ui = new(AppPalettes.Feedback);
    private readonly ViewRouter<FeedbackRoute> router;
    private readonly RouterDraw<FeedbackRoute> drawView;
    private readonly Action back;
    private readonly Action refreshHistory;
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly FailureSlot sendFailure = new();
    private readonly NavBarButton[] composeButtons = new NavBarButton[1];
    private readonly NavBarButton[] photosButtons = new NavBarButton[1];

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int sendOutcome;
    private volatile AepFailureBox? sendOutcomeFailure;
    private bool resumeCompose;

    public FeedbackApp(AethernetSession session, FeedbackClient client, MediaClient media, PhotoLibrary library,
        Configuration configuration, ConfirmService confirm, WallpaperImageCache wallpaperImages,
        RemoteImageCache remoteImages, GameData gameData, NotificationService notifications,
        RealtimeSignalBus signals, FeedbackLauncher launcher)
    {
        store = new FeedbackStore(session, client, media, notifications, configuration, signals);
        this.launcher = launcher;
        this.library = library;
        this.configuration = configuration;
        this.confirm = confirm;
        this.wallpaperImages = wallpaperImages;
        this.remoteImages = remoteImages;
        this.gameData = gameData;
        router = new ViewRouter<FeedbackRoute>(FeedbackRoute.Hub);
        drawView = DrawView;
        back = () => router.Pop();
        refreshHistory = store.RefreshHistory;
    }

    public void OnOpened()
    {
        router.Reset();
        photoViewer.Close();
        deviceInfo.Capture(gameData, configuration);
        if (store.IsSignedIn && launcher.TryConsumeDetail(out var feedbackId))
        {
            router.Push(FindHistoryItem(feedbackId) is null ? FeedbackRoute.History : FeedbackRoute.Detail(feedbackId),
                false);
        }
        else if (resumeCompose && !draft.IsEmpty)
        {
            router.Push(FeedbackRoute.Compose, false);
        }

        resumeCompose = false;
        store.ResetHistorySupport();
        store.RefreshHistory();
    }

    public void OnClosed()
    {
        var current = router.Current.Screen;
        resumeCompose = current is FeedbackScreen.Compose or FeedbackScreen.Photos;
        router.Reset();
        photoViewer.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        ConsumeSendOutcome();
        ConsumePickedFile();

        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            ui.Body(context.Content);
            DrawSignedOut(context.Content);
            return;
        }

        TourHolds.Release(Id);
        if (store.HistoryState == FeedbackHistoryState.Unknown)
        {
            store.RefreshHistory();
        }

        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(FeedbackRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case FeedbackScreen.Compose:
                DrawCompose(area);
                return;
            case FeedbackScreen.Photos:
                DrawPhotos(area);
                return;
            case FeedbackScreen.Sent:
                DrawSent(area);
                return;
            case FeedbackScreen.History:
                DrawHistory(area);
                return;
            case FeedbackScreen.Detail:
                DrawDetail(area, route.Id ?? string.Empty);
                return;
            default:
                DrawHub(area);
                return;
        }
    }

    private void DrawSignedOut(Rect content)
    {
        var context = new PhoneContext(content, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            var scale = UiScale.Current;
            var width = ImGui.GetContentRegionAvail().X;
            var origin = ImGui.GetCursorScreenPos();
            var title = Loc.T(L.Feedback.SignInTitle);
            var hint = Loc.T(L.Feedback.SignInHint);
            var height = FeedbackArt.StatePanelHeight(title, hint, true, width, scale);
            var top = MathF.Max(origin.Y + Metrics.Space.Xxl * scale, body.Center.Y - height * 0.6f);
            if (FeedbackArt.StatePanel(ui, top, origin.X + width * 0.5f, width, FontAwesomeIcon.UserLock, ui.Accent,
                    title, hint, Loc.T(L.Feedback.OpenSettings), "feedback.signin"))
            {
                navigation.Open("settings");
            }

            ReserveTo(origin, width, top + height);
        }

        AppHeader.EndLargeTitle(in navBar, context, "feedback.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void ConsumeSendOutcome()
    {
        var outcome = Interlocked.Exchange(ref sendOutcome, SendIdle);
        if (outcome == SendIdle)
        {
            return;
        }

        if (outcome == SendFailed)
        {
            sendFailure.Set(sendOutcomeFailure?.Failure ?? AepFailure.Transport(AepFailureKind.Offline));
            UiFeedback.Play(UiSound.Caution);
            if (router.Current.Screen != FeedbackScreen.Compose)
            {
                ShellToast.Show(Loc.T(L.Feedback.SendFailed));
            }

            return;
        }

        sendFailure.Clear();
        if (string.Equals(draft.Text, sentText, StringComparison.Ordinal))
        {
            draft.Clear();
        }

        configuration.LastFeedbackSentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        configuration.Save();
        store.NoteSent();
        UiFeedback.Play(UiSound.MessageSent);
        store.RefreshHistory();
        if (router.Current.Screen == FeedbackScreen.Compose)
        {
            BeginThanks();
            router.Replace(FeedbackRoute.Sent);
            return;
        }

        ShellToast.Show(Loc.T(L.Feedback.SentToast));
    }

    private int CooldownRemaining()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = CooldownSeconds - (now - configuration.LastFeedbackSentUnix);
        return remaining > 0 ? (int)remaining : 0;
    }

    private static float DrawSectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        Vector4 ink)
    {
        var fitted = Typography.FitText(title, width, TextStyles.Title3);
        Typography.Draw(drawList, origin, fitted, ink, TextStyles.Title3);
        return Typography.Measure(fitted, TextStyles.Title3).Y;
    }

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    private static ImRaii.StyleDisposable ZeroItemSpacing() => ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

    public void Dispose()
    {
        store.Dispose();
    }
}
