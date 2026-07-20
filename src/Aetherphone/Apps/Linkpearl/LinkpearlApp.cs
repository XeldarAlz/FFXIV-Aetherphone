using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp : IPhoneApp
{
    private enum MessagesTab : byte
    {
        Chats,
        Contacts,
        Find,
    }

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public string Id => "messages";
    public string DisplayName => Loc.T(L.Apps.Linkpearl);
    public string Glyph => "Lp";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => store.TotalUnread() + linkshells.TotalUnread();
    private readonly MessageStore store;
    private readonly LinkshellStore linkshells;
    private readonly LinkshellMuteStore mutes;
    private readonly LinkpearlNotificationGate notificationGate;
    private readonly ChatBridge bridge;
    private readonly LinkshellBridge linkshellBridge;
    private readonly LinkpearlLauncher launcher;
    private readonly LodestoneService lodestone;
    private readonly NotificationService notifications;
    private readonly GameData gameData;
    private readonly LookupService lookup;
    private readonly ConfirmService confirm;
    private readonly ViewRouter<LinkpearlRoute> router;
    private readonly RouterDraw<LinkpearlRoute> drawView;
    private readonly Action backToList;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private MessagesTab activeTab;

    public LinkpearlApp(MessageStore store, LinkshellStore linkshells, LinkshellMuteStore mutes,
        LinkpearlNotificationGate notificationGate, ChatBridge bridge,
        LinkshellBridge linkshellBridge, LinkpearlLauncher launcher, LodestoneService lodestone,
        NotificationService notifications, GameData gameData, LookupService lookup, ConfirmService confirm)
    {
        this.store = store;
        this.linkshells = linkshells;
        this.mutes = mutes;
        this.notificationGate = notificationGate;
        this.bridge = bridge;
        this.linkshellBridge = linkshellBridge;
        this.launcher = launcher;
        this.lodestone = lodestone;
        this.notifications = notifications;
        this.gameData = gameData;
        this.lookup = lookup;
        this.confirm = confirm;
        router = new ViewRouter<LinkpearlRoute>(LinkpearlRoute.Root, Id);
        drawView = DrawView;
        backToList = () =>
        {
            chatMenu.Close();
            router.Pop();
        };
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = MessagesTab.Chats;
        trackedThread = null;
        ResetContactsState();
        ResetFindState();
        ReadFriends();
        if (launcher.TryConsumeLinkshell(out var channel, out var linkshellName))
        {
            chatSegment = 1;
            var existing = linkshells.Find(channel);
            var name = existing?.Name is { Length: > 0 } current ? current : linkshellName;
            var thread = linkshells.GetOrCreate(channel, name);
            thread.MarkRead();
            router.Push(LinkpearlRoute.Shell(thread), false);
            return;
        }

        if (launcher.TryConsume(out var display, out var sendTarget))
        {
            chatSegment = 0;
            var conversation = store.GetOrCreate(display, sendTarget);
            conversation.MarkRead();
            router.Push(LinkpearlRoute.Direct(conversation), false);
        }
    }

    public void OnClosed()
    {
        chatMenu.Close();
        router.Reset();
        draft = string.Empty;
        trackedThread = null;
        ResetContactsState();
        ResetFindState();
    }

    public void Draw(in PhoneContext context)
    {
        var delta = ImGui.GetIO().DeltaTime;
        TickContacts(delta);
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        chatMenu.Gate();
        router.Draw(context.Content, context.Theme.AppBackground, delta, drawView);
    }

    private void DrawView(LinkpearlRoute route, Rect area, int depth)
    {
        switch (route.Screen)
        {
            case LinkpearlScreen.DirectThread when route.Conversation is { } conversation:
                if (!store.Contains(conversation))
                {
                    router.Reset();
                    break;
                }

                DrawDirectThread(area, conversation);
                break;
            case LinkpearlScreen.LinkshellThread when route.Linkshell is { } thread:
                if (!linkshells.Contains(thread))
                {
                    router.Reset();
                    break;
                }

                DrawLinkshellThread(area, thread);
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
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        if (GuideIntents.Consume("messages.tab.contacts"))
        {
            SelectTab(MessagesTab.Contacts);
        }

        if (GuideIntents.Consume("messages.tab.find"))
        {
            SelectTab(MessagesTab.Find);
        }

        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, HeaderTitle());
        if (activeTab == MessagesTab.Contacts && DrawRefreshButton(in context))
        {
            RequestRefresh();
        }

        if (activeTab == MessagesTab.Chats && DrawNotificationPauseButton(in context))
        {
            notificationGate.Toggle();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var navHeight = 60f * scale;
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - navHeight), area.Max);
        var content = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale),
            new Vector2(area.Max.X, navRect.Min.Y));
        switch (activeTab)
        {
            case MessagesTab.Contacts:
                DrawContactsTab(content);
                break;
            case MessagesTab.Find:
                DrawFindTab(content);
                break;
            default:
                DrawChatsTab(content);
                break;
        }

        DrawBottomNav(navRect);
    }

    private string HeaderTitle() => activeTab switch
    {
        MessagesTab.Contacts => Loc.T(L.Apps.Contacts),
        MessagesTab.Find => Loc.T(L.Apps.FindPeople),
        _ => DisplayName,
    };

    private void DrawBottomNav(Rect nav)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(nav.Min, new Vector2(nav.Max.X, nav.Min.Y),
            ImGui.GetColorU32(Palette.WithAlpha(frameTheme.TextMuted, 0.25f)), 1f);
        var width = nav.Width / 3f;
        var chatsRect = new Rect(nav.Min, new Vector2(nav.Min.X + width, nav.Max.Y));
        var contactsRect = new Rect(new Vector2(nav.Min.X + width, nav.Min.Y),
            new Vector2(nav.Min.X + width * 2f, nav.Max.Y));
        var findRect = new Rect(new Vector2(nav.Min.X + width * 2f, nav.Min.Y), nav.Max);
        UiAnchors.Report("messages.tab.chats", chatsRect);
        UiAnchors.Report("messages.tab.contacts", contactsRect);
        UiAnchors.Report("messages.tab.find", findRect);
        DrawNavItem(chatsRect, FontAwesomeIcon.Comments, Loc.T(L.Messages.TabChats), MessagesTab.Chats, BadgeCount);
        DrawNavItem(contactsRect, FontAwesomeIcon.AddressBook, Loc.T(L.Apps.Contacts), MessagesTab.Contacts, 0);
        DrawNavItem(findRect, FontAwesomeIcon.Search, Loc.T(L.Apps.FindPeople), MessagesTab.Find, 0);
    }

    private void DrawNavItem(Rect rect, FontAwesomeIcon icon, string label, MessagesTab tab, int badge)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var active = activeTab == tab;
        var color = active ? frameTheme.Accent : frameTheme.TextMuted;
        var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + 20f * scale);
        ProgressRing.CenterIcon(iconCenter, icon, color, 17f * scale);
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Min.Y + 42f * scale), label, color, 0.72f,
            active ? FontWeight.SemiBold : FontWeight.Regular);
        if (badge > 0)
        {
            var badgeCenter = new Vector2(iconCenter.X + 12f * scale, iconCenter.Y - 9f * scale);
            ImGui.GetWindowDrawList().AddCircleFilled(badgeCenter, 7f * scale,
                ImGui.GetColorU32(frameTheme.Danger), 16);
            Typography.DrawCentered(badgeCenter, badge > 9 ? "9+" : badge.ToString(Loc.Culture), White, 0.62f,
                FontWeight.SemiBold);
        }

        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            SelectTab(tab);
        }
    }

    private void SelectTab(MessagesTab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        if (tab == MessagesTab.Contacts)
        {
            RequestRefresh();
        }
    }

    public void Dispose()
    {
    }
}
