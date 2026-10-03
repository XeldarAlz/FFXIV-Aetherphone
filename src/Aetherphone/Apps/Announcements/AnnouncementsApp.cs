using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Announcements;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp : IPhoneApp
{
    private const float RefreshSeconds = 30f;
    private const string SettingsAppId = "settings";

    public string Id => AnnouncementsStore.AppId;
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Announcements);
    public string Glyph => "An";
    public int BadgeCount => store.UnreadCount;
    public bool HasBadge => true;
    public bool WantsSystemTheme => true;

    private readonly AnnouncementsStore store;
    private readonly FailureSlot listFailure = new();
    private readonly AnnouncementsLauncher launcher;
    private readonly AppSkin ui = new(AppPalettes.Announcements(PhoneTheme.Default));
    private readonly ViewRouter<AnnouncementsRoute> router;
    private readonly RouterDraw<AnnouncementsRoute> drawView;
    private readonly Action back;
    private readonly ScreenToast toast = new();
    private readonly Dictionary<string, AnnouncementEntry> entryCache = new(StringComparer.Ordinal);

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float sinceRefresh;
    private bool resetScroll;
    private float fontKey;
    private float headlineLineHeight;
    private float subheadlineLineHeight;
    private float deltaSeconds;

    private AnnouncementEntry[] entries = Array.Empty<AnnouncementEntry>();
    private AnnouncementDto[]? entriesSource;
    private LanguageInfo? entriesLanguage;
    private int entriesFormatVersion = -1;
    private DateTime entriesDay;
    private bool anyUnread;

    public AnnouncementsApp(AethernetSession session, AnnouncementsClient client, NotificationService notifications,
        Configuration configuration, AnnouncementsLauncher launcher, PhoneVisibility visibility,
        RealtimeSignalBus signals)
    {
        store = new AnnouncementsStore(session, client, notifications, configuration, visibility, signals);
        this.launcher = launcher;
        router = new ViewRouter<AnnouncementsRoute>(AnnouncementsRoute.List);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        sinceRefresh = 0f;
        resetScroll = true;
        readingId = null;
        pendingSwapId = null;
        olderRequestedAt = -1;
        query = string.Empty;
        store.BeginVisit();
        if (launcher.TryConsumeDetail(out var announcementId))
        {
            router.Push(AnnouncementsRoute.Detail(announcementId), false);
        }

        store.Refresh();
        store.MarkAllSeen();
    }

    public void OnClosed()
    {
        if (readingId is not null)
        {
            store.MarkRead(readingId);
            readingId = null;
        }

        router.Reset();
        store.MarkAllSeen();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        ui.Palette = AppPalettes.Announcements(theme);
        deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        fontKey = Typography.LineHeight(TextStyles.Body);
        headlineLineHeight = Typography.LineHeight(TextStyles.Headline);
        subheadlineLineHeight = Typography.LineHeight(TextStyles.Subheadline);

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);

        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            DrawSignedOut(context);
            return;
        }

        TickRefresh();
        SyncEntries();
        ApplyPendingSwap();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        toast.Draw(screen, ScreenToastStyle.From(theme));
        UpdateTourHold();
    }

    private void DrawSignedOut(in PhoneContext context)
    {
        ui.Body(context.Content);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        if (AnnouncementsStatePanel.Draw(navBar.Body, ui, FontAwesomeIcon.UserLock,
                Loc.T(L.Announcements.SignInTitle), Loc.T(L.Announcements.SignInRequired),
                Loc.T(L.Announcements.OpenSettings)))
        {
            navigation.Open(SettingsAppId);
        }

        AppHeader.EndLargeTitle(in navBar, context, "announcements.nav", DisplayName, NavStyle(),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void UpdateTourHold()
    {
        if (router.Depth == 1 && entries.Length > 0)
        {
            TourHolds.Release(Id);
            return;
        }

        TourHolds.Hold(Id);
    }

    private void DrawView(AnnouncementsRoute route, Rect area, int depth)
    {
        ui.Body(area);
        if (route.Screen == AnnouncementsScreen.Detail)
        {
            DrawDetail(area, route.Id!, depth);
            return;
        }

        DrawList(area, depth);
    }

    private void TickRefresh()
    {
        if (store.PushCovered)
        {
            sinceRefresh = 0f;
            return;
        }

        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private void SyncEntries()
    {
        var snapshot = store.Announcements;
        var language = Loc.Current;
        var formatVersion = TimeText.FormatVersion;
        var today = DateTime.Now.Date;
        if (!ReferenceEquals(snapshot, entriesSource) || !ReferenceEquals(language, entriesLanguage)
            || formatVersion != entriesFormatVersion || today != entriesDay)
        {
            RebuildEntries(snapshot);
            entriesSource = snapshot;
            entriesLanguage = language;
            entriesFormatVersion = formatVersion;
            entriesDay = today;
            filterDirty = true;
        }

        anyUnread = false;
        for (var index = 0; index < entries.Length; index++)
        {
            if (store.IsUnread(entries[index].Source))
            {
                anyUnread = true;
                return;
            }
        }
    }

    private void RebuildEntries(AnnouncementDto[] snapshot)
    {
        var rebuilt = entries.Length == snapshot.Length ? entries : new AnnouncementEntry[snapshot.Length];
        for (var index = 0; index < snapshot.Length; index++)
        {
            var announcement = snapshot[index];
            if (!entryCache.TryGetValue(announcement.Id, out var entry))
            {
                entry = new AnnouncementEntry();
                entryCache[announcement.Id] = entry;
            }

            entry.Rebuild(announcement);
            rebuilt[index] = entry;
        }

        entries = rebuilt;
    }

    private int IndexOf(string announcementId)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].Source.Id == announcementId)
            {
                return index;
            }
        }

        return -1;
    }

    private void Open(AnnouncementEntry entry)
    {
        router.Push(AnnouncementsRoute.Detail(entry.Source.Id));
    }

    private NavBarStyle NavStyle() => new(ui.TitleInk, ui.Accent, ui.Palette.BackdropTop);

    private Rect Lift(ImDrawListPtr drawList, Rect rest, string id, bool hovered, float rounding, float scale)
    {
        var eased = HoverFx.Amount(id, hovered);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, PressFx.CardPressedScale);
        var factor = (1f + Motion.HoverLiftCard * eased) * press;
        var half = rest.Size * 0.5f * factor;
        var card = new Rect(rest.Center - half, rest.Center + half);
        ui.Card(drawList, card.Min, card.Max, rounding * factor, true);
        return card;
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 tip, float size, float thickness, Vector4 color,
        bool pointsRight)
    {
        var packed = ImGui.GetColorU32(color);
        var armX = pointsRight ? tip.X - size : tip.X + size;
        drawList.AddLine(new Vector2(armX, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(armX, tip.Y + size), packed, thickness);
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
