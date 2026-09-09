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
using Aetherphone.Core.Theme;
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
    }

    private const float RootHeaderHeight = 52f;
    private const byte MoreMarkAllRead = 0;
    private const byte MorePause = 1;
    private const byte MoreSettings = 2;

    public string Id => "messages";
    public string DisplayName => Loc.T(L.Apps.Linkpearl);
    public string Glyph => "Lp";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => inbox.TotalUnread;
    public bool HasBadge => true;
    public bool WantsSystemTheme => true;
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
    private readonly ViewRouter<LinkpearlRoute> router;
    private readonly RouterDraw<LinkpearlRoute> drawView;
    private readonly Action backToList;
    private readonly Action backToSettings;
    private readonly Action leaveTabEditor;
    private readonly GameChatThread chatThread;
    private readonly GameChatMenu chatMenu = new("linkpearl.chat.menu");
    private readonly AppSkin ui = new(AppPalettes.Linkpearl(PhoneTheme.Default));
    private readonly BottomTabBar tabBar = new();
    private readonly NavTab[] navTabs = new NavTab[2];
    private readonly DropdownMenu moreMenu = new();
    private readonly DropdownMenu.Item[] moreItems = new DropdownMenu.Item[3];
    private readonly byte[] moreActions = new byte[3];
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
        Configuration configuration, LinkpearlPopouts popouts)
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
        router = new ViewRouter<LinkpearlRoute>(LinkpearlRoute.Root);
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
        chatSearchQuery = string.Empty;
        chatFilter = ChatFilter.All;
        search.Clear();
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
        moreMenu.Close();
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
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        ui.Palette = AppPalettes.Linkpearl(frameTheme);
        ui.Theme = frameTheme;
        chatMenu.Gate();
        conversationSheet.Gate();
        moreMenu.Gate();
        editorMenu.Gate();
        settingsMenu.Gate();
        chatThread.Gate();
        ConsumeLaunchRequests();
        router.Draw(context.Content, context.Theme.AppBackground, delta, drawView);
        DrawConversationSheet(context.Content);
    }

    private void DrawView(LinkpearlRoute route, Rect area, int depth)
    {
        switch (route.Screen)
        {
            case LinkpearlScreen.Conversation:
                DrawConversation(area, route.ConversationKey);
                break;
            case LinkpearlScreen.TabEditor:
                DrawTabEditor(area, route.ConversationKey);
                break;
            case LinkpearlScreen.Settings:
                DrawSettings(area);
                break;
            case LinkpearlScreen.SettingsSection:
                DrawSettingsSection(area, route.Section);
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
        var header = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + RootHeaderHeight * scale));
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomTabBar.Height * scale), area.Max);
        var content = new Rect(new Vector2(area.Min.X, header.Max.Y), new Vector2(area.Max.X, navRect.Min.Y));
        using (InputShield.Engage(newChatSheet.CapturesPointer))
        {
            DrawRootHeader(header, scale);
            if (activeTab == MessagesTab.People)
            {
                DrawPeopleTab(content);
            }
            else
            {
                DrawChatsTab(content);
            }

            DrawBottomNav(navRect);
        }

        DrawMoreMenu(area);
        newChatSheet.Draw(area, frameTheme, Loc.T(L.Linkpearl.NewChat), NewChatSheetFraction(area), drawNewChatSheet);
    }

    private void DrawRootHeader(Rect header, float scale)
    {
        var title = activeTab == MessagesTab.People ? Loc.T(L.Linkpearl.People) : DisplayName;
        var slotCount = activeTab == MessagesTab.People ? 1 : 2;
        var actions = new HeaderActions(CenteredActionRow(header, scale), scale, slotCount);
        HeaderTitle.Draw("linkpearl.header.title", title, header.Min.X + Metrics.Space.Lg * scale, actions,
            frameTheme.TextStrong, scale);
        if (activeTab == MessagesTab.People)
        {
            UiAnchors.Report("contacts.refresh", actions.Bounds(0));
            if (ui.IconButton(actions.Slot(0), actions.Radius, IconGlyph.Of(FontAwesomeIcon.Sync),
                    frameTheme.TextStrong, AppSkin.Transparent, HeaderActions.GlyphScale, Loc.T(L.Common.Refresh),
                    HoverLabelSide.Below))
            {
                RequestRefresh();
            }

            return;
        }

        UiAnchors.Report("messages.new", actions.Bounds(0));
        if (ui.IconButton(actions.Slot(0), actions.Radius, IconGlyph.Of(FontAwesomeIcon.Plus), frameTheme.Accent,
                Palette.WithAlpha(frameTheme.Accent, 0.16f), HeaderActions.GlyphScale, Loc.T(L.Linkpearl.NewChat),
                HoverLabelSide.Below))
        {
            OpenNewChat();
        }

        if (ui.IconButton(actions.Slot(1), actions.Radius, IconGlyph.Of(FontAwesomeIcon.Cog),
                frameTheme.TextStrong, AppSkin.Transparent, HeaderActions.GlyphScale, Loc.T(L.Linkpearl.More),
                HoverLabelSide.Below))
        {
            OpenMoreMenu(actions.Bounds(1));
        }

        if (notificationGate.Paused)
        {
            ImGui.GetWindowDrawList().AddCircleFilled(actions.Slot(1) + new Vector2(10f * scale, -10f * scale),
                3.5f * scale, ImGui.GetColorU32(frameTheme.Accent), 12);
        }
    }

    private static Rect CenteredActionRow(Rect header, float scale)
    {
        var offset = (header.Height - AppHeader.Height * scale) * 0.5f;
        return new Rect(new Vector2(header.Min.X, header.Min.Y + offset), header.Max);
    }

    private void OpenMoreMenu(Rect anchor)
    {
        moreMenu.Header = string.Empty;
        moreMenu.Toggle("linkpearl.more", anchor);
    }

    private void DrawMoreMenu(Rect area)
    {
        if (!moreMenu.IsOpenFor("linkpearl.more"))
        {
            return;
        }

        var paused = notificationGate.Paused;
        moreItems[0] = new DropdownMenu.Item(Loc.T(L.Linkpearl.MarkAllRead), IconGlyph.Of(FontAwesomeIcon.CheckDouble));
        moreActions[0] = MoreMarkAllRead;
        moreItems[1] = new DropdownMenu.Item(Loc.T(paused ? L.Messages.ResumeNotifications : L.Messages.PauseNotifications),
            IconGlyph.Of((paused ? FontAwesomeIcon.Bell : FontAwesomeIcon.BellSlash)));
        moreActions[1] = MorePause;
        moreItems[2] = new DropdownMenu.Item(Loc.T(L.Linkpearl.ChatSettings), IconGlyph.Of(FontAwesomeIcon.Cog));
        moreActions[2] = MoreSettings;
        var clicked = moreMenu.Draw(area, frameTheme, moreItems);
        if (clicked < 0)
        {
            return;
        }

        switch (moreActions[clicked])
        {
            case MoreMarkAllRead:
                inbox.MarkAllRead();
                inbox.FlushSeen();
                notifications.RemoveApp(Id);
                break;
            case MorePause:
                notificationGate.Toggle();
                break;
            case MoreSettings:
                router.Push(LinkpearlRoute.Settings);
                break;
        }
    }

    private void DrawBottomNav(Rect nav)
    {
        navTabs[0] = new NavTab(FontAwesomeIcon.Comments, Loc.T(L.Messages.TabChats), BadgeCount,
            AnchorKey: "messages.tab.chats");
        navTabs[1] = new NavTab(FontAwesomeIcon.UserFriends, Loc.T(L.Linkpearl.People),
            AnchorKey: "messages.tab.people");
        var tapped = tabBar.Draw(nav, ui, frameTheme, navTabs, (int)activeTab);
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

    public void Dispose() => chatThread.Dispose();
}
