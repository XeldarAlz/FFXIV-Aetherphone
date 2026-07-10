using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.DirectMessages;

internal sealed partial class DirectMessagesApp : IPhoneApp
{
    private const float ThreadPollSeconds = 3f;
    private const float TypingSendSeconds = 2.5f;
    private const int MessageMax = 1000;

    public string Id => "dm";
    public string DisplayName => Loc.T(L.Apps.Messages);
    public string Glyph => "Ms";
    public int BadgeCount => store.UnreadTotal;

    private readonly DirectMessagesStore store;
    private readonly ContactBook contacts;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly DmLauncher launcher;
    private readonly PhotoLibrary library;
    private readonly HttpService http;
    private readonly AppSkin ui = new(AppPalettes.Messenger);
    private readonly ViewRouter<DmRoute> router;
    private readonly RouterDraw<DmRoute> drawView;
    private readonly ChatTranscript transcript = new();
    private readonly EncryptionGate encryptionGate;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private string filter = string.Empty;
    private string messageDraft = string.Empty;
    private bool threadFocus;
    private float sinceThreadPoll;
    private float sinceTypingSend = TypingSendSeconds;
    private string lastTypingDraft = string.Empty;
    private ChatMessageDto[] transcriptSource = Array.Empty<ChatMessageDto>();
    private TranscriptMessage[] transcriptCache = Array.Empty<TranscriptMessage>();
    private Func<string, string?>? threadMediaUrl;
    private Action<string>? onThreadImageClick;
    private bool composeGroupMode;
    private string groupTitleDraft = string.Empty;
    private readonly HashSet<string> selectedContacts = new();
    private volatile bool composeBusy;
    private volatile string? composeResult;
    private string? imageViewId;
    private volatile int imageSaveOutcome;
    private volatile bool imageSaveBusy;
    private string[] chatPickerPaths = Array.Empty<string>();
    private string? chatPickerConversationId;
    private string? chatPendingPickedPath;
    private readonly PhotoZoomView imageZoom = new();

    public DirectMessagesApp(DirectMessagesStore store, ContactBook contacts, AethernetSession session,
        RemoteImageCache images, LodestoneService lodestone, DmLauncher launcher, PhotoLibrary library, HttpService http,
        KeyVault keyVault, ConversationKeyStore conversationKeys, Configuration configuration)
    {
        this.store = store;
        this.contacts = contacts;
        this.session = session;
        this.images = images;
        this.lodestone = lodestone;
        this.launcher = launcher;
        this.library = library;
        this.http = http;
        encryptionGate = new EncryptionGate(keyVault, conversationKeys, configuration);
        router = new ViewRouter<DmRoute>(DmRoute.List, Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        filter = string.Empty;
        contacts.Refresh(force: true);
        store.RefreshConversations();
        if (launcher.TryConsumeConversation(out var conversationId))
        {
            router.Push(DmRoute.Thread(conversationId), false);
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
        router.Reset();
        filter = string.Empty;
        messageDraft = string.Empty;
        selectedContacts.Clear();
        composeGroupMode = false;
        groupTitleDraft = string.Empty;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var delta = ImGui.GetIO().DeltaTime;
        ProcessComposeResult();
        messageMenu.Gate();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, delta, drawView);
    }

    private void ProcessComposeResult()
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
            if (router.Current.Screen == DmScreen.AddMembers)
            {
                router.Pop();
            }

            store.RefreshDetail();
        }

        var result = composeResult;
        if (result is null)
        {
            return;
        }

        composeResult = null;
        selectedContacts.Clear();
        groupTitleDraft = string.Empty;
        composeGroupMode = false;
        if (router.Current == DmRoute.NewChat)
        {
            router.Pop();
        }

        router.Push(DmRoute.Thread(result));
    }

    private void DrawView(DmRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case DmScreen.Thread:
                DrawThread(area, route.Id ?? string.Empty);
                break;
            case DmScreen.NewChat:
                DrawNewChat(area);
                break;
            case DmScreen.GroupInfo:
                DrawGroupInfo(area, route.Id ?? string.Empty);
                break;
            case DmScreen.AddMembers:
                DrawAddMembers(area, route.Id ?? string.Empty);
                break;
            case DmScreen.ChatImage:
                DrawChatImagePicker(area, route.Id ?? string.Empty);
                break;
            case DmScreen.ImageView:
                DrawImageViewer(area, route.Id ?? string.Empty);
                break;
            default:
                DrawList(area);
                break;
        }
    }

    private void DrawList(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName, AppPalettes.Messenger.TitleInk,
            1.3f, FontWeight.Bold);
        var top = area.Min.Y + AppHeader.Height * scale;
        if (!session.IsSignedIn)
        {
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            EmptyState.Draw(body, ui, FontAwesomeIcon.Comments, DisplayName, Loc.T(L.DirectMessages.SignInPrompt));
            return;
        }

        var content = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (encryptionGate.ShouldBlock)
        {
            encryptionGate.Draw(content, theme, ui.Accent);
            return;
        }

        if (!store.ConversationsLoaded && !store.LoadingConversations)
        {
            store.RefreshConversations();
        }

        var searchHeight = 52f * scale;
        SearchField.DrawSubmit(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
            "##dmFilter", Loc.T(L.Phone.FilterHint), ref filter, AppPalettes.Messenger);
        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max);
        var entries = Filtered();
        if (entries.Count == 0)
        {
            if (filter.Trim().Length > 0)
            {
                EmptyState.Draw(listRect, ui, FontAwesomeIcon.Search, Loc.T(L.Phone.NoOneFound), string.Empty);
            }
            else
            {
                EmptyState.Draw(listRect, ui, FontAwesomeIcon.Comments, Loc.T(L.DirectMessages.Empty),
                    Loc.T(L.DirectMessages.EmptyHint));
            }
        }
        else
        {
            using (AppSurface.Begin(listRect))
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < entries.Count; index++)
                {
                    DrawConversationRow(entries[index], scale);
                }

                ImGui.Dummy(new Vector2(0f, 72f * scale));
            }
        }

        if (ComposeFab.Draw(listRect, "##dmNewFab", ui.Accent, FontAwesomeIcon.Pen.ToIconString(),
                Loc.T(L.DirectMessages.NewMessage)))
        {
            selectedContacts.Clear();
            groupTitleDraft = string.Empty;
            composeGroupMode = false;
            router.Push(DmRoute.NewChat);
        }
    }

    private List<ConversationDto> Filtered()
    {
        var snapshot = store.Conversations;
        var list = new List<ConversationDto>(snapshot.Length);
        var query = filter.Trim();
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (query.Length == 0 || DirectMessagesStore.DisplayTitle(snapshot[index]).Contains(query,
                    StringComparison.OrdinalIgnoreCase))
            {
                list.Add(snapshot[index]);
            }
        }

        return list;
    }

    private void DrawConversationRow(ConversationDto item, float scale)
    {
        var rowHeight = 62f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale);
        var pad = 12f * scale;
        var radius = 22f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var title = DirectMessagesStore.DisplayTitle(item);
        if (item.IsGroup)
        {
            drawList.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)), 32);
            AppSkin.Icon(avatarCenter, FontAwesomeIcon.Users.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 1f);
        }
        else
        {
            AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, title, string.Empty, item.OtherAvatarUrl,
                images, lodestone, 0.95f, 32);
        }

        var textLeft = avatarCenter.X + radius + 12f * scale;
        var textRight = origin.X + width - (item.UnreadCount > 0 ? 40f * scale : 14f * scale);
        var textWidth = textRight - textLeft;
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale),
            Typography.FitText(title, textWidth, 1f, FontWeight.SemiBold), theme.TextStrong, 1f, FontWeight.SemiBold);
        var previewColor = item.UnreadCount > 0 ? theme.TextStrong : AppPalettes.Messenger.MutedInk;
        var preview = item.LastMessagePreview.Length > 0
            ? item.LastMessagePreview
            : (item.LastMessageKind == 1 ? Loc.T(L.DirectMessages.PhotoPreview) : string.Empty);
        Typography.Draw(new Vector2(textLeft, origin.Y + 33f * scale),
            Typography.FitText(preview, textWidth, 0.85f, FontWeight.Regular), previewColor, 0.85f);
        if (item.UnreadCount > 0)
        {
            var badgeCenter = new Vector2(origin.X + width - 22f * scale, origin.Y + rowHeight * 0.5f);
            drawList.AddCircleFilled(badgeCenter, 9f * scale, ImGui.GetColorU32(ui.Accent), 20);
            Typography.DrawCentered(badgeCenter, item.UnreadCount.ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
                0.75f, FontWeight.SemiBold);
        }

        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + rowHeight)))
        {
            router.Push(DmRoute.Thread(item.Id));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    public void Dispose()
    {
        encryptionGate.Dispose();
        store.Dispose();
    }
}
