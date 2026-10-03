using Aetherphone.Core.Onboarding;
using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct MinimizedDrag
{
    public readonly Vector2 Delta;
    public readonly bool Released;

    public MinimizedDrag(Vector2 delta, bool released)
    {
        Delta = delta;
        Released = released;
    }
}

internal sealed partial class MinimizedPhone : IDisposable
{
    private enum SlotMode : byte
    {
        None,
        Pages,
        Music,
        PcMusic,
        Call,
    }

    private enum FaceSurface : byte
    {
        Plain,
        Wallpaper,
        Map,
    }

    private const float DragSlop = 5f;
    private const float CardHoldSeconds = 4.5f;
    private const float PulseSeconds = 0.8f;
    private const float TooltipClearance = 44f;
    private const int MaxQueuedCards = 3;
    private const float ZoomSaveDelay = 0.9f;
    private const float ControlThreshold = 0.6f;
    private const float SwapThreshold = 0.03f;
    private const float GripSizeFactor = 0.7f;
    private const float CalmWallpaperScrim = 0.30f;
    private const float HarshWallpaperScrim = 0.58f;
    private const float MutedInk = 0.72f;
    private const string MusicAppId = "music";
    private const string CallAppId = "message";
    private static readonly TimeSpan ShowingGrace = TimeSpan.FromSeconds(0.5);
    private static readonly FaceInk LightInk = new(new Vector4(1f, 1f, 1f, 1f), new Vector4(1f, 1f, 1f, MutedInk));
    private static readonly Vector4 MusicAccent = AppAccents.For("music");

    private readonly PlaybackHub playback;
    private readonly CallHub calls;
    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly INavigator navigation;
    private readonly Configuration configuration;
    private readonly MinimizedLayoutService layout;
    private readonly ThemeProvider themes;
    private readonly MinimizedFeed feed;
    private readonly MinimapReader minimap;
    private readonly LiveBackdrop liveBackdrop;
    private readonly ResizeGrip resizeGrip = new();
    private readonly Queue<PhoneNotification> queuedCards = new();
    private readonly MinimizedPart[] pageParts = new MinimizedPart[MinimizedParts.Count];
    private Spring hover;
    private Spring badge;
    private Spring dnd;
    private Spring card;
    private Spring mapSpan;
    private Spring slotPresence;
    private Spring contentFade;
    private Spring island;
    private Spring pagePosition;
    private SlotMode displayedMode;
    private bool liveCall;
    private bool liveMusic;
    private int pageCount;
    private int pageIndex;
    private int pagesRevision = -1;
    private float zoomSaveDue;
    private bool zoomDirty;
    private float resizeStartScale;
    private Vector2 resizeStartSize;
    private PhoneNotification? cardNotification;
    private bool cardDismissed;
    private float cardElapsed;
    private float clock;
    private float pulseRemaining;
    private Vector4 pulseAccent;
    private bool pressed;
    private bool dragging;
    private Vector2 pressOrigin;
    private Vector2 dragDelta;
    private bool dragReleased;
    private bool slotHovered;
    private bool bannerHovered;
    private bool controlHovered;
    private string? badgeAppId;
    private string countLabel = string.Empty;
    private int countValue = -1;
    private string durationLabel = string.Empty;
    private int durationSeconds = -1;
    private string timeLabel = string.Empty;
    private string dateLabel = string.Empty;
    private int timeKey = -1;
    private int timeFormat = -1;
    private int dateKey = -1;
    private float measuredWidth = -1f;
    private CultureInfo? textCulture;
    private float clockScale;
    private float mapClockScale;
    private Vector2 dateSize;
    private int textFrame = -1;
    private DateTime lastInteractiveDrawUtc = DateTime.MinValue;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private CallView frameView;
    private FaceInk frameInk = LightInk;
    private FaceSurface frameSurface;
    private Rect frameScreen;
    private float frameScale = 1f;
    private float frameAlpha = 1f;
    private bool frameInteractive;
    private bool frameBodyHovered;

    public MinimizedPhone(PhoneServices services, NotificationRouter router, INavigator navigation,
        MinimizedLayoutService layout)
    {
        playback = services.Playback;
        pcMedia = services.PcMedia;
        calls = services.Calls;
        notifications = services.Notifications;
        configuration = services.Configuration;
        this.router = router;
        this.navigation = navigation;
        this.layout = layout;
        themes = services.Themes;
        feed = new MinimizedFeed(services.Weather, services.Coins, services.AethernetSession, services.Activity,
            services.GameData, services.GameTimers);
        minimap = new MinimapReader(services.ZoneMapTextures);
        liveBackdrop = services.LiveBackdrop;
        mapSpan = new Spring(MinimizedShapes.MapSpan(configuration.MinimizedMapZoom));
        notifications.Changed += RefreshBadge;
        notifications.Presented += OnPresented;
        notifications.Vibration += OnVibration;
        configuration.BadgeSettingsChanged += RefreshBadge;
        RefreshBadge();
    }

    public bool IsShowing => DateTime.UtcNow - lastInteractiveDrawUtc < ShowingGrace;

    public float Zoom => PhoneBounds.ClampMinimizedScale(configuration.MinimizedScale, IdleUnits);

    public Vector2 Measure() => IdleSize();

    public Vector2 IdleSize()
    {
        var size = IdleUnits * Scale;
        return new Vector2(MathF.Round(size.X), MathF.Round(size.Y));
    }

    private static float Scale => UiScale.Global * UiScale.Minimized;

    private static Vector2 IdleUnits => new(MinimizedShapes.BodyWidth, MinimizedShapes.BodyHeight);

    private bool ShowsMinimap => configuration.MinimizedShape == MinimizedShape.Minimap;

    private PhoneCaseKind CaseKind => themes.Chrome.CaseKind;

    public MinimizedDrag ConsumeDrag()
    {
        var result = new MinimizedDrag(dragDelta, dragReleased);
        dragDelta = Vector2.Zero;
        dragReleased = false;
        return result;
    }

    public bool Draw(Rect body, PhoneTheme theme, float delta)
    {
        var scale = Scale;
        var geometry = ChassisGeometry.Puck(body, theme.CaseKind);
        var drawList = ImGui.GetForegroundDrawList();
        var glassBody = !ShowsMinimap && liveBackdrop.TryRecordFor(body);
        if (glassBody)
        {
            Material.LiquidGlass(drawList, body.Min, body.Max, geometry.BodyRadius, scale, Material.ToneFor(theme),
                0f);
        }
        else
        {
            DeviceChrome.DrawShell(drawList, geometry, scale, theme, 1f);
        }

        var clicked = DrawFace(drawList, geometry, theme, delta, true, 1f, glassBody);
        if (TourCue.MiniPhone)
        {
            CoachmarkOverlay.DrawMiniCue(drawList, body, geometry.BodyRadius, scale);
        }

        return clicked;
    }

    public bool DrawFace(ImDrawListPtr drawList, in ChassisGeometry geometry, PhoneTheme theme, float delta,
        bool interactive, float alpha, bool glassBody = false)
    {
        clock += delta;
        feed.Update(delta);
        var mapFace = ShowsMinimap;
        if (mapFace)
        {
            minimap.Update(delta);
        }

        if (interactive)
        {
            lastInteractiveDrawUtc = DateTime.UtcNow;
        }

        var scale = Scale;
        var body = geometry.Body;
        var bodyHovered = interactive && UiInteract.Hover(body.Min, body.Max);
        var view = calls.Snapshot();
        StepState(delta, interactive, bodyHovered, view);
        if (alpha <= 0.001f)
        {
            return false;
        }

        RefreshText(scale);
        var wallpaper = !mapFace && configuration.MinimizedWallpaper;
        frameTheme = theme;
        frameView = view;
        frameScale = scale;
        frameAlpha = alpha;
        frameInteractive = interactive;
        frameBodyHovered = bodyHovered;
        frameScreen = geometry.Screen;
        frameSurface = mapFace ? FaceSurface.Map : wallpaper ? FaceSurface.Wallpaper : FaceSurface.Plain;
        frameInk = frameSurface == FaceSurface.Plain ? new FaceInk(theme.TextStrong, theme.TextMuted) : LightInk;
        slotHovered = false;
        bannerHovered = false;
        controlHovered = false;
        var screen = geometry.Screen;
        drawList.PushClipRect(screen.Min, screen.Max, true);
        if (mapFace)
        {
            DrawMapFace(drawList, screen);
        }
        else
        {
            if (wallpaper)
            {
                DrawWallpaperBackdrop(drawList, geometry, theme, alpha, glassBody);
            }

            DrawClockFace(drawList, screen);
        }

        DrawBanner(drawList, screen);
        drawList.PopClipRect();
        if ((mapFace || wallpaper) && !glassBody)
        {
            DeviceChrome.MaskScreenCorners(drawList, geometry, theme, scale);
        }

        if (pulseRemaining > 0f)
        {
            var strength = pulseRemaining / PulseSeconds;
            MinimizedPhoneRenderer.DrawPulse(drawList, geometry, pulseAccent, strength * strength * alpha, scale);
        }

        if (!interactive)
        {
            return false;
        }

        UpdateResize(drawList, body, scale, delta);
        HandleWheel(mapFace);
        return HandleGesture(body, scale, bodyHovered, controlHovered);
    }

    private void UpdateResize(ImDrawListPtr drawList, Rect body, float scale, float delta)
    {
        var grab = resizeGrip.Track(drawList, body.Max, scale * GripSizeFactor, delta);
        controlHovered |= grab.Engaged;
        if (grab.Started)
        {
            resizeStartScale = UiScale.Minimized;
            resizeStartSize = body.Size;
        }

        if (grab.Active)
        {
            var along = Vector2.Dot(grab.Delta, resizeStartSize) / MathF.Max(resizeStartSize.LengthSquared(), 1f);
            var clamped = PhoneBounds.ClampMinimizedScale(resizeStartScale * (1f + along), IdleUnits);
            var next = MinimizedShapes.SnapScale(clamped);
            if (MathF.Abs(next - configuration.MinimizedScale) > 0.001f)
            {
                configuration.MinimizedScale = next;
            }
        }

        if (grab.Committed)
        {
            configuration.Save();
        }
    }

    private void HandleWheel(bool mapFace)
    {
        if (!frameBodyHovered || pressed || controlHovered && !mapFace)
        {
            return;
        }

        var step = Math.Sign(ImGui.GetIO().MouseWheel);
        if (step == 0)
        {
            return;
        }

        if (mapFace)
        {
            StepZoom(step);
            return;
        }

        if (displayedMode != SlotMode.Pages || pageCount < 2)
        {
            return;
        }

        pageIndex = Math.Clamp(pageIndex - step, 0, pageCount - 1);
    }

    private void StepZoom(int step)
    {
        if (step == 0)
        {
            return;
        }

        var next = MinimizedShapes.ClampZoom(configuration.MinimizedMapZoom + step);
        if (next == configuration.MinimizedMapZoom)
        {
            return;
        }

        configuration.MinimizedMapZoom = next;
        zoomSaveDue = clock + ZoomSaveDelay;
        zoomDirty = true;
    }

    private static void DrawWallpaperBackdrop(ImDrawListPtr drawList, in ChassisGeometry geometry, PhoneTheme theme,
        float alpha, bool rounded)
    {
        var screen = geometry.Screen;
        var library = Plugin.Wallpapers;
        var aspect = screen.Height > 0f ? screen.Width / screen.Height : 0.5f;
        DrawWallpaperLayer(drawList, screen, geometry.ScreenRadius, library.Resolve(theme.LightWallpaperId), aspect,
            alpha, theme.ScreenBase, rounded);
        var darkness = library.ThemeDarkness;
        if (darkness > 0.001f)
        {
            DrawWallpaperLayer(drawList, screen, geometry.ScreenRadius, library.Resolve(theme.DarkWallpaperId), aspect,
                alpha * darkness, null, rounded);
        }

        var scrim = CalmWallpaperScrim +
                    (HarshWallpaperScrim - CalmWallpaperScrim) * WallpaperLegibility.Strength(theme);
        Squircle.Fill(drawList, screen.Min, screen.Max, geometry.ScreenRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, scrim * alpha)));
    }

    private static void DrawWallpaperLayer(ImDrawListPtr drawList, Rect screen, float radius, WallpaperEntry entry,
        float aspect, float alpha, Vector4? fallback, bool rounded)
    {
        if (rounded)
        {
            WallpaperRenderer.DrawSingleRounded(drawList, screen, radius, entry, aspect, alpha, fallback);
            return;
        }

        WallpaperRenderer.DrawSingle(drawList, screen, radius, entry, aspect, alpha, fallback);
    }

    private void DrawGlass(ImDrawListPtr drawList, Rect rect, float radius, float opacity)
    {
        if (opacity <= 0.001f)
        {
            return;
        }

        var scale = frameScale;
        switch (frameSurface)
        {
            case FaceSurface.Wallpaper:
                var snapshot = WallpaperBackdrop.Snapshot();
                var library = Plugin.Wallpapers;
                var aspect = frameScreen.Height > 0f ? frameScreen.Width / frameScreen.Height : 0.5f;
                WallpaperRenderer.RecordBackdrop(frameScreen, library.Resolve(frameTheme.LightWallpaperId),
                    library.Resolve(frameTheme.DarkWallpaperId), aspect, library.ThemeDarkness);
                Material.LiquidGlass(drawList, rect.Min, rect.Max, radius, scale, GlassTone.Dark, 0f, opacity);
                WallpaperBackdrop.Restore(snapshot);
                return;
            case FaceSurface.Map:
                Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                    ImGui.GetColorU32(new Vector4(0.06f, 0.07f, 0.09f, 0.72f * opacity)));
                Material.GlassRim(drawList, rect.Min, rect.Max, radius, scale, GlassTone.Dark, opacity);
                return;
            default:
                var tone = Palette.Luminance(frameTheme.ScreenBase) >= 0.5f ? GlassTone.Light : GlassTone.Dark;
                Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                    ImGui.GetColorU32(Palette.WithAlpha(frameInk.Strong, 0.08f * opacity)));
                Material.GlassRim(drawList, rect.Min, rect.Max, radius, scale, tone, opacity);
                return;
        }
    }

    private void StepState(float delta, bool interactive, bool bodyHovered, in CallView view)
    {
        if (pulseRemaining > 0f)
        {
            pulseRemaining = MathF.Max(0f, pulseRemaining - delta);
        }

        var callActive = view.State is CallState.Dialing or CallState.Connecting or CallState.Active;
        liveCall = callActive && layout.IsEnabled(MinimizedPart.Calls);
        liveMusic = layout.IsEnabled(MinimizedPart.NowPlaying) && (playback.IsActive || ReadPcMusic());
        RefreshPages();
        StepSlot(delta);
        island.Step(liveCall && liveMusic ? 1f : 0f, Motion.Island, delta);
        badge.Step(badgeAppId is null ? 0f : 1f, Motion.Appear, delta);
        dnd.Step(configuration.DoNotDisturb ? 1f : 0f, Motion.Appear, delta);
        AdvanceCard(delta, bodyHovered);
        hover.Step(interactive && bodyHovered ? 1f : 0f, Motion.HoverLift, delta);
        pagePosition.Step(pageIndex, Motion.PageSettle, delta);
        mapSpan.Step(MinimizedShapes.MapSpan(configuration.MinimizedMapZoom), Motion.PageSettle, delta);
        if (zoomDirty && clock >= zoomSaveDue)
        {
            zoomDirty = false;
            configuration.Save();
        }
    }

    private void StepSlot(float delta)
    {
        var target = TargetMode();
        slotPresence.Step(target == SlotMode.None ? 0f : 1f, Motion.Appear, delta);
        if (target == displayedMode)
        {
            contentFade.Step(1f, Motion.Appear, delta);
            return;
        }

        contentFade.Step(0f, Motion.Appear, delta);
        if (contentFade.Value > SwapThreshold && slotPresence.Value > SwapThreshold)
        {
            return;
        }

        displayedMode = target;
        contentFade.SnapTo(0f);
    }

    private SlotMode TargetMode()
    {
        if (liveCall)
        {
            return SlotMode.Call;
        }

        if (liveMusic)
        {
            return ShowsPcMusic() ? SlotMode.PcMusic : SlotMode.Music;
        }

        return pageCount > 0 ? SlotMode.Pages : SlotMode.None;
    }

    private void RefreshPages()
    {
        if (pagesRevision == layout.Revision)
        {
            return;
        }

        pagesRevision = layout.Revision;
        pageCount = 0;
        var slots = layout.Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (slot.Enabled && MinimizedParts.IsPage(slot.Part))
            {
                pageParts[pageCount++] = slot.Part;
            }
        }

        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, pageCount - 1));
        pagePosition.SnapTo(pageIndex);
    }

    private void AdvanceCard(float delta, bool bodyHovered)
    {
        if (cardNotification is null)
        {
            card.SnapTo(0f);
            if (queuedCards.Count > 0)
            {
                BeginCard(queuedCards.Dequeue());
            }

            return;
        }

        if (!cardDismissed)
        {
            card.Step(1f, Motion.Island, delta);
            if (!bodyHovered)
            {
                cardElapsed += delta;
            }

            if (cardElapsed >= CardHoldSeconds)
            {
                cardDismissed = true;
            }

            return;
        }

        card.Step(0f, Motion.Island, delta);
        if (card.Value > 0.02f)
        {
            return;
        }

        card.SnapTo(0f);
        cardNotification = null;
        cardDismissed = false;
    }

    private void ApplyMusicControl(MinimizedControl control)
    {
        switch (control)
        {
            case MinimizedControl.Previous:
                playback.Previous();
                break;
            case MinimizedControl.Next:
                playback.Next();
                break;
            case MinimizedControl.PlayPause:
                playback.TogglePlayPause();
                break;
        }
    }

    private void ApplyCallControl(MinimizedControl control)
    {
        if (control == MinimizedControl.ToggleMute)
        {
            calls.ToggleMute();
        }
        else if (control == MinimizedControl.Hangup)
        {
            calls.Hangup();
        }
    }

    private bool HandleGesture(Rect body, float scale, bool bodyHovered, bool hoveredControl)
    {
        if (bodyHovered && !hoveredControl)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!pressed && bodyHovered && !hoveredControl && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressed = true;
            dragging = false;
            pressOrigin = ImGui.GetMousePos();
        }

        var expandRequested = false;
        if (pressed)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetMousePos();
                if (!dragging && (mouse - pressOrigin).Length() > DragSlop * scale)
                {
                    dragging = true;
                }

                if (dragging)
                {
                    dragDelta += ImGui.GetIO().MouseDelta;
                }
            }
            else
            {
                if (dragging)
                {
                    dragReleased = true;
                }
                else if (bodyHovered)
                {
                    Tap();
                    expandRequested = true;
                }

                pressed = false;
                dragging = false;
            }
        }

        if (!pressed && bodyHovered && !hoveredControl && !bannerHovered && !LiveSlotHovered)
        {
            var viewport = ImGui.GetMainViewport();
            var side = body.Max.Y + TooltipClearance * scale > viewport.Pos.Y + viewport.Size.Y
                ? HoverLabelSide.Above
                : HoverLabelSide.Below;
            HoverTooltip.Show("minimized.phone", body, Loc.T(L.Plugin.MinimizedHint), side);
        }

        return expandRequested;
    }

    private bool LiveSlotHovered => slotHovered && displayedMode is SlotMode.Music or SlotMode.PcMusic or SlotMode.Call;

    private void Tap()
    {
        if (bannerHovered && cardNotification is { } notification && !cardDismissed)
        {
            router.Open(notification);
            cardDismissed = true;
            queuedCards.Clear();
            return;
        }

        if (!slotHovered)
        {
            return;
        }

        switch (displayedMode)
        {
            case SlotMode.Music:
            case SlotMode.PcMusic:
                navigation.Open(MusicAppId);
                break;
            case SlotMode.Call:
                calls.RequestCallScreen();
                navigation.Open(CallAppId);
                break;
        }
    }

    private void RefreshText(float scale)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == textFrame)
        {
            return;
        }

        textFrame = frame;
        var now = DateTime.Now;
        var minuteKey = now.Hour * 60 + now.Minute;
        var cultureChanged = !ReferenceEquals(textCulture, Loc.Culture);
        var timeChanged = minuteKey != timeKey || timeFormat != TimeText.FormatVersion || cultureChanged;
        if (timeChanged)
        {
            timeKey = minuteKey;
            timeFormat = TimeText.FormatVersion;
            timeLabel = TimeText.HourLabel(now.Hour) + ":" + TimeText.MinuteLabel(now.Minute);
        }

        var textWidth = MinimizedShapes.BodyWidth * scale -
                        ChassisGeometry.PuckBand(MinimizedShapes.BodyWidth * scale, CaseKind) -
                        HeroInset * 2f * scale;
        var dayKey = now.Year * 400 + now.DayOfYear;
        var widthChanged = MathF.Abs(textWidth - measuredWidth) > 0.5f;
        if (dayKey != dateKey || cultureChanged || widthChanged)
        {
            dateKey = dayKey;
            dateLabel = now.ToString("dddd d", Loc.Culture);
            if (Typography.Measure(dateLabel, MinimizedPhoneRenderer.DateStyle()).X > textWidth)
            {
                dateLabel = now.ToString("ddd d", Loc.Culture);
            }

            dateSize = Typography.Measure(dateLabel, MinimizedPhoneRenderer.DateStyle());
        }

        textCulture = Loc.Culture;
        if (!timeChanged && !widthChanged)
        {
            return;
        }

        measuredWidth = textWidth;
        clockScale = Typography.FitScale(timeLabel, textWidth, TextScale(ClockMaxScale), TextScale(ClockMinScale),
            FontWeight.Bold);
        mapClockScale = Typography.FitScale(timeLabel, textWidth, TextScale(MapClockMaxScale),
            TextScale(MapClockMinScale), FontWeight.Bold);
    }

    private string DurationLabel(in CallView view)
    {
        if (view.State != CallState.Active)
        {
            durationSeconds = -1;
            return CallStatusText.Label(view);
        }

        if (view.Seconds != durationSeconds || !view.Connected)
        {
            durationSeconds = view.Seconds;
            durationLabel = CallStatusText.Label(view);
        }

        return durationLabel;
    }

    private static float TextScale(float scale) => UiScale.MinimizedText(scale);

    private void RefreshBadge()
    {
        if (!configuration.IsAppBadgeEnabled(NotificationChannels.NotificationsAppId))
        {
            countValue = 0;
            countLabel = string.Empty;
            badgeAppId = null;
            return;
        }

        var unread = notifications.UnreadCount;
        if (unread != countValue)
        {
            countValue = unread;
            countLabel = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        }

        var recent = notifications.Recent;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            var notification = recent[index];
            if (notification.Read)
            {
                continue;
            }

            badgeAppId = notification.AppId;
            return;
        }

        badgeAppId = null;
    }

    private void OnPresented(PhoneNotification notification)
    {
        if (!IsShowing || !layout.IsEnabled(MinimizedPart.Alerts))
        {
            return;
        }

        if (cardNotification is { } showing && !cardDismissed && showing.StackKey == notification.StackKey)
        {
            cardNotification = notification;
            cardElapsed = 0f;
            return;
        }

        RemoveQueuedGroup(notification.StackKey);
        if (queuedCards.Count >= MaxQueuedCards)
        {
            return;
        }

        if (cardNotification is null)
        {
            BeginCard(notification);
            return;
        }

        queuedCards.Enqueue(notification);
    }

    private void RemoveQueuedGroup(string stackKey)
    {
        var count = queuedCards.Count;
        for (var index = 0; index < count; index++)
        {
            var queued = queuedCards.Dequeue();
            if (queued.StackKey != stackKey)
            {
                queuedCards.Enqueue(queued);
            }
        }
    }

    private void BeginCard(PhoneNotification notification)
    {
        cardNotification = notification;
        cardDismissed = false;
        cardElapsed = 0f;
        card.SnapTo(0f);
    }

    private void OnVibration(PhoneNotification notification)
    {
        if (!IsShowing)
        {
            return;
        }

        pulseRemaining = PulseSeconds;
        pulseAccent = notification.Accent;
    }

    public void Dispose()
    {
        if (zoomDirty)
        {
            zoomDirty = false;
            configuration.Save();
        }

        notifications.Changed -= RefreshBadge;
        notifications.Presented -= OnPresented;
        notifications.Vibration -= OnVibration;
        configuration.BadgeSettingsChanged -= RefreshBadge;
    }
}
