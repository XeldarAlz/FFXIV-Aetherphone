using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum MinimizedAction : byte
{
    None,
    Expand,
    Close,
}

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

internal sealed class MinimizedPhone : IDisposable
{
    public const float BodyWidth = 82f;
    private const float MinBodyHeight = 156f;
    private const float TopPadding = 10f;
    private const float BottomPadding = 11f;
    private const float SidePadding = 4f;
    private const float SectionGap = 9f;
    private const float DateGap = 2f;
    private const float MusicHeight = 66f;
    private const float MusicExpandedHeight = 30f;
    private const float CallHeight = 32f;
    private const float CallExpandedHeight = 34f;
    private const float CardHeight = 60f;
    private const float BadgeHeight = 22f;
    private const float ClockMaxScale = 1.45f;
    private const float ClockMinScale = 0.95f;
    private const float DateScale = 0.72f;
    private const float HoldSeconds = 0.55f;
    private const float DragSlop = 5f;
    private const float CardHoldSeconds = 4.5f;
    private const float PulseSeconds = 0.8f;
    private const float TooltipClearance = 44f;
    private const int MaxQueuedCards = 3;
    private const float PresenceSmoothTime = 0.16f;
    private const float HoverSmoothTime = 0.12f;
    private const float ExpandSmoothTime = 0.17f;
    private const float CardSmoothTime = 0.20f;
    private const float HoldSmoothTime = 0.07f;
    private const float ControlThreshold = 0.6f;
    private const string MusicAppId = "music";
    private const string CallAppId = "message";
    private static readonly TimeSpan ShowingGrace = TimeSpan.FromSeconds(0.5);

    private readonly PlaybackHub playback;
    private readonly CallHub calls;
    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly INavigator navigation;
    private readonly Configuration configuration;
    private readonly MinimizedLayoutService layout;
    private readonly MinimizedFeed feed;
    private readonly Queue<PhoneNotification> queuedCards = new();
    private Spring hover;
    private Spring expand;
    private Spring musicPresence;
    private Spring callPresence;
    private Spring badge;
    private Spring dnd;
    private Spring card;
    private Spring hold;
    private PhoneNotification? cardNotification;
    private bool cardDismissed;
    private float cardElapsed;
    private float clock;
    private float pulseRemaining;
    private Vector4 pulseAccent;
    private bool pressed;
    private bool dragging;
    private bool holdFired;
    private float held;
    private Vector2 pressOrigin;
    private Vector2 dragDelta;
    private bool dragReleased;
    private bool musicHovered;
    private bool callHovered;
    private bool cardHovered;
    private bool controlHovered;
    private float drawnCardPresence;
    private string? badgeAppId;
    private Vector4 badgeAccent;
    private string countLabel = string.Empty;
    private int countValue = -1;
    private string durationLabel = string.Empty;
    private int durationSeconds = -1;
    private string timeLabel = string.Empty;
    private string meridiemLabel = string.Empty;
    private string dateLabel = string.Empty;
    private int timeKey = -1;
    private int timeFormat = -1;
    private int dateKey = -1;
    private int layoutRevision = -1;
    private bool meridiemInline;
    private CultureInfo? textCulture;
    private float clockScale;
    private Vector2 clockSize;
    private Vector2 dateSize;
    private int textFrame = -1;
    private DateTime lastInteractiveDrawUtc = DateTime.MinValue;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private CallView frameView;
    private float frameScale = 1f;
    private float frameAlpha = 1f;
    private float frameExpandEased;
    private bool frameInteractive;
    private bool frameBodyHovered;

    public MinimizedPhone(PhoneServices services, NotificationRouter router, INavigator navigation,
        MinimizedLayoutService layout)
    {
        playback = services.Playback;
        calls = services.Calls;
        notifications = services.Notifications;
        configuration = services.Configuration;
        this.router = router;
        this.navigation = navigation;
        this.layout = layout;
        feed = new MinimizedFeed(services.Weather, services.Coins, services.AethernetSession, services.Activity,
            services.GameData);
        notifications.Changed += RefreshBadge;
        notifications.Presented += OnPresented;
        notifications.Vibration += OnVibration;
        RefreshBadge();
    }

    public bool IsShowing => DateTime.UtcNow - lastInteractiveDrawUtc < ShowingGrace;

    public Vector2 Measure(float scale)
    {
        var band = ChassisGeometry.PuckBand(BodyWidth * scale);
        var height = MathF.Max(MinBodyHeight * scale - band, ContentHeight(scale)) + band;
        return new Vector2(MathF.Round(BodyWidth * scale), MathF.Round(height));
    }

    public static Vector2 IdleSize(float scale) =>
        new(MathF.Round(BodyWidth * scale), MathF.Round(MinBodyHeight * scale));

    public MinimizedDrag ConsumeDrag()
    {
        var result = new MinimizedDrag(dragDelta, dragReleased);
        dragDelta = Vector2.Zero;
        dragReleased = false;
        return result;
    }

    public MinimizedAction Draw(Rect body, PhoneTheme theme, float delta)
    {
        var scale = UiScale.Global;
        var geometry = ChassisGeometry.Puck(body);
        var dl = ImGui.GetForegroundDrawList();
        var lift = Math.Clamp(hover.Value, 0f, 1f);
        Elevation.Squircle(dl, geometry.Body.Min, geometry.Body.Max, geometry.BodyRadius, scale, 0.85f + 0.35f * lift);
        DeviceChrome.DrawShell(dl, geometry, scale, theme, 1f);
        return DrawFace(dl, geometry, theme, delta, true, 1f);
    }

    public MinimizedAction DrawFace(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneTheme theme, float delta,
        bool interactive, float alpha)
    {
        clock += delta;
        feed.Update(delta);
        if (interactive)
        {
            lastInteractiveDrawUtc = DateTime.UtcNow;
        }

        var scale = UiScale.Global;
        var body = geometry.Body;
        var bodyHovered = interactive && UiInteract.Hover(body.Min, body.Max);
        var view = calls.Snapshot();
        StepState(delta, interactive, bodyHovered, view);
        if (alpha <= 0.001f)
        {
            return MinimizedAction.None;
        }

        RefreshText(scale);
        frameTheme = theme;
        frameView = view;
        frameScale = scale;
        frameAlpha = alpha;
        frameExpandEased = Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f));
        frameInteractive = interactive;
        frameBodyHovered = bodyHovered;
        musicHovered = false;
        callHovered = false;
        cardHovered = false;
        controlHovered = false;
        drawnCardPresence = 0f;
        var screen = geometry.Screen;
        dl.PushClipRect(screen.Min, screen.Max, true);
        DrawParts(dl, screen, scale);
        var holdValue = Math.Clamp(hold.Value, 0f, 1f);
        if (holdValue > 0.005f)
        {
            MinimizedPhoneRenderer.DrawHoldSweep(dl, geometry, theme, holdValue * alpha, scale);
        }

        dl.PopClipRect();
        if (cardNotification is { } stroked && drawnCardPresence > 0.01f)
        {
            MinimizedPhoneRenderer.DrawCardStroke(dl, geometry, stroked.Accent, alpha * drawnCardPresence, cardHovered,
                scale);
        }

        if (pulseRemaining > 0f)
        {
            var strength = pulseRemaining / PulseSeconds;
            MinimizedPhoneRenderer.DrawPulse(dl, geometry, pulseAccent, strength * strength * alpha, scale);
        }

        if (!interactive)
        {
            return MinimizedAction.None;
        }

        return HandleGesture(body, scale, delta, bodyHovered, controlHovered);
    }

    private void DrawParts(ImDrawListPtr dl, Rect screen, float scale)
    {
        MeasureFlow(scale, frameExpandEased, out var flowHeight, out var lastIndex);
        var slack = MathF.Max(0f, screen.Height - (TopPadding + BottomPadding) * scale - flowHeight);
        var y = screen.Min.Y + TopPadding * scale;
        var slots = layout.Slots;
        var previous = MinimizedPart.Clock;
        var hasPrevious = false;
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (!slot.Enabled)
            {
                continue;
            }

            var presence = Presence(slot.Part);
            if (presence <= 0.01f)
            {
                continue;
            }

            if (hasPrevious)
            {
                y += Gap(previous, slot.Part) * scale * presence;
            }

            if (index == lastIndex && hasPrevious)
            {
                y += slack;
            }

            y = DrawPart(dl, screen, y, slot.Part, presence);
            previous = slot.Part;
            hasPrevious = true;
        }
    }

    private void MeasureFlow(float scale, float expandEased, out float flowHeight, out int lastIndex)
    {
        flowHeight = 0f;
        lastIndex = -1;
        var slots = layout.Slots;
        var previous = MinimizedPart.Clock;
        var hasPrevious = false;
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (!slot.Enabled)
            {
                continue;
            }

            var presence = Presence(slot.Part);
            if (presence <= 0.01f)
            {
                continue;
            }

            if (hasPrevious)
            {
                flowHeight += Gap(previous, slot.Part) * scale * presence;
            }

            flowHeight += PartHeight(slot.Part, scale, expandEased) * presence;
            previous = slot.Part;
            hasPrevious = true;
            lastIndex = index;
        }
    }

    private float DrawPart(ImDrawListPtr dl, Rect screen, float y, MinimizedPart part, float presence)
    {
        switch (part)
        {
            case MinimizedPart.Clock:
                MinimizedPhoneRenderer.DrawTime(dl, screen, y, timeLabel, meridiemInline ? meridiemLabel : string.Empty,
                    clockScale, clockSize, frameTheme, frameAlpha, frameScale);
                return y + clockSize.Y;
            case MinimizedPart.Date:
                MinimizedPhoneRenderer.DrawDateRow(dl, screen, y, meridiemInline ? string.Empty : meridiemLabel,
                    dateLabel, dateSize, frameTheme, frameAlpha, Math.Clamp(dnd.Value, 0f, 1f), frameScale);
                return y + dateSize.Y;
            case MinimizedPart.NowPlaying:
                return DrawMusic(dl, screen, y, presence);
            case MinimizedPart.Calls:
                return DrawCall(dl, screen, y, presence);
            case MinimizedPart.Alerts:
                return DrawCard(dl, screen, y, presence);
            case MinimizedPart.Badge:
                return DrawBadge(dl, screen, y, presence);
            default:
                return DrawWidget(dl, screen, y, part);
        }
    }

    private float DrawMusic(ImDrawListPtr dl, Rect screen, float y, float presence)
    {
        var scale = frameScale;
        var compactHeight = MusicHeight * scale;
        var expandedHeight = MusicExpandedHeight * scale * frameExpandEased;
        var section = SectionRect(screen, y, (compactHeight + expandedHeight) * presence);
        musicHovered = frameBodyHovered && presence > 0.9f && UiInteract.Hover(section.Min, section.Max);
        dl.PushClipRect(section.Min, section.Max, true);
        var compact = new Rect(section.Min, new Vector2(section.Max.X, y + compactHeight));
        var sectionAlpha = frameAlpha * presence;
        MinimizedPhoneRenderer.DrawMusicSection(dl, compact, playback, clock, sectionAlpha, scale, frameTheme);
        if (expandedHeight > 0.5f)
        {
            var row = new Rect(new Vector2(section.Min.X, compact.Max.Y),
                new Vector2(section.Max.X, compact.Max.Y + expandedHeight));
            var active = frameInteractive && frameExpandEased > ControlThreshold;
            var result = MinimizedPhoneRenderer.DrawMusicTransport(dl, row, playback, frameTheme,
                sectionAlpha * frameExpandEased, active, scale);
            ApplyMusicControl(result.Action);
            controlHovered |= result.Hovered;
        }

        dl.PopClipRect();
        return section.Max.Y;
    }

    private float DrawCall(ImDrawListPtr dl, Rect screen, float y, float presence)
    {
        var scale = frameScale;
        var compactHeight = CallHeight * scale;
        var expandedHeight = CallExpandedHeight * scale * frameExpandEased;
        var section = SectionRect(screen, y, (compactHeight + expandedHeight) * presence);
        callHovered = frameBodyHovered && presence > 0.9f && UiInteract.Hover(section.Min, section.Max);
        dl.PushClipRect(section.Min, section.Max, true);
        var compact = new Rect(section.Min, new Vector2(section.Max.X, y + compactHeight));
        var sectionAlpha = frameAlpha * presence;
        MinimizedPhoneRenderer.DrawCallSection(dl, compact, frameView, DurationLabel(frameView), clock, sectionAlpha,
            scale, frameTheme);
        if (expandedHeight > 0.5f)
        {
            var row = new Rect(new Vector2(section.Min.X, compact.Max.Y),
                new Vector2(section.Max.X, compact.Max.Y + expandedHeight));
            var active = frameInteractive && frameExpandEased > ControlThreshold;
            var result = MinimizedPhoneRenderer.DrawCallControls(dl, row, frameView, frameTheme,
                sectionAlpha * frameExpandEased, active, scale);
            ApplyCallControl(result.Action);
            controlHovered |= result.Hovered;
        }

        dl.PopClipRect();
        return section.Max.Y;
    }

    private float DrawCard(ImDrawListPtr dl, Rect screen, float y, float presence)
    {
        if (cardNotification is not { } notification)
        {
            return y;
        }

        var scale = frameScale;
        var section = SectionRect(screen, y, CardHeight * scale * presence);
        drawnCardPresence = presence;
        cardHovered = frameBodyHovered && presence > 0.9f && UiInteract.Hover(section.Min, section.Max);
        dl.PushClipRect(section.Min, section.Max, true);
        var full = new Rect(section.Min, new Vector2(section.Max.X, y + CardHeight * scale));
        MinimizedPhoneRenderer.DrawCardSection(dl, full, notification, frameTheme, frameAlpha * presence, scale);
        dl.PopClipRect();
        return section.Max.Y;
    }

    private float DrawBadge(ImDrawListPtr dl, Rect screen, float y, float presence)
    {
        if (badgeAppId is not { } appId)
        {
            return y;
        }

        var height = BadgeHeight * frameScale * presence;
        var center = new Vector2(screen.Center.X, y + height * 0.5f);
        MinimizedPhoneRenderer.DrawBadge(dl, center, appId, badgeAccent, countLabel, frameTheme,
            frameAlpha * presence, frameScale);
        return y + height;
    }

    private float DrawWidget(ImDrawListPtr dl, Rect screen, float y, MinimizedPart part)
    {
        var height = MinimizedWidgetRenderer.Height(part, frameScale);
        var section = SectionRect(screen, y, height);
        MinimizedWidgetRenderer.Draw(dl, section, part, feed, configuration, frameTheme, frameAlpha, frameScale);
        return section.Max.Y;
    }

    private Rect SectionRect(Rect screen, float top, float height) =>
        new(new Vector2(screen.Min.X + SidePadding * frameScale, top),
            new Vector2(screen.Max.X - SidePadding * frameScale, top + height));

    private void StepState(float delta, bool interactive, bool bodyHovered, in CallView view)
    {
        if (pulseRemaining > 0f)
        {
            pulseRemaining = MathF.Max(0f, pulseRemaining - delta);
        }

        var callActive = view.State is CallState.Dialing or CallState.Connecting or CallState.Active;
        var callShown = callActive && layout.IsEnabled(MinimizedPart.Calls);
        var musicShown = playback.IsActive && layout.IsEnabled(MinimizedPart.NowPlaying);
        musicPresence.Step(musicShown ? 1f : 0f, PresenceSmoothTime, delta);
        callPresence.Step(callShown ? 1f : 0f, PresenceSmoothTime, delta);
        badge.Step(badgeAppId is null ? 0f : 1f, PresenceSmoothTime, delta);
        dnd.Step(configuration.DoNotDisturb ? 1f : 0f, PresenceSmoothTime, delta);
        AdvanceCard(delta, bodyHovered);
        hover.Step(interactive && bodyHovered ? 1f : 0f, HoverSmoothTime, delta);
        var wantsExpand = interactive && bodyHovered && (musicShown || callShown) && !dragging;
        expand.Step(wantsExpand ? 1f : 0f, ExpandSmoothTime, delta);
        var holdTarget = pressed && !dragging ? Math.Clamp(held / HoldSeconds, 0f, 1f) : 0f;
        hold.Step(holdTarget, HoldSmoothTime, delta);
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
            card.Step(1f, CardSmoothTime, delta);
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

        card.Step(0f, CardSmoothTime, delta);
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

    private MinimizedAction HandleGesture(Rect body, float scale, float delta, bool bodyHovered, bool hoveredControl)
    {
        if (bodyHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!pressed && bodyHovered && !hoveredControl && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressed = true;
            dragging = false;
            holdFired = false;
            held = 0f;
            pressOrigin = ImGui.GetMousePos();
        }

        var action = MinimizedAction.None;
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
                else
                {
                    held += delta;
                    if (held >= HoldSeconds && !holdFired)
                    {
                        holdFired = true;
                        action = MinimizedAction.Close;
                    }
                }
            }
            else
            {
                if (dragging)
                {
                    dragReleased = true;
                }
                else if (!holdFired && bodyHovered)
                {
                    action = Tap();
                }

                pressed = false;
                dragging = false;
                held = 0f;
            }
        }

        if (!pressed && bodyHovered && !musicHovered && !callHovered && !cardHovered && expand.Value < 0.5f)
        {
            var viewport = ImGui.GetMainViewport();
            var side = body.Max.Y + TooltipClearance * scale > viewport.Pos.Y + viewport.Size.Y
                ? HoverLabelSide.Above
                : HoverLabelSide.Below;
            HoverTooltip.Show("minimized.phone", body, Loc.T(L.Plugin.MinimizedHint), side);
        }

        return action;
    }

    private MinimizedAction Tap()
    {
        if (cardHovered && cardNotification is { } notification && !cardDismissed)
        {
            router.Open(notification);
            cardDismissed = true;
            queuedCards.Clear();
            return MinimizedAction.Expand;
        }

        if (musicHovered)
        {
            navigation.Open(MusicAppId);
        }
        else if (callHovered)
        {
            calls.RequestCallScreen();
            navigation.Open(CallAppId);
        }

        return MinimizedAction.Expand;
    }

    private float Presence(MinimizedPart part) => part switch
    {
        MinimizedPart.NowPlaying => Math.Clamp(musicPresence.Value, 0f, 1f),
        MinimizedPart.Calls => Math.Clamp(callPresence.Value, 0f, 1f),
        MinimizedPart.Alerts => Easing.SmoothStep(Math.Clamp(card.Value, 0f, 1f)),
        MinimizedPart.Badge => Math.Clamp(badge.Value, 0f, 1f),
        _ => 1f,
    };

    private float PartHeight(MinimizedPart part, float scale, float expandEased) => part switch
    {
        MinimizedPart.Clock => clockSize.Y,
        MinimizedPart.Date => dateSize.Y,
        MinimizedPart.NowPlaying => (MusicHeight + MusicExpandedHeight * expandEased) * scale,
        MinimizedPart.Calls => (CallHeight + CallExpandedHeight * expandEased) * scale,
        MinimizedPart.Alerts => CardHeight * scale,
        MinimizedPart.Badge => BadgeHeight * scale,
        _ => MinimizedWidgetRenderer.Height(part, scale),
    };

    private static float Gap(MinimizedPart previous, MinimizedPart part) =>
        previous == MinimizedPart.Clock && part == MinimizedPart.Date ? DateGap : SectionGap;

    private float ContentHeight(float scale)
    {
        RefreshText(scale);
        MeasureFlow(scale, Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f)), out var flowHeight, out _);
        return (TopPadding + BottomPadding) * scale + flowHeight;
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
        if (minuteKey != timeKey || timeFormat != TimeText.FormatVersion || !ReferenceEquals(textCulture, Loc.Culture))
        {
            timeKey = minuteKey;
            timeFormat = TimeText.FormatVersion;
            timeLabel = TimeText.HourLabel(now.Hour) + ":" + TimeText.MinuteLabel(now.Minute);
            meridiemLabel = TimeText.Use24Hour ? string.Empty : TimeText.MeridiemLabel(now.Hour >= 12);
        }

        var dayKey = now.Year * 400 + now.DayOfYear;
        if (dayKey != dateKey || !ReferenceEquals(textCulture, Loc.Culture))
        {
            dateKey = dayKey;
            dateLabel = now.ToString("ddd d", Loc.Culture);
        }

        if (layoutRevision != layout.Revision)
        {
            layoutRevision = layout.Revision;
            meridiemInline = !layout.IsEnabled(MinimizedPart.Date);
        }

        textCulture = Loc.Culture;
        var textWidth = BodyWidth * scale - ChassisGeometry.PuckBand(BodyWidth * scale) - SidePadding * 2f * scale;
        var timeWidth = textWidth;
        if (meridiemInline && meridiemLabel.Length > 0)
        {
            timeWidth -= MinimizedPhoneRenderer.InlineMeridiemWidth(meridiemLabel, scale);
        }

        clockScale = Typography.FitScale(timeLabel, timeWidth, TextScale(ClockMaxScale), TextScale(ClockMinScale),
            FontWeight.Bold);
        clockSize = Typography.Measure(timeLabel, clockScale, FontWeight.Bold);
        dateSize = Typography.Measure(dateLabel, TextScale(DateScale), FontWeight.Regular);
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

    private static float TextScale(float scale) => scale / UiScale.Phone;

    private void RefreshBadge()
    {
        if (!Plugin.Cfg.IsAppBadgeEnabled("notifications"))
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
            badgeAccent = notification.Accent;
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
        notifications.Changed -= RefreshBadge;
        notifications.Presented -= OnPresented;
        notifications.Vibration -= OnVibration;
    }
}
