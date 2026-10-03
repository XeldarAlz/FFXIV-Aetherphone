using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notifications;

internal sealed partial class NotificationsApp : IPhoneApp
{
    private const float BottomPad = 28f;
    private const float BlockGap = 16f;
    private const string SettingsAppId = "settings";

    public string Id => NotificationChannels.NotificationsAppId;
    public string DisplayName => Loc.T(L.Apps.Notifications);
    public string Glyph => "N";
    public int BadgeCount => notifications.UnreadCount;
    public bool HasBadge => true;

    private readonly PhoneServices services;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Notifications);
    private readonly NotificationGroups groups = new();
    private readonly List<NotificationAppCount> appCounts = new();
    private readonly List<NotificationGroup>[] sections =
        { new List<NotificationGroup>(), new List<NotificationGroup>(), new List<NotificationGroup>() };
    private readonly NavBarButton[] navButtons = new NavBarButton[2];
    private readonly Action clearConfirmed;
    private NotificationDeck? deck;
    private INavigator navigation = null!;
    private int builtVersion = -1;
    private LanguageInfo? builtLanguage;
    private DateTime builtDay;
    private string? builtFilter;
    private string? appFilter;
    private string? pendingClearApp;
    private int visibleCount;

    public NotificationsApp(PhoneServices services, IReadOnlyList<IPhoneApp> apps)
    {
        this.services = services;
        this.apps = apps;
        notifications = services.Notifications;
        configuration = services.Configuration;
        confirm = services.Confirm;
        clearConfirmed = ClearConfirmed;
    }

    public void OnOpened()
    {
        services.SocialNotifications.AcknowledgeAll();
        notifications.MarkAllRead();
        deck?.Reset();
        appFilter = null;
        filterRail.Reset();
        builtVersion = -1;
    }

    public void OnClosed() => deck?.Reset();

    public void Draw(in PhoneContext context)
    {
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        var scale = UiScale.Current;
        var area = context.Content;
        ui.Backdrop(SceneChrome.ScreenFrom(area, context.Theme, scale));
        ui.Body(area);
        deck ??= new NotificationDeck(notifications, CreateRouter(context.Navigation));
        EnsureBuilt();
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);

        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            var windowMin = ImGui.GetWindowPos();
            var bounds = new Rect(windowMin, windowMin + ImGui.GetWindowSize());
            deck.UpdateDrag(scale, deltaSeconds, null);
            deck.BeginFrame(bounds);
            if (deck.DragActive || UiInteract.HoverWindowOnly(bounds.Min, bounds.Max, false))
            {
                UiInteract.ReportGestureSurface();
            }

            DrawStatus(width, scale);
            if (groups.Count == 0)
            {
                DrawEmpty(new Rect(ImGui.GetCursorScreenPos(), navBar.Body.Max), scale);
            }
            else
            {
                DrawFilters(scale);
                DrawAppCard(width, scale);
                var listTop = ImGui.GetCursorScreenPos();
                DrawSections(deck, width, scale);
                UiAnchors.Report("notifications.list",
                    new Rect(listTop, new Vector2(listTop.X + width, MathF.Max(listTop.Y, MathF.Min(bounds.Max.Y,
                        ImGui.GetCursorScreenPos().Y)))));
                DrawSettingsLink(width, scale);
            }

            ImGui.Dummy(new Vector2(0f, BottomPad * scale));
            deck.EndFrame(scale, true, null);
            if (deck.Swiping)
            {
                surface.CancelDrag();
            }
        }

        deck.Advance(deltaSeconds);
        var buttonCount = BuildNavButtons();
        UiAnchors.Report("notifications.focus", AppHeader.LargeTitleButtonRect(in navBar, 0, buttonCount));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "notifications.nav", DisplayName,
            NavBarStyle.From(ui), navButtons.AsSpan(0, buttonCount));
        HandleNavButton(pressed);
    }

    private NotificationRouter CreateRouter(INavigator navigator) =>
        new(navigator, notifications, services.SocialNotifications, services.LinkpearlLauncher,
            services.VelvetLauncher, services.DmLauncher, services.GramDmLauncher, services.SocialLauncher,
            services.MusterLauncher, services.YellowPagesLauncher, services.AnnouncementsLauncher,
            services.SafetyLauncher, services.EncryptionSetup, services.RadioLauncher, services.CasinoLauncher,
            services.AetherStreamLauncher, services.HuntsLauncher, services.JamLauncher, services.FeedbackLauncher);

    private void EnsureBuilt()
    {
        var language = Loc.Current;
        var today = DateTime.Today;
        if (builtVersion == notifications.Version && ReferenceEquals(builtLanguage, language) &&
            builtDay == today && ReferenceEquals(builtFilter, appFilter))
        {
            return;
        }

        builtVersion = notifications.Version;
        builtLanguage = language;
        builtDay = today;
        var recent = notifications.Recent;
        groups.Rebuild(recent);
        deck!.Sync(groups);
        NotificationSections.Tally(recent, appCounts);
        if (appFilter is not null && IndexOfApp(appFilter) < 0)
        {
            appFilter = null;
        }

        builtFilter = appFilter;
        for (var index = 0; index < sections.Length; index++)
        {
            sections[index].Clear();
        }

        visibleCount = 0;
        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            var group = list[index];
            if (!NotificationSections.Matches(group, appFilter))
            {
                continue;
            }

            sections[(int)NotificationSections.Of(group.Newest.ReceivedAt, today)].Add(group);
            visibleCount += group.Count;
        }

        RebuildFilterLabels();
        appCardText.Reset();
    }

    private int IndexOfApp(string appId)
    {
        for (var index = 0; index < appCounts.Count; index++)
        {
            if (string.Equals(appCounts[index].AppId, appId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private string AppName(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (string.Equals(apps[index].Id, appId, StringComparison.Ordinal))
            {
                return apps[index].DisplayName;
            }
        }

        var channels = NotificationChannels.All;
        for (var index = 0; index < channels.Count; index++)
        {
            if (string.Equals(channels[index].AppId, appId, StringComparison.Ordinal))
            {
                return Loc.T(channels[index].Name);
            }
        }

        return appId;
    }

    private void DrawSections(NotificationDeck activeDeck, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var style = DeckStyle();
        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            var section = sections[sectionIndex];
            if (section.Count == 0)
            {
                continue;
            }

            SectionHeader(SectionTitle((NotificationSection)sectionIndex), width, scale);
            for (var index = 0; index < section.Count; index++)
            {
                var group = section[index];
                var origin = ImGui.GetCursorScreenPos();
                var height = activeDeck.Height(group, scale);
                if (ImGui.IsRectVisible(origin, origin + new Vector2(width, height)))
                {
                    activeDeck.DrawGroup(drawList, group, origin, width, style, scale, 1f, true);
                }

                Advance(origin, width, height, NotificationDeck.GroupGap, scale);
            }
        }
    }

    private NotificationDeckStyle DeckStyle()
    {
        var backdrop = ui.BackdropColor;
        return NotificationDeckStyle.ForCards(Palette.Mix(backdrop, White, CardLift),
            Palette.Mix(backdrop, White, LayerLift), ui.TitleInk, ui.MutedInk, ui.HeaderInk, ui.Theme.Danger);
    }

    private static string SectionTitle(NotificationSection section) => section switch
    {
        NotificationSection.Today => Loc.T(L.Time.Today),
        NotificationSection.Yesterday => Loc.T(L.Time.Yesterday),
        _ => Loc.T(L.Notifications.Earlier),
    };

    private void SectionHeader(string title, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = SectionHeaderHeight * scale;
        var fitted = Typography.FitText(title, MathF.Max(1f, width), TextStyles.Title3);
        var textHeight = Typography.Measure(fitted, TextStyles.Title3).Y;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X, origin.Y + (height - textHeight) * 0.5f),
            fitted, ui.TitleInk, TextStyles.Title3);
        Advance(origin, width, height, 0f, scale);
    }

    private int BuildNavButtons()
    {
        navButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Moon),
            Loc.T(configuration.DoNotDisturb ? L.Notifications.TurnOffFocus : L.Notifications.TurnOnFocus));
        if (visibleCount == 0)
        {
            return 1;
        }

        navButtons[1] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Times), Loc.T(L.Notifications.ClearTooltip));
        return 2;
    }

    private void HandleNavButton(int pressed)
    {
        if (pressed == 0)
        {
            ToggleDoNotDisturb();
            return;
        }

        if (pressed == 1)
        {
            AskClear();
        }
    }

    private void ToggleDoNotDisturb()
    {
        configuration.DoNotDisturb = !configuration.DoNotDisturb;
        configuration.Save();
        UiFeedback.Play(configuration.DoNotDisturb ? UiSound.ToggleOn : UiSound.ToggleOff);
    }

    private void AskClear()
    {
        if (visibleCount == 0)
        {
            return;
        }

        pendingClearApp = appFilter;
        var title = appFilter is null
            ? Loc.T(L.Notifications.ClearAllTitle)
            : Loc.T(L.Notifications.ClearAppTitle, AppName(appFilter));
        confirm.Ask(new ConfirmRequest
        {
            Title = title,
            Message = Loc.Plural(L.Notifications.ClearBody, visibleCount),
            ConfirmLabel = Loc.T(L.Notifications.Clear),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = clearConfirmed,
        });
    }

    private void ClearConfirmed()
    {
        if (deck is null)
        {
            return;
        }

        if (pendingClearApp is null)
        {
            deck.Router.AcknowledgeAll();
            notifications.Clear();
            deck.Reset();
            return;
        }

        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            if (NotificationSections.Matches(list[index], pendingClearApp))
            {
                deck.ClearGroup(list[index]);
            }
        }

        pendingClearApp = null;
        appFilter = null;
    }

    private void OpenSettings(string? appId)
    {
        UiFeedback.Play(UiSound.Tap);
        if (appId is null)
        {
            services.SettingsLauncher.Request(SettingsPageKind.Notifications);
        }
        else
        {
            services.SettingsLauncher.RequestAppNotifications(appId);
        }

        navigation.Open(SettingsAppId);
    }

    private static void Advance(Vector2 origin, float width, float height, float gap, float scale)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + gap * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    public void Dispose()
    {
    }
}
