using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Net;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp : IPhoneApp
{
    private enum MessageTab : byte
    {
        Chats,
        Calls,
        Contacts,
    }

    private const float ThreadPollSeconds = 3f;
    private const float TypingSendSeconds = 2.5f;
    private const int MessageMax = 1000;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 CallGreen = new(0.20f, 0.78f, 0.35f, 1f);

    public string Id => "message";
    public string DisplayName => Loc.T(L.Apps.Message);
    public string Glyph => "Me";
    public int BadgeCount => store.UnreadTotal + calls.UnseenMissed;

    private readonly DirectMessagesStore store;
    private readonly ContactBook contacts;
    private readonly CallHub calls;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly DmLauncher launcher;
    private readonly PhotoLibrary library;
    private readonly HttpService http;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Message);
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly ViewRouter<MessageRoute> router;
    private readonly RouterDraw<MessageRoute> drawView;
    private readonly ChatTranscript transcript = new();
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private MessageTab activeTab = MessageTab.Chats;
    private CallView currentCall;
    private CallState lastCallState;
    private string filter = string.Empty;

    public MessageApp(DirectMessagesStore store, ContactBook contacts, CallHub calls, AethernetSession session,
        RemoteImageCache images, LodestoneService lodestone, DmLauncher launcher, PhotoLibrary library,
        HttpService http, Configuration configuration)
    {
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
        router = new ViewRouter<MessageRoute>(MessageRoute.Root, Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = MessageTab.Chats;
        filter = string.Empty;
        searchDraft = string.Empty;
        addError = string.Empty;
        contacts.Refresh(force: true);
        store.RefreshConversations();
        if (launcher.TryConsumeConversation(out var conversationId))
        {
            router.Push(MessageRoute.Thread(conversationId), false);
        }
        else if (launcher.TryConsumeUser(out var userId))
        {
            store.CreateDirect(userId, id =>
            {
                if (!string.IsNullOrEmpty(id))
                {
                    composeResult = id;
                }
            });
        }
    }

    public void OnClosed()
    {
        FlushNotes();
        if (!composer.IsEditing && store.ConversationId is { } openConversation)
        {
            SaveDraft(openConversation);
        }

        router.Reset();
        avatarLightbox.Reset();
        filter = string.Empty;
        searchDraft = string.Empty;
        selectedContacts.Clear();
        groupTitleDraft = string.Empty;
        composer.CancelVoice();
        voicePlayer.Stop();
        searchController.Close();
        composer.Clear();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var delta = ImGui.GetIO().DeltaTime;
        if (copiedTimer > 0f)
        {
            copiedTimer = MathF.Max(0f, copiedTimer - delta);
        }

        currentCall = calls.Snapshot();
        SyncCallRoute();
        ProcessPending();
        messageMenuController.Gate();
        chatMenu.Gate();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        using (InputShield.Engage(avatarLightbox.Expanded))
        {
            router.Draw(context.Content, AppSkin.Transparent, delta, drawView);
        }

        if (avatarLightbox.Active)
        {
            avatarLightbox.Draw(screen, theme);
        }
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

            store.RefreshDetail();
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
        var result = composeResult;
        if (result is null)
        {
            return;
        }

        composeResult = null;
        selectedContacts.Clear();
        groupTitleDraft = string.Empty;
        activeTab = MessageTab.Chats;
        if (router.Current == MessageRoute.NewChat || router.Current.Screen == MessageScreen.ContactDetail)
        {
            router.Pop();
        }

        router.Push(MessageRoute.Thread(result));
    }

    private void DrawView(MessageRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case MessageScreen.Thread:
                DrawThread(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.NewChat:
                DrawNewChat(area);
                break;
            case MessageScreen.GroupInfo:
                DrawGroupInfo(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.AddMembers:
                DrawAddMembers(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.ChatImage:
                DrawChatImagePicker(area, route.Id ?? string.Empty);
                break;
            case MessageScreen.ImageView:
                DrawImageViewer(area, route.Id ?? string.Empty);
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
            case MessageScreen.Starred:
                DrawStarred(area);
                break;
            case MessageScreen.Reactions:
                DrawReactions(area, route.Id ?? string.Empty);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        if (GuideIntents.Consume("message.tab.calls"))
        {
            SelectTab(MessageTab.Calls);
        }

        if (GuideIntents.Consume("message.tab.contacts"))
        {
            SelectTab(MessageTab.Contacts);
        }

        var scale = ImGuiHelpers.GlobalScale;
        var headerRect = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        var navHeight = 60f * scale;
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - navHeight), area.Max);
        var contentTop = headerRect.Max.Y;
        DrawRootHeader(headerRect);
        if (currentCall.State is CallState.Dialing or CallState.Connecting or CallState.Active)
        {
            contentTop = DrawReturnToCallBanner(new Rect(new Vector2(area.Min.X, contentTop),
                new Vector2(area.Max.X, contentTop + ReturnBannerHeight * scale)));
        }

        var content = new Rect(new Vector2(area.Min.X, contentTop), new Vector2(area.Max.X, navRect.Min.Y));
        switch (activeTab)
        {
            case MessageTab.Calls:
                DrawCallsTab(content);
                break;
            case MessageTab.Contacts:
                DrawContactsTab(content);
                break;
            default:
                DrawChatsTab(content);
                break;
        }

        DrawBottomNav(navRect);
        DrawChatMenu(area);
    }

    private void DrawRootHeader(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var title = activeTab switch
        {
            MessageTab.Calls => Loc.T(L.Phone.Calls),
            MessageTab.Contacts => Loc.T(L.Apps.Contacts),
            _ => Loc.T(L.Message.TabChats),
        };
        Typography.DrawCentered(new Vector2(area.Center.X, area.Center.Y), title, ui.TitleInk, 1.3f, FontWeight.Bold);
        if (activeTab == MessageTab.Chats && configuration.MessageArchivedChats.Count > 0)
        {
            var archiveCenter = new Vector2(area.Max.X - 24f * scale, area.Center.Y);
            if (ui.IconButton(archiveCenter, 16f * scale, FontAwesomeIcon.BoxOpen.ToIconString(), ui.BodyInk,
                    Transparent, 1.1f, Loc.T(L.Message.Archived), HoverLabelSide.Below))
            {
                router.Push(MessageRoute.Archived);
            }
        }

        if (activeTab == MessageTab.Chats && configuration.MessageStarredMessages.Count > 0)
        {
            var starCenter = new Vector2(area.Min.X + 24f * scale, area.Center.Y);
            if (ui.IconButton(starCenter, 16f * scale, FontAwesomeIcon.Star.ToIconString(), ui.BodyInk,
                    Transparent, 1.05f, Loc.T(L.Message.StarredTitle), HoverLabelSide.Below))
            {
                router.Push(MessageRoute.Starred);
            }
        }

        if (activeTab == MessageTab.Contacts)
        {
            var shieldCenter = new Vector2(area.Max.X - 24f * scale, area.Center.Y);
            if (ui.IconButton(shieldCenter, 16f * scale, FontAwesomeIcon.ShieldAlt.ToIconString(), ui.BodyInk,
                    Transparent, 1.2f, Loc.T(L.Friends.NewNumberTitle), HoverLabelSide.Below) && session.IsSignedIn)
            {
                router.Push(MessageRoute.Safety);
            }

            var request = contacts.NumberChange;
            if (request is not null && request.Status == "pending")
            {
                ImGui.GetWindowDrawList().AddCircleFilled(shieldCenter + new Vector2(10f * scale, -10f * scale),
                    4f * scale, ImGui.GetColorU32(ui.Accent), 16);
            }
        }
    }

    private void DrawBottomNav(Rect nav)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(nav.Min, new Vector2(nav.Max.X, nav.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        var width = nav.Width / 3f;
        var chatsRect = new Rect(nav.Min, new Vector2(nav.Min.X + width, nav.Max.Y));
        var callsRect = new Rect(new Vector2(nav.Min.X + width, nav.Min.Y),
            new Vector2(nav.Min.X + width * 2f, nav.Max.Y));
        var contactsRect = new Rect(new Vector2(nav.Min.X + width * 2f, nav.Min.Y), nav.Max);
        UiAnchors.Report("message.tab.chats", chatsRect);
        UiAnchors.Report("message.tab.calls", callsRect);
        UiAnchors.Report("message.tab.contacts", contactsRect);
        DrawNavItem(chatsRect, FontAwesomeIcon.Comments, Loc.T(L.Message.TabChats), MessageTab.Chats,
            store.UnreadTotal);
        DrawNavItem(callsRect, FontAwesomeIcon.Phone, Loc.T(L.Phone.Calls), MessageTab.Calls, calls.UnseenMissed);
        DrawNavItem(contactsRect, FontAwesomeIcon.AddressBook, Loc.T(L.Apps.Contacts), MessageTab.Contacts, 0);
    }

    private void DrawNavItem(Rect rect, FontAwesomeIcon icon, string label, MessageTab tab, int badge)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var active = activeTab == tab;
        var color = active ? ui.Accent : ui.MutedInk;
        var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + 20f * scale);
        AppSkin.Icon(iconCenter, icon.ToIconString(), color, 1.2f);
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Min.Y + 42f * scale), label, color, 0.72f,
            active ? FontWeight.SemiBold : FontWeight.Regular);
        if (badge > 0)
        {
            var badgeCenter = new Vector2(iconCenter.X + 12f * scale, iconCenter.Y - 9f * scale);
            ImGui.GetWindowDrawList().AddCircleFilled(badgeCenter, 7f * scale, ImGui.GetColorU32(theme.Danger), 16);
            Typography.DrawCentered(badgeCenter, badge > 9 ? "9+" : badge.ToString(Loc.Culture), White, 0.62f,
                FontWeight.SemiBold);
        }

        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            SelectTab(tab);
        }
    }

    private void SelectTab(MessageTab tab)
    {
        activeTab = tab;
        chatMenu.Close();
        filter = string.Empty;
    }

    public void Dispose()
    {
        composer.Dispose();
        voicePlayer.Dispose();
        store.Dispose();
        contacts.Dispose();
    }
}
