using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Dev;

internal sealed partial class DevApp : IPhoneApp
{
    private const float ChatPollSeconds = 2.5f;
    private const float BoardPollSeconds = 10f;
    private const int MessageMax = 1000;
    private const int CardTitleMax = 120;
    private const int CardBodyMax = 4000;

    private static readonly string[] ColumnLabels = { "To Do", "In Progress", "Done" };

    public string Id => "dev";
    public string DisplayName => "Dev";
    public string Glyph => "De";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => store.UnreadCount;
    public bool IsAvailable => session.HasDevAccess;

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly DevStore store;
    private readonly LodestoneService lodestone;
    private readonly PhotoLibrary library;
    private readonly HttpService http;
    private readonly RemoteImageCache images;
    private readonly AppSkin ui = new(AppPalettes.Dev);
    private readonly ViewRouter<DevRoute> router;
    private readonly RouterDraw<DevRoute> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private DevTab activeTab = DevTab.Board;
    private int boardColumn;
    private float boardSegmentAnim;
    private float detailSegmentAnim;
    private string? detailSegmentCardId;
    private float sinceChatPoll;
    private float sinceBoardPoll;
    private string messageDraft = string.Empty;
    private bool chatFocus;
    private string[] chatPickerPaths = Array.Empty<string>();
    private bool chatPickerLoaded;
    private string? chatPendingPickedPath;
    private readonly List<BubbleEntrance> chatEntrances = new();
    private int entranceSettled;
    private bool entrancePrimed;
    private string? entranceLastId;
    private bool followChatBottom = true;
    private bool snapChatToBottom;
    private float olderAnchorFromBottom = -1f;
    private int olderBaselineCount;
    private int olderSettleFrames;
    private float olderElapsed;
    private float olderSpinnerPhase;
    private string? imageViewId;
    private volatile int imageSaveOutcome;
    private volatile bool imageSaveBusy;
    private string cardTitleDraft = string.Empty;
    private string cardBodyDraft = string.Empty;
    private string? cardEditLoadedFor;
    private readonly string[] segmentLabels = { ColumnLabels[0], ColumnLabels[1], ColumnLabels[2] };
    private readonly int[] segmentLabelCounts = { -1, -1, -1 };

    public DevApp(AethernetSession session, AethernetClient client, LodestoneService lodestone,
        Configuration configuration, PhotoLibrary library, HttpService http, RemoteImageCache images)
    {
        this.session = session;
        this.client = client;
        this.lodestone = lodestone;
        this.library = library;
        this.http = http;
        store = new DevStore(session, client, configuration);
        this.images = images;
        router = new ViewRouter<DevRoute>(DevRoute.Root, Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = DevTab.Board;
        boardColumn = 0;
        boardSegmentAnim = 0f;
        sinceChatPoll = 0f;
        sinceBoardPoll = 0f;
        chatPickerLoaded = false;
        client.EnsureCurrentUser();
        if (session.HasDevAccess)
        {
            store.EnsureLoaded();
        }
    }

    public void OnClosed()
    {
        router.Reset();
        messageDraft = string.Empty;
        cardTitleDraft = string.Empty;
        cardBodyDraft = string.Empty;
        cardEditLoadedFor = null;
        chatEntrances.Clear();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        if (!session.HasDevAccess)
        {
            context.Navigation.GoHome();
            return;
        }

        store.EnsureLoaded();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(DevRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case DevScreen.CardDetail:
                DrawCardDetail(area, route.Id!);
                break;
            case DevScreen.CardCompose:
                DrawCardCompose(area);
                break;
            case DevScreen.CardEdit:
                DrawCardEdit(area, route.Id!);
                break;
            case DevScreen.ChatImage:
                DrawChatImagePicker(area);
                break;
            case DevScreen.ImageView:
                DrawImageViewer(area, route.Id!);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        TickPolling();
        var headerHeight = 42f * scale;
        var navHeight = 60f * scale;
        var headerRect = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + headerHeight));
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - navHeight), area.Max);
        var contentArea = new Rect(new Vector2(area.Min.X, headerRect.Max.Y), new Vector2(area.Max.X, navRect.Min.Y));
        Typography.DrawCentered(new Vector2(headerRect.Center.X, headerRect.Center.Y),
            activeTab == DevTab.Chat ? "Dev Chat" : "Dev Board", AppPalettes.Dev.TitleInk, 1.2f, FontWeight.SemiBold);
        if (activeTab == DevTab.Chat)
        {
            DrawChat(contentArea);
        }
        else
        {
            DrawBoard(contentArea);
        }

        DrawBottomNav(navRect);
    }

    private void TickPolling()
    {
        var delta = ImGui.GetIO().DeltaTime;
        sinceBoardPoll += delta;
        if (sinceBoardPoll >= BoardPollSeconds)
        {
            sinceBoardPoll = 0f;
            store.RefreshBoard();
        }

        if (activeTab != DevTab.Chat)
        {
            return;
        }

        sinceChatPoll += delta;
        if (sinceChatPoll >= ChatPollSeconds)
        {
            sinceChatPoll = 0f;
            store.PollChat();
        }
    }

    private void DrawBottomNav(Rect nav)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(nav.Min, new Vector2(nav.Max.X, nav.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        var width = nav.Width / 2f;
        DrawNavItem(new Rect(nav.Min, new Vector2(nav.Min.X + width, nav.Max.Y)), FontAwesomeIcon.Columns, "Board",
            DevTab.Board, 0);
        DrawNavItem(new Rect(new Vector2(nav.Min.X + width, nav.Min.Y), nav.Max), FontAwesomeIcon.Comment, "Chat",
            DevTab.Chat, store.UnreadCount);
    }

    private void DrawNavItem(Rect rect, FontAwesomeIcon icon, string label, DevTab tab, int badge)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var active = activeTab == tab;
        var color = active ? Palette.Mix(Accent, theme.TextStrong, 0.35f) : AppPalettes.Dev.MutedInk;
        var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + 20f * scale);
        AppSkin.Icon(iconCenter, icon.ToIconString(), color, 1.05f);
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Min.Y + 42f * scale), label, color, 0.72f,
            active ? FontWeight.SemiBold : FontWeight.Regular);
        if (badge > 0)
        {
            var badgeCenter = new Vector2(iconCenter.X + 12f * scale, iconCenter.Y - 9f * scale);
            ImGui.GetWindowDrawList().AddCircleFilled(badgeCenter, 7f * scale, ImGui.GetColorU32(theme.Danger), 16);
            Typography.DrawCentered(badgeCenter, badge > 9 ? "9+" : badge.ToString(), new Vector4(1f, 1f, 1f, 1f),
                0.62f, FontWeight.SemiBold);
        }

        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            activeTab = tab;
        }
    }

    private void DrawSegmentStrip(Rect rect, string[] labels, int active, ref float anim, Action<int> onSelect)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
        anim += (active - anim) * MathF.Min(1f, ImGui.GetIO().DeltaTime * 14f);
        var segmentWidth = rect.Width / labels.Length;
        var pad = 3f * scale;
        var thumbMinX = rect.Min.X + pad + anim * segmentWidth;
        var thumbMin = new Vector2(thumbMinX, rect.Min.Y + pad);
        var thumbMax = new Vector2(thumbMinX + segmentWidth - pad * 2f, rect.Max.Y - pad);
        Squircle.Fill(drawList, thumbMin, thumbMax, (thumbMax.Y - thumbMin.Y) * 0.5f, ImGui.GetColorU32(Accent));
        for (var index = 0; index < labels.Length; index++)
        {
            var segmentRect = new Rect(new Vector2(rect.Min.X + segmentWidth * index, rect.Min.Y),
                new Vector2(rect.Min.X + segmentWidth * (index + 1), rect.Max.Y));
            var ink = index == active ? new Vector4(1f, 1f, 1f, 1f) : AppPalettes.Dev.MutedInk;
            Typography.DrawCentered(segmentRect.Center, labels[index], ink, 0.82f,
                index == active ? FontWeight.SemiBold : FontWeight.Medium);
            if (index != active && UiInteract.HoverClick(segmentRect.Min, segmentRect.Max))
            {
                onSelect(index);
            }
        }
    }

    private AvatarHandle AvatarFor(string userId, string? avatarUrl) => lodestone.Remote(userId, ToUri(avatarUrl));

    private static string Monogram(string displayName, string handle)
    {
        var source = string.IsNullOrEmpty(displayName) ? handle : displayName;
        return source.Length > 0 ? source[..1].ToUpperInvariant() : "?";
    }

    private static Uri? ToUri(string? url) =>
        string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ? null : uri;

    private struct BubbleEntrance
    {
        public int Line;
        public float Elapsed;
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
