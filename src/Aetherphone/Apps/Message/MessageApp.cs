using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp : IResumableApp, ISpotlightConversations
{
    private enum MessageTab : byte
    {
        Chats,
        Calls,
        Contacts,
        Settings,
    }

    private const int TabCount = 4;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 CallGreen = new(0.20f, 0.78f, 0.35f, 1f);

    public string Id => "message";
    public string DisplayName => Loc.T(L.Apps.Message);
    public string Glyph => "Me";
    public int BadgeCount => store.UnreadTotal + calls.UnseenMissed;
    public bool HasBadge => true;
    public ConversationDto[] SearchableConversations => store.Conversations;

    public ConversationDto[] SpotlightConversations => store.Conversations;

    private readonly DirectMessagesStore store;
    private readonly FailureSlot threadListFailure = new();
    private readonly ContactBook contacts;
    private readonly CallHub calls;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly DmLauncher launcher;
    private readonly PhotoLibrary library;
    private readonly HttpService http;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly ReportService report;
    private readonly WallpaperImageCache wallpaperImages;
    internal readonly EncryptionHelpService encryptionHelp;
    private readonly MusterStore musters;
    private readonly MusterLauncher musterLauncher;
    private readonly SocialNotificationService socialNotifications;
    private readonly EncryptionSetupLauncher encryptionSetup;
    private readonly SettingsLauncher settingsLauncher;
    private readonly MessagePopouts popouts;
    private readonly AppSkin ui = new(AppPalettes.Message);
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly ViewRouter<MessageRoute> router;
    private readonly RouterDraw<MessageRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private MessageTab activeTab = MessageTab.Chats;
    private CallView currentCall;
    private CallState lastCallState;
    private string filter = string.Empty;
    private bool recoveryNudgeDismissed;
    private string searchDraft = string.Empty;
    private readonly ActionSheet chatSheet = new();
    private readonly HashSet<string> selectedContacts = new();
    private string groupTitleDraft = string.Empty;
    private volatile string? composeResult;
    private volatile bool backToListPending;
    private volatile bool backToDetailPending;
    private volatile AepFailureBox? addMembersFailure;
    private string addError = string.Empty;
    private float copiedTimer;
    private volatile bool removePending;
    private string? forwardOpenPending;
    private readonly ThreadView threadView;
    private readonly Action refreshContacts;

    public MessageApp(DirectMessagesStore store, ContactBook contacts, CallHub calls, AethernetSession session,
        RemoteImageCache images, LodestoneService lodestone, DmLauncher launcher, PhotoLibrary library,
        HttpService http, Configuration configuration, ConfirmService confirm, TranslationService translation,
        ReportService report,
        WallpaperImageCache wallpaperImages, MusterStore musters, MusterLauncher musterLauncher,
        SocialNotificationService socialNotifications, EncryptionSetupLauncher encryptionSetup,
        EncryptionHelpService encryptionHelp, SettingsLauncher settingsLauncher, MessagePopouts popouts)
    {
        this.popouts = popouts;
        this.translation = translation;
        this.socialNotifications = socialNotifications;
        this.musterLauncher = musterLauncher;
        this.encryptionSetup = encryptionSetup;
        this.settingsLauncher = settingsLauncher;
        this.store = store;
        this.contacts = contacts;
        this.calls = calls;
        this.session = session;
        this.images = images;
        this.lodestone = lodestone;
        this.launcher = launcher;
        this.library = library;
        this.http = http;
        this.configuration = configuration;
        this.confirm = confirm;
        this.report = report;
        this.wallpaperImages = wallpaperImages;
        this.encryptionHelp = encryptionHelp;
        this.musters = musters;
        router = new ViewRouter<MessageRoute>(MessageRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
        chrome = new ChatListChrome(ui, ink);
        pickers = new ChatAppearancePickers(chrome, wallpaperImages, library);
        pickTheme = SetTheme;
        pickWallpaper = id => SetWallpaper(wallpaperScope, id);
        setWallpaperPattern = SetWallpaperPattern;
        clearWallpaperOverride = ClearWallpaperOverride;
        refreshContacts = () => contacts.Refresh(force: true);
        groupPhotoPicker = new ImagePickCrop(library, wallpaperImages);
        threadView = new ThreadView(this);
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = MessageTab.Chats;
        filter = string.Empty;
        ResetChatSearch();
        searchDraft = string.Empty;
        addError = string.Empty;
        avatarLightbox.Reset();
        selectedContacts.Clear();
        groupTitleDraft = string.Empty;
        RefreshAndConsumeLaunch(forceContacts: true);
    }

    public void OnResumed()
    {
        RefreshAndConsumeLaunch(forceContacts: false);
    }

    private void RefreshAndConsumeLaunch(bool forceContacts)
    {
        contacts.Refresh(force: forceContacts);
        store.RefreshConversations();
        ConsumeLaunchRequests();
    }

    private void ConsumeLaunchRequests()
    {
        if (launcher.TryConsumeCalls())
        {
            activeTab = MessageTab.Calls;
            return;
        }

        if (launcher.TryConsumeConversation(out var conversationId))
        {
            ShowLaunchedThread(conversationId);
            return;
        }

        if (!launcher.TryConsumeUser(out var userId))
        {
            return;
        }

        store.CreateDirect(userId, id =>
        {
            if (!string.IsNullOrEmpty(id))
            {
                composeResult = id;
            }
        });
    }

    private void ShowLaunchedThread(string conversationId)
    {
        activeTab = MessageTab.Chats;
        var target = MessageRoute.Thread(conversationId);
        if (router.Current == target)
        {
            return;
        }

        router.Reset();
        router.Push(target, false);
    }

    public void OnClosed()
    {
        FlushNotes();
        threadView.OnAppClosed();
    }

    public void Draw(in PhoneContext context)
    {
        store.NoteInboxWatched();
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        ResolveTheme();
        var delta = ImGui.GetIO().DeltaTime;
        if (copiedTimer > 0f)
        {
            copiedTimer = MathF.Max(0f, copiedTimer - delta);
        }

        currentCall = calls.Snapshot();
        contacts.Refresh();
        ConsumeLaunchRequests();
        SyncCallRoute();
        ConsumeSharedPhoto();
        ProcessPending();
        threadView.GateMenus();
        chatSheet.Gate();
        threadSheet.Gate();
        memberSheet.Gate();
        groupPhotoSheet.Gate();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        screenRect = screen;
        chrome.ScreenRect = screen;
        ui.Backdrop(screen);
        using (InputShield.Engage(avatarLightbox.Expanded))
        {
            router.Draw(SceneChrome.AppAreaFrom(context.Content, theme, UiScale.Current), AppSkin.Transparent,
                delta, drawView);
        }

        if (avatarLightbox.Active)
        {
            avatarLightbox.Draw(screen, theme);
        }

        DrawChatSheet(screen);
        DrawThreadSheet(screen);
        DrawMemberSheet(screen);
        DrawGroupPhotoSheet(screen);
    }

    private void SyncCallRoute()
    {
        var state = currentCall.State;
        var inCall = state is CallState.Dialing or CallState.Connecting or CallState.Active;
        var requested = calls.ConsumeCallScreenRequest();
        if (!inCall)
        {
            if (router.Current.Screen is MessageScreen.Call or MessageScreen.AddToCall)
            {
                router.Pop(false);
            }
        }
        else if ((requested || lastCallState is CallState.Idle or CallState.Ringing or CallState.Ended)
                 && router.Current.Screen is not MessageScreen.Call and not MessageScreen.AddToCall)
        {
            router.Push(MessageRoute.Call, false);
        }

        lastCallState = state;
    }

    private void ProcessPending()
    {
        if (backToListPending)
        {
            backToListPending = false;
            router.Reset();
            store.RefreshConversations();
        }

        if (backToDetailPending)
        {
            backToDetailPending = false;
            selectedContacts.Clear();
            if (router.Current.Screen == MessageScreen.AddMembers)
            {
                router.Pop();
            }

            store.RefreshThreadDetail();
        }

        if (removePending)
        {
            removePending = false;
            if (router.Current.Screen == MessageScreen.ContactDetail)
            {
                router.Pop();
            }
        }

        if (forwardOpenPending is { } forwardTarget)
        {
            forwardOpenPending = null;
            activeTab = MessageTab.Chats;
            router.Reset();
            router.Push(MessageRoute.Thread(forwardTarget));
        }

        ProcessAddOutcomes();
        ProcessGroupOutcomes();
        var result = composeResult;
        if (result is null)
        {
            return;
        }

        composeResult = null;
        selectedContacts.Clear();
        groupTitleDraft = string.Empty;
        activeTab = MessageTab.Chats;
        while (router.Current.Screen is MessageScreen.NewChat or MessageScreen.NewGroup or MessageScreen.ContactDetail)
        {
            router.Pop(false);
        }

        router.Push(MessageRoute.Thread(result));
    }

    private void DrawView(MessageRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case MessageScreen.Thread:
                threadView.Draw(area, route.Id ?? string.Empty, depth == router.Depth);
                break;
            case MessageScreen.NewChat:
                DrawNewChat(area);
                break;
            case MessageScreen.NewGroup:
                DrawNewGroup(area);
                break;
            case MessageScreen.GroupInfo:
                DrawGroupInfo(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.EditGroup:
                DrawEditGroup(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.GroupPhoto:
                DrawGroupPhoto(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.AddMembers:
                DrawAddMembers(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.ChatImage:
                threadView.DrawImagePicker(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.ImageView:
                threadView.DrawImageViewer(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.Archived:
                DrawArchived(area);
                break;
            case MessageScreen.ContactDetail:
                DrawContactDetail(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.AddContact:
                DrawAddContact(area);
                break;
            case MessageScreen.Safety:
                DrawSafety(area);
                break;
            case MessageScreen.Call:
                DrawCallRoute(area);
                break;
            case MessageScreen.AddToCall:
                DrawAddToCall(area);
                break;
            case MessageScreen.NewCall:
                DrawNewCall(area);
                break;
            case MessageScreen.Encryption:
                DrawEncryptionInfo(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.MessageInfo:
                DrawMessageInfo(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.Forward:
                DrawForwardPicker(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.SharePhoto:
                DrawSharePhotoPicker(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.Starred:
                DrawStarred(area, route.Id);
                break;
            case MessageScreen.Reactions:
                threadView.DrawReactions(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.ChatTheme:
                DrawChatTheme(area);
                break;
            case MessageScreen.Wallpaper:
                DrawWallpaper(area, route.Id ?? string.Empty);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        if (session.IsSignedIn)
        {
            TourHolds.Release(Id);
        }
        else
        {
            TourHolds.Hold(Id);
        }

        var scale = UiScale.Current;
        var headerRect = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        var contentTop = headerRect.Max.Y;
        DrawRootHeader(headerRect);
        if (currentCall.State is CallState.Dialing or CallState.Connecting or CallState.Active)
        {
            contentTop = DrawReturnToCallBanner(new Rect(new Vector2(area.Min.X, contentTop),
                new Vector2(area.Max.X, contentTop + ReturnBannerHeight * scale)));
        }

        var content = new Rect(new Vector2(area.Min.X, contentTop), area.Max);
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case MessageTab.Calls:
                    DrawCallsTab(content);
                    break;
                case MessageTab.Contacts:
                    DrawContactsTab(content);
                    break;
                case MessageTab.Settings:
                    DrawSettingsTab(content);
                    break;
                default:
                    DrawChatsTab(content);
                    break;
            }
        }

        DrawTabBar(area);
    }

    private void DrawRootHeader(Rect area)
    {
        var drawList = ImGui.GetWindowDrawList();
        switch (activeTab)
        {
            case MessageTab.Calls:
                DrawTabHeader(area, Loc.T(L.Phone.Calls), 1);
                if (session.IsSignedIn && calls.Enabled
                    && DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.PhonePlus,
                        Loc.T(L.Phone.NewCall)))
                {
                    searchDraft = string.Empty;
                    router.Push(MessageRoute.NewCall);
                }

                break;
            case MessageTab.Contacts:
                DrawTabHeader(area, Loc.T(L.Apps.Contacts), 1);
                if (session.IsSignedIn
                    && DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.UserPlus,
                        Loc.T(L.Message.NewContact)))
                {
                    addError = string.Empty;
                    router.Push(MessageRoute.AddContact);
                }

                break;
            case MessageTab.Settings:
                DrawTabHeader(area, Loc.T(L.Settings.Title), 0);
                break;
            default:
                DrawTabHeader(area, Loc.T(L.Message.TabChats), 2);
                if (!session.IsSignedIn)
                {
                    break;
                }

                var newChatCenter = SocialChrome.HeaderSlot(area, 0);
                UiAnchors.Report("message.newchat", ChatListChrome.HeaderHit(newChatCenter));
                if (DrawHeaderIcon(drawList, newChatCenter, PhoneIcons.MessagePlus, Loc.T(L.Message.NewChat)))
                {
                    OpenNewChat();
                }

                if (DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1), PhoneIcons.Search,
                        Loc.T(L.Common.Search), chatSearchOpen))
                {
                    ToggleChatSearch();
                }

                break;
        }
    }

    private void OpenNewChat()
    {
        selectedContacts.Clear();
        groupTitleDraft = string.Empty;
        filter = string.Empty;
        router.Push(MessageRoute.NewChat);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)MessageTab.Chats] = new TabItem(Loc.T(L.Message.TabChats), PhoneIcons.MessageCircle,
            PhoneIcons.MessageCircleFilled, store.UnreadTotal, "message.tab.chats");
        tabItems[(int)MessageTab.Calls] = new TabItem(Loc.T(L.Phone.Calls), PhoneIcons.Phone, PhoneIcons.PhoneFilled,
            calls.UnseenMissed, "message.tab.calls");
        tabItems[(int)MessageTab.Contacts] = new TabItem(Loc.T(L.Apps.Contacts), PhoneIcons.Users,
            AnchorKey: "message.tab.contacts");
        tabItems[(int)MessageTab.Settings] = new TabItem(Loc.T(L.Settings.Title), PhoneIcons.Settings,
            AnchorKey: "message.tab.settings");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((MessageTab)result.Tapped);
    }

    private void SelectTab(MessageTab tab)
    {
        activeTab = tab;
        chatSheet.Close();
        filter = string.Empty;
        CloseChatSearch();
    }

    public void Dispose()
    {
        threadView.Dispose();
        store.Dispose();
    }
}
