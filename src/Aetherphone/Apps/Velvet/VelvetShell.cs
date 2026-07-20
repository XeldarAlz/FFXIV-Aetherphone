using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell : IPhoneApp
{
    private const float HeartbeatSeconds = 45f;

    private readonly VelvetStore store;
    private readonly StoryPresenter stories;
    private readonly VelvetLauncher launcher;
    private readonly SocialLauncher socialLauncher;
    private readonly LodestoneService lodestone;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly PhotoLibrary library;
    private readonly HttpService http;
    private readonly RemoteImageCache images;
    private readonly SocialNotificationService social;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly AppSkin ui = new(VelvetTheme.Palette);
    private readonly RichTextCache detailBodyLayouts = new();
    private readonly RichTextCache commentLayouts = new();
    private readonly MentionPopup mentionPopup = new();
    private readonly MentionAutocomplete commentMentions;
    private readonly EmojiComposer commentEmoji = new();
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly PhotoCarousel carousel = new();
    private readonly AvatarComposer avatar;
    private readonly VelvetPostComposer post;
    private readonly ViewRouter<VelvetView> router;
    private readonly RouterDraw<VelvetView> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private VelvetPage activeTab = VelvetPage.Discover;
    private float sinceHeartbeat = HeartbeatSeconds;

    public VelvetShell(AethernetSession session, AethernetApi net, LodestoneService lodestone,
        Configuration configuration, PhotoLibrary library, HttpService http, RemoteImageCache images,
        NotificationService notifications, VelvetLauncher launcher, SocialLauncher socialLauncher, GameData gameData,
        SocialNotificationService social, KeyVault keyVault, ConversationKeyStore conversationKeys,
        PhoneVisibility visibility, RealtimeSignalBus realtimeSignals, WallpaperImageCache wallpaperImages,
        IAnalyticsService analytics, ConfirmService confirm, ReportService report)
    {
        store = new VelvetStore(session, net.Velvet, net.Account, net.Safety, net.Media, notifications, configuration,
            keyVault, conversationKeys, visibility, realtimeSignals, analytics);
        commentMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        stories = new StoryPresenter(session, net.Grams, net.Media, images, lodestone, VelvetArt.StoryRing, VelvetTheme.Palette,
            new StoryConfirmLabels(L.Velvet.DeleteConfirm, L.Velvet.DeleteCancel, L.Velvet.Saving), confirm,
            "Velvet stories", StartStoryCompose);
        this.launcher = launcher;
        this.socialLauncher = socialLauncher;
        this.lodestone = lodestone;
        this.configuration = configuration;
        this.gameData = gameData;
        this.library = library;
        this.http = http;
        this.images = images;
        this.social = social;
        this.confirm = confirm;
        this.report = report;
        this.wallpaperImages = wallpaperImages;
        avatar = new AvatarComposer(() => store.AvatarBusy, store.UpdateAvatar,
            new AvatarComposerLabels(L.Velvet.ChangePhoto, L.Velvet.ImportFromPc, L.Velvet.NoPhotos,
                L.Velvet.MoveAndScale, L.Velvet.Use, L.Velvet.Saving, L.Velvet.GestureHint), library,
            wallpaperImages);
        post = new VelvetPostComposer(store, stories, library, images, lodestone, wallpaperImages);
        router = new ViewRouter<VelvetView>(VelvetView.Root, Id);
        drawView = DrawView;
        back = () => router.Pop();
        threadView = new ThreadView(this);
    }

    public string Id => "velvet";

    public Vector4 Accent => AppAccents.For(Id);

    public string DisplayName => Loc.T(L.Apps.Velvet);

    public string Glyph => "Ve";

    public int BadgeCount => store.UnreadCount + store.RequestCount;

    public void OnOpened()
    {
        router.Reset();
        activeTab = VelvetPage.Discover;
        messagesTab = VelvetMessagesTab.Chats;
        store.InvalidateLists();
        if (GateAccepted && store.IsSignedIn)
        {
            store.EnsureMe();
            stories.RefreshTray();
        }

        if (launcher.TryConsume(out var targetUserId) && GateAccepted && configuration.IsVelvetOnboarded() &&
            store.IsSignedIn)
        {
            OpenThread(targetUserId);
        }

        if (socialLauncher.TryConsume(Id, out var link) && GateAccepted && configuration.IsVelvetOnboarded() &&
            store.IsSignedIn)
        {
            if (link.Kind == SocialLinkKind.Profile)
            {
                OpenProfile(link.Id);
            }
            else
            {
                store.EnsurePost(link.Id);
                router.Push(VelvetView.PostDetail(link.Id));
            }
        }
    }

    public void OnClosed()
    {
        router.Reset();
        avatarLightbox.Reset();
        store.ClearDiscover();
        filterSheet.Close();
        activeTab = VelvetPage.Discover;
        stories.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            EmptyState.Draw(context.Content, ui, FontAwesomeIcon.Moon, Loc.T(L.Velvet.SignedOutTitle),
                Loc.T(L.Velvet.SignedOutHint));
            return;
        }

        if (!GateAccepted)
        {
            TourHolds.Hold(Id);
            DrawGate(context.Content);
            return;
        }

        if (!configuration.IsVelvetOnboarded())
        {
            TourHolds.Hold(Id);
            DrawOnboarding(context.Content);
            return;
        }

        TourHolds.Release(Id);
        store.EnsureMe();
        TickHeartbeat();
        GateMenus();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        stories.Advance();
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        if (stories.Active)
        {
            stories.DrawViewer(screen, theme);
            return;
        }

        using (InputShield.Engage(avatarLightbox.Expanded))
        {
            router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        if (avatarLightbox.Active)
        {
            avatarLightbox.Draw(screen, theme);
        }
    }

    public void Dispose()
    {
        threadView.Dispose();
        stories.Dispose();
        store.Dispose();
    }

    private bool GateAccepted =>
        configuration.VelvetAcknowledgedGate &&
        configuration.VelvetAcknowledgedGateVersion >= Configuration.VelvetGateVersion;

    private void TickHeartbeat()
    {
        sinceHeartbeat += ImGui.GetIO().DeltaTime;
        if (sinceHeartbeat >= HeartbeatSeconds)
        {
            sinceHeartbeat = 0f;
            store.Heartbeat();
        }
    }

    private void DrawView(VelvetView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Screen)
        {
            case VelvetScreenId.Root:
                DrawRoot(area);
                break;
            case VelvetScreenId.Profile:
                DrawProfile(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Thread:
                threadView.Draw(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.PostDetail:
                DrawPostDetail(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Compose:
                DrawCompose(area);
                break;
            case VelvetScreenId.EditProfile:
                DrawEditProfile(area);
                break;
            case VelvetScreenId.Settings:
                DrawSettings(area);
                break;
            case VelvetScreenId.Activity:
                DrawActivity(area);
                break;
            case VelvetScreenId.Likers:
                DrawLikers(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Blocked:
                DrawBlocked(area);
                break;
            case VelvetScreenId.ChatImage:
                threadView.DrawImagePicker(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.ImageView:
                threadView.DrawImageViewer(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Intro:
                DrawIntro(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.RequestDetail:
                DrawRequestDetail(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Reactions:
                threadView.DrawReactions(area, view.Arg ?? string.Empty);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var headerHeight = VHeader.Height * scale;
        var tabHeight = VTabBar.Height * scale;
        var headerRect = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + headerHeight));
        var tabRect = new Rect(new Vector2(area.Min.X, area.Max.Y - tabHeight), area.Max);
        var bodyRect = new Rect(new Vector2(area.Min.X, headerRect.Max.Y),
            new Vector2(area.Max.X, tabRect.Min.Y));

        if (GuideIntents.Consume("velvet.tab.feed"))
        {
            activeTab = VelvetPage.Feed;
        }
        else if (GuideIntents.Consume("velvet.tab.messages"))
        {
            activeTab = VelvetPage.Messages;
        }
        else if (GuideIntents.Consume("velvet.tab.me"))
        {
            activeTab = VelvetPage.Me;
        }
        else if (GuideIntents.Consume("velvet.tab.discover"))
        {
            activeTab = VelvetPage.Discover;
        }

        var bellCenter = new Vector2(headerRect.Max.X - 20f * scale, headerRect.Min.Y + headerHeight * 0.5f);
        UiAnchors.Report("velvet.activity", AnchorBox(bellCenter, 18f * scale));

        var title = activeTab switch
        {
            VelvetPage.Feed => Loc.T(L.Velvet.TabFeed),
            VelvetPage.Messages => Loc.T(L.Velvet.Messages),
            VelvetPage.Me => Loc.T(L.Velvet.TabMe),
            _ => Loc.T(L.Velvet.TabDiscover),
        };
        if (VHeader.Root(headerRect, title, theme, 0))
        {
            router.Push(VelvetView.Activity);
        }

        switch (activeTab)
        {
            case VelvetPage.Feed:
                DrawFeed(bodyRect);
                break;
            case VelvetPage.Messages:
                DrawMessages(bodyRect);
                break;
            case VelvetPage.Me:
                DrawMe(bodyRect);
                break;
            default:
                DrawDiscover(bodyRect);
                break;
        }

        if (activeTab == VelvetPage.Discover)
        {
            DrawDiscoverFilterSheet(area);
        }

        var messageBadge = store.UnreadCount + store.RequestCount;
        var tabs = new[]
        {
            new VTabDef(FontAwesomeIcon.Compass, Loc.T(L.Velvet.TabDiscover)),
            new VTabDef(FontAwesomeIcon.Image, Loc.T(L.Velvet.TabFeed)),
            new VTabDef(FontAwesomeIcon.Comment, Loc.T(L.Velvet.Messages), messageBadge),
            new VTabDef(FontAwesomeIcon.User, Loc.T(L.Velvet.TabMe)),
        };
        var tabMargin = 12f * scale;
        var cellWidth = (tabRect.Width - tabMargin * 2f) / 4f;
        var tabLeft = tabRect.Min.X + tabMargin;
        var tabMidY = (tabRect.Min.Y + 6f * scale + tabRect.Max.Y - 12f * scale) * 0.5f;
        UiAnchors.Report("velvet.tab.discover", AnchorBox(new Vector2(tabLeft + cellWidth * 0.5f, tabMidY), 22f * scale));
        UiAnchors.Report("velvet.tab.feed", AnchorBox(new Vector2(tabLeft + cellWidth * 1.5f, tabMidY), 22f * scale));
        UiAnchors.Report("velvet.tab.messages", AnchorBox(new Vector2(tabLeft + cellWidth * 2.5f, tabMidY), 22f * scale));
        UiAnchors.Report("velvet.tab.me", AnchorBox(new Vector2(tabLeft + cellWidth * 3.5f, tabMidY), 22f * scale));

        var picked = VTabBar.Draw(tabRect, tabs, (int)activeTab, scale);
        if (picked >= 0)
        {
            activeTab = (VelvetPage)picked;
            if (activeTab != VelvetPage.Discover)
            {
                filterSheet.Close();
            }
        }
    }

    private void DrawRichBody(ImDrawListPtr drawList, RichTextLayout layout, Vector2 origin)
    {
        var ink = new RichTextInk(VelvetTheme.BodyInk, VelvetTheme.RoseGlow, VelvetTheme.RoseGlow);
        RichText.Draw(drawList, layout, origin, ink, out var hit);
        if (hit.Kind == RichTextRunKind.Mention && hit.Clicked)
        {
            OpenProfile(layout.Mentions[hit.TargetIndex].UserId);
        }
    }

    private void OpenProfile(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        filterSheet.Close();
        store.OpenProfile(userId);
        router.Push(VelvetView.Profile(userId));
    }

    private void OpenThread(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        filterSheet.Close();
        activeTab = VelvetPage.Messages;
        router.Push(VelvetView.Thread(userId));
    }
}
