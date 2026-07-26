using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Announcements;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp : IPhoneApp
{
    private const float RefreshSeconds = 30f;
    private const float MeasureWidth = 10000f;

    private static readonly Vector4 OnAccentInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 OnAccentMutedInk = new(1f, 1f, 1f, 0.82f);

    public string Id => AnnouncementsStore.AppId;
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Announcements);
    public string Glyph => "An";
    public int BadgeCount => store.UnreadCount;

    private readonly AnnouncementsStore store;
    private readonly AnnouncementsLauncher launcher;
    private readonly AppSkin ui = new(AppPalettes.Announcements);
    private readonly ViewRouter<AnnouncementsRoute> router;
    private readonly RouterDraw<AnnouncementsRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float sinceRefresh;
    private bool resetScroll;

    public AnnouncementsApp(AethernetSession session, AnnouncementsClient client, NotificationService notifications,
        Configuration configuration, AnnouncementsLauncher launcher)
    {
        store = new AnnouncementsStore(session, client, notifications, configuration);
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
        if (launcher.TryConsumeDetail(out var announcementId))
        {
            router.Push(AnnouncementsRoute.Detail(announcementId), false);
        }

        store.Refresh();
        store.MarkAllSeen();
    }

    public void OnClosed()
    {
        router.Reset();
        store.MarkAllSeen();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);

        if (!store.IsSignedIn)
        {
            ui.Body(context.Content);
            AppHeader.Draw(context, DisplayName, navigation.Back);
            var top = context.Content.Min.Y + AppHeader.Height * scale;
            var body = new Rect(new Vector2(context.Content.Min.X, top), context.Content.Max);
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.Announcements.SignInTitle),
                Loc.T(L.Announcements.SignInRequired));
            return;
        }

        TickRefresh();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(AnnouncementsRoute route, Rect area, int depth)
    {
        ui.Body(area);
        if (route.Screen == AnnouncementsScreen.Detail)
        {
            DrawDetail(area, route.Id!);
            return;
        }

        DrawList(area);
    }

    private void TickRefresh()
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private AnnouncementDto? Resolve(string announcementId)
    {
        var announcements = store.Announcements;
        for (var index = 0; index < announcements.Length; index++)
        {
            if (announcements[index].Id == announcementId)
            {
                return announcements[index];
            }
        }

        return null;
    }

    private static string[] ClampLines(string text, in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0f)
        {
            return Array.Empty<string>();
        }

        var wrapped = Typography.WrapText(text, style, maxWidth);
        if (wrapped.Length <= maxLines)
        {
            return wrapped;
        }

        var trimmed = new string[maxLines];
        Array.Copy(wrapped, trimmed, maxLines);
        trimmed[maxLines - 1] = Typography.FitText(trimmed[maxLines - 1] + "…", maxWidth, style);
        return trimmed;
    }

    private static float LineHeight(in TextStyle style) =>
        Typography.MeasureWrappedBlock("Ag", style, MeasureWidth).Y;

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 tip, float size, float thickness, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
