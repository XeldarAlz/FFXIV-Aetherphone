using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp : IResumableApp
{
    private enum MessagesTab : byte
    {
        Chats,
        People,
        Settings,
    }

    public const string DefaultThemeId = ChatThemes.LinkpearlDefaultId;

    public string Id => "messages";
    public string DisplayName => Loc.T(L.Apps.Linkpearl);
    public string Glyph => "Lp";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => inbox.TotalUnread;
    public bool HasBadge => true;
    public bool WantsSystemTheme => false;
    private readonly ChatInbox inbox;
    private readonly TabStore tabs;
    private readonly ChatArchive archive;
    private readonly ChatLog chatLog;
    private readonly LinkpearlNotificationGate notificationGate;
    private readonly LinkpearlLauncher launcher;
    private readonly LodestoneService lodestone;
    private readonly MarketLauncher marketLauncher;
    private readonly NotificationService notifications;
    private readonly GameData gameData;
    private readonly LookupService lookup;
    private readonly ConfirmService confirm;
    private readonly Configuration configuration;
    private readonly LinkpearlPopouts popouts;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly ViewRouter<LinkpearlRoute> router;
    private readonly RouterDraw<LinkpearlRoute> drawView;
    private readonly Action backToList;
    private readonly Action backToSettings;
    private readonly Action leaveTabEditor;
    private readonly GameChatThread chatThread;
    private readonly GameChatMenu chatMenu = new("linkpearl.chat.menu");
    private readonly AppSkin ui = new(ChatThemes.PaletteFor(DefaultThemeId));
    private readonly ChatListChrome chrome;
    private readonly ChatAppearancePickers pickers;
    private readonly BottomTabBar tabBar = new();
    private readonly NavTab[] navTabs = new NavTab[3];
    private SocialInk ink = ChatThemes.InkFor(DefaultThemeId);
    private ChatTheme activeTheme = ChatThemes.Resolve(DefaultThemeId);
    private Rect screenRect;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private MessagesTab activeTab;
    private string chatSearchQuery = string.Empty;
    private readonly ChatSearch search = new();
    private readonly ActionSheet conversationSheet = new();
    private ChatFilter chatFilter;
    private readonly SheetSurface newChatSheet = new("linkpearl.newChat");
    private readonly Action<Rect> drawNewChatSheet;
    private readonly DropdownMenu settingsMenu = new();
    private readonly DropdownMenu editorMenu = new();
    private string threadKey = string.Empty;

    public LinkpearlApp(ChatInbox inbox, TabStore tabs, ChatArchive archive,
        LinkpearlNotificationGate notificationGate,
        LinkpearlLauncher launcher, LodestoneService lodestone, MarketLauncher marketLauncher,
        NotificationService notifications, GameData gameData,
        LookupService lookup, ConfirmService confirm, ChatLog chatLog, ChatSend chatSend,
        Configuration configuration, LinkpearlPopouts popouts, WallpaperImageCache wallpaperImages,
        PhotoLibrary library)
    {
        this.inbox = inbox;
        this.tabs = tabs;
        this.archive = archive;
        this.chatLog = chatLog;
        this.notificationGate = notificationGate;
        this.launcher = launcher;
        this.lodestone = lodestone;
        this.marketLauncher = marketLauncher;
        this.notifications = notifications;
        this.gameData = gameData;
        this.lookup = lookup;
        this.confirm = confirm;
        this.configuration = configuration;
        this.popouts = popouts;
        this.wallpaperImages = wallpaperImages;
        router = new ViewRouter<LinkpearlRoute>(LinkpearlRoute.Root);
        chrome = new ChatListChrome(ui, ink);
        pickers = new ChatAppearancePickers(chrome, wallpaperImages, library);
        pickTheme = SetTheme;
        pickWallpaper = id => SetWallpaper(wallpaperScope, id);
        setWallpaperPattern = SetWallpaperPattern;
        clearWallpaperOverride = ClearWallpaperOverride;
        chatMenu.SendTell = (name, world) => OpenDirectThread(name, SendTargetFor(name, world));
        chatMenu.LookUp = (name, world) => router.Push(LinkpearlRoute.Character(string.Empty, name, world));
        chatMenu.OpenMarket = itemId =>
        {
            marketLauncher.RequestItem(itemId);
            frameNavigation.Open("market");
        };
        chatThread = new GameChatThread(chatLog, chatSend, gameData)
        {
            Context = chatMenu.Open,
            Link = chatMenu.OpenLink,
        };
        drawView = DrawView;
        drawNewChatSheet = DrawNewChatSheet;
        paintThreadBackdrop = PaintThreadBackdrop;
        backToList = () =>
        {
            chatMenu.Close();
            inbox.Viewing = string.Empty;
            threadKey = string.Empty;
            router.Pop();
        };
        backToSettings = () =>
        {
            settingsMenu.Close();
            router.Pop();
        };
        leaveTabEditor = LeaveTabEditor;
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = MessagesTab.Chats;
        threadKey = string.Empty;
        chatFilter = ChatFilter.All;
        ResetChatSearch();
        inbox.Viewing = string.Empty;
        inbox.Invalidate();
        inbox.Sync();
        ResetPeopleState();
        ReadFriends();
        ConsumeLaunchRequests();
    }

    public void OnResumed()
    {
        inbox.Invalidate();
        inbox.Sync();
        if (threadKey.Length > 0)
        {
            inbox.Viewing = threadKey;
        }

        ReadFriends();
        ConsumeLaunchRequests();
    }

    private void ConsumeLaunchRequests()
    {
        if (launcher.TryConsume(out var conversationKey))
        {
            inbox.Sync();
            if (inbox.Find(conversationKey) is null)
            {
                return;
            }

            if (router.Current.Screen != LinkpearlScreen.Root)
            {
                router.Reset();
            }

            activeTab = MessagesTab.Chats;
            OpenConversation(conversationKey);
            return;
        }

        if (!launcher.TryConsumeLookup(out var lookupName, out var lookupWorld))
        {
            return;
        }

        if (router.Current.Screen != LinkpearlScreen.Root)
        {
            router.Reset();
        }

        activeTab = MessagesTab.People;
        router.Push(LinkpearlRoute.Character(string.Empty, lookupName, lookupWorld));
    }

    public void OnClosed()
    {
        chatMenu.Close();
        conversationSheet.Close();
        editorMenu.Close();
        settingsMenu.Close();
        newChatSheet.Close();
        chatThread.Close();
        inbox.Viewing = string.Empty;
        inbox.ClearTransient();
        inbox.FlushSeen();
    }

    public void Draw(in PhoneContext context)
    {
        var delta = ImGui.GetIO().DeltaTime;
        TickContacts(delta);
        ResolveTheme(context.Theme);
        frameNavigation = context.Navigation;
        chatMenu.Gate();
        conversationSheet.Gate();
        editorMenu.Gate();
        settingsMenu.Gate();
        chatThread.Gate();
        ConsumeLaunchRequests();
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, frameTheme, scale);
        screenRect = screen;
        chrome.ScreenRect = screen;
        ui.Backdrop(screen);
        router.Draw(SceneChrome.AppAreaFrom(context.Content, frameTheme, scale), AppSkin.Transparent, delta, drawView);
        DrawConversationSheet(screen);
    }

    private void ResolveTheme(PhoneTheme phoneTheme)
    {
        var id = configuration.LinkpearlChatTheme.Length > 0 ? configuration.LinkpearlChatTheme : DefaultThemeId;
        activeTheme = ChatThemes.Resolve(id);
        ink = ChatThemes.InkFor(id);
        ui.Palette = ChatThemes.PaletteFor(id);
        frameTheme = PhoneTheme.WithAccent(phoneTheme, activeTheme.Accent);
        ui.Theme = frameTheme;
        chrome.Ink = ink;
        chrome.Theme = frameTheme;
    }

    private void DrawView(LinkpearlRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case LinkpearlScreen.Conversation:
                DrawConversation(area, route.ConversationKey);
                break;
            case LinkpearlScreen.TabEditor:
                DrawTabEditor(area, route.ConversationKey);
                break;
            case LinkpearlScreen.TabInfo:
                DrawTabInfo(area, route.ConversationKey);
                break;
            case LinkpearlScreen.SettingsSection:
                DrawSettingsSection(area, route.Section);
                break;
            case LinkpearlScreen.ChatTheme:
                DrawChatThemeScreen(area);
                break;
            case LinkpearlScreen.Wallpaper:
                DrawWallpaperScreen(area, route.ConversationKey);
                break;
            case LinkpearlScreen.FriendDetail when route.Friend is { } friend:
                DrawFriendDetail(area, friend);
                break;
            case LinkpearlScreen.CharacterDetail:
                DrawCharacterDetail(area, route);
                break;
            case LinkpearlScreen.FreeCompanyDetail:
                DrawFreeCompanyDetail(area, route);
                break;
            default:
                inbox.Viewing = string.Empty;
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        if (GuideIntents.Consume("messages.tab.people"))
        {
            SelectTab(MessagesTab.People);
        }

        var scale = UiScale.Current;
        var header = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomTabBar.Height * scale), area.Max);
        var content = new Rect(new Vector2(area.Min.X, header.Max.Y), new Vector2(area.Max.X, navRect.Min.Y));
        using (InputShield.Engage(newChatSheet.CapturesPointer))
        {
            DrawRootHeader(header);
            switch (activeTab)
            {
                case MessagesTab.People:
                    DrawPeopleTab(content);
                    break;
                case MessagesTab.Settings:
                    DrawSettingsTab(content);
                    break;
                default:
                    DrawChatsTab(content);
                    break;
            }

            DrawBottomNav(navRect);
        }

        newChatSheet.Draw(area, NewChatSkin(), Loc.T(L.Linkpearl.NewChat), NewChatSheetFraction(area),
            drawNewChatSheet);
    }

    private void DrawRootHeader(Rect header)
    {
        var drawList = ImGui.GetWindowDrawList();
        switch (activeTab)
        {
            case MessagesTab.People:
                chrome.DrawTabHeader(header, Loc.T(L.Linkpearl.People), backToList, 2);
                var refreshCenter = SocialChrome.HeaderSlot(header, 0);
                UiAnchors.Report("contacts.refresh", HeaderHit(refreshCenter));
                if (chrome.DrawHeaderIcon(drawList, refreshCenter, PhoneIcons.Refresh, Loc.T(L.Common.Refresh)))
                {
                    RequestRefresh();
                }

                if (chrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(header, 1), PhoneIcons.Search,
                        Loc.T(L.Common.Search), peopleSearchOpen))
                {
                    TogglePeopleSearch();
                }

                break;
            case MessagesTab.Settings:
                chrome.DrawTabHeader(header, Loc.T(L.Settings.Title), backToList, 0);
                break;
            default:
                DrawChatsHeader(header, drawList);
                break;
        }
    }

    private void DrawChatsHeader(Rect header, ImDrawListPtr drawList)
    {
        var unread = inbox.TotalUnread > 0;
        chrome.DrawTabHeader(header, DisplayName, backToList, unread ? 3 : 2);
        var newChatCenter = SocialChrome.HeaderSlot(header, 0);
        UiAnchors.Report("messages.new", HeaderHit(newChatCenter));
        if (chrome.DrawHeaderIcon(drawList, newChatCenter, PhoneIcons.MessagePlus, Loc.T(L.Linkpearl.NewChat)))
        {
            OpenNewChat();
        }

        if (chrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(header, 1), PhoneIcons.Search,
                Loc.T(L.Common.Search), chatSearchOpen))
        {
            ToggleChatSearch();
        }

        if (unread && chrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(header, 2), PhoneIcons.Checks,
                Loc.T(L.Linkpearl.MarkAllRead)))
        {
            MarkAllRead();
        }
    }

    private static Rect HeaderHit(Vector2 center)
    {
        var radius = SocialChrome.HeaderIconRadius * UiScale.Current;
        var half = new Vector2(radius, radius);
        return new Rect(center - half, center + half);
    }

    private void MarkAllRead()
    {
        inbox.MarkAllRead();
        inbox.FlushSeen();
        notifications.RemoveApp(Id);
    }

    private void DrawBottomNav(Rect nav)
    {
        navTabs[0] = new NavTab(FontAwesomeIcon.Comments, Loc.T(L.Messages.TabChats), BadgeCount,
            AnchorKey: "messages.tab.chats", Glyph: PhoneIcons.MessageCircle,
            ActiveGlyph: PhoneIcons.MessageCircleFilled);
        navTabs[1] = new NavTab(FontAwesomeIcon.UserFriends, Loc.T(L.Linkpearl.People),
            AnchorKey: "messages.tab.people", Glyph: PhoneIcons.Users);
        navTabs[2] = new NavTab(FontAwesomeIcon.Cog, Loc.T(L.Settings.Title),
            AnchorKey: "messages.tab.settings", Glyph: PhoneIcons.Settings);
        var tapped = tabBar.Draw(nav, ui, frameTheme, navTabs, (int)activeTab, activeInk: ink.AccentLink);
        if (tapped >= 0)
        {
            SelectTab((MessagesTab)tapped);
        }
    }

    private void SelectTab(MessagesTab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        if (tab == MessagesTab.People)
        {
            RequestRefresh();
        }
    }

    private static string SendTargetFor(string name, string world) =>
        world.Length > 0 ? string.Concat(name, "@", world) : name;

    private static Rect CenteredActionRow(Rect header, float scale)
    {
        var offset = (header.Height - AppHeader.Height * scale) * 0.5f;
        return new Rect(new Vector2(header.Min.X, header.Min.Y + offset), header.Max);
    }

    public void Dispose() => chatThread.Dispose();
}
