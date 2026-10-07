using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Timers;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed partial class DynamicIsland
{
    private const ImGuiWindowFlags IslandFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private const float PulseWidthScale = 1.12f;
    private const float PulseHoldSeconds = 0.14f;
    private const float CompactHeight = 36f;
    private const float CompactPadX = 8f;
    private const float CompactBubbleInset = 4f;
    private const float CompactTrailingInset = 11f;
    private const float CompactTrailingGap = 8f;
    private const float BubbleGap = 7f;
    private const float ExpandedHeight = 96f;
    private const float ExpandedHalfWidth = 150f;
    private const float ExpandedSideInset = 14f;
    private const float ExpandedLift = 2f;
    private const float CardPadX = 18f;
    private const float CardIconRadius = 22f;
    private const float CardControlRadius = 20f;
    private const float CardTextGap = 12f;
    private const float CardLineGap = 3f;
    private const float IconBubbleAlpha = 0.18f;
    private const float ControlFillAlpha = 0.14f;
    private const float CompactRingAlpha = 0.16f;
    private const float ControlThreshold = 0.6f;
    private const float CallPulseSpeed = 3f;
    private const float NoticeHoldSeconds = 1.8f;
    private const float NoticeHalfWidth = 104f;

    private static readonly Vector4 MusicAccent = AppAccents.For("music");
    private static readonly Vector4 SessionAccent = AppAccents.For("aetherstream");
    private static readonly Vector4 MusterAccent = AppAccents.For("muster");
    private static readonly Vector4 FishingAccent = AppAccents.For("fishing");
    private static readonly Vector4 CallAccent = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 TimerAccent = new(1.00f, 0.62f, 0.18f, 1f);
    private static readonly Vector4 GameTimerAccent = AppAccents.For("timers");
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);
    private static readonly Vector4 FocusAccent = new(0.42f, 0.40f, 0.95f, 1f);
    private static readonly Vector4 QuietInk = new(0.64f, 0.64f, 0.68f, 1f);

    private readonly PlaybackHub playback;
    private readonly CallHub calls;
    private readonly Configuration configuration;
    private readonly AppInstaller installer;
    private readonly VideoSuite? video;
    private readonly MusterStore? musters;
    private readonly MusterLauncher? musterLauncher;
    private readonly GameTimers? gameTimers;
    private RunningTimer upcomingGameTimer;
    private long gameTimerCachedSeconds = -1;
    private string gameTimerCachedText = string.Empty;
    private readonly FishingAlerts? fishing;
    private int timerCachedSeconds = -1;
    private string timerCachedText = string.Empty;
    private int musterCachedMinutes = -1;
    private string musterCachedCountdown = string.Empty;
    private string musterCachedStatus = string.Empty;
    private long fishingCachedSeconds = -1;
    private FishingIslandKind fishingCachedKind;
    private bool fishingCachedOpen;
    private string fishingCachedCountdown = string.Empty;
    private string fishingCachedStatus = string.Empty;
    private int viewersCachedCount = -1;
    private string viewersCachedText = string.Empty;
    private Spring presence;
    private Spring split;
    private Spring expand;
    private Spring pulse;
    private Spring noticeWidth;
    private double noticeUntil = -1d;
    private bool noticeEnabled;
    private IslandNotice notice;
    private float clock;
    private float pulseUntil = -1f;
    private bool expanded;
    private bool lastExpanded;
    private int expandedFrame = -1;
    private IslandActivity shownKind = IslandActivity.None;
    private MusterDto? upcomingMuster;
    private Rect lastBounds;
    private Rect lastBubble;
    private bool lastBubbleVisible;

    public DynamicIsland(PlaybackHub playback, CallHub calls, Configuration configuration, AppInstaller installer,
        VideoSuite? video, MusterStore? musters, MusterLauncher? musterLauncher, PcMediaSource? pcMedia,
        GameTimers? gameTimers = null, FishingAlerts? fishing = null)
    {
        this.installer = installer;
        this.gameTimers = gameTimers;
        this.fishing = fishing;
        this.playback = playback;
        this.calls = calls;
        this.configuration = configuration;
        this.video = video;
        this.musters = musters;
        this.musterLauncher = musterLauncher;
        this.pcMedia = pcMedia;
    }

    public void Pulse()
    {
        if (expanded || expand.Value > 0.05f)
        {
            return;
        }

        pulseUntil = clock + PulseHoldSeconds;
    }

    public void Announce(IslandNotice kind, bool enabled)
    {
        notice = kind;
        noticeEnabled = enabled;
        noticeUntil = ImGui.GetTime() + NoticeHoldSeconds;
        expanded = false;
    }

    public bool CapturesPointer()
    {
        if (presence.Value < 0.05f)
        {
            return false;
        }

        if (lastExpanded)
        {
            return true;
        }

        if (UiInteract.Hover(lastBounds.Min, lastBounds.Max))
        {
            return true;
        }

        return lastBubbleVisible && UiInteract.Hover(lastBubble.Min, lastBubble.Max);
    }

    public void Draw(Rect screen, PhoneTheme theme, INavigator navigation, string? foregroundAppId)
    {
        var view = calls.Snapshot();
        var signals = ReadSignals(view);
        var selected = IslandActivities.Select(signals);
        var primary = selected != IslandActivity.Call && ImGui.GetTime() < noticeUntil
            ? IslandActivity.Notice
            : selected;
        if (primary != IslandActivity.None)
        {
            shownKind = primary;
        }
        else
        {
            expanded = false;
        }

        var delta = FrameClock.Delta;
        clock += delta;
        presence.Step(primary == IslandActivity.None ? 0f : 1f, Motion.Appear, delta);
        split.Step(signals.Playback && primary != IslandActivity.Playback ? 1f : 0f, Motion.Island, delta);
        pulse.Step(clock < pulseUntil ? 1f : 0f, Motion.Appear, delta);
        noticeWidth.Step(shownKind == IslandActivity.Notice ? 1f : 0f, Motion.Island, delta);
        var presenceValue = Math.Clamp(presence.Value, 0f, 1f);
        var pulseValue = Math.Clamp(pulse.Value, 0f, 1f);
        if (primary == IslandActivity.None && presenceValue < 0.02f && pulseValue < 0.01f)
        {
            expand.SnapTo(0f);
            lastExpanded = false;
            lastBounds = StatusBar.BaseIsland(screen);
            lastBubbleVisible = false;
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##dynamicIsland", screen.Size, false, IslandFlags))
        {
            DrawContent(screen, theme, navigation, view, presenceValue, pulseValue, delta, foregroundAppId);
        }
    }

    private IslandSignals ReadSignals(in CallView view)
    {
        var call = view.State is CallState.Ringing or CallState.Dialing or CallState.Connecting or CallState.Active;
        var session = Allows(IslandActivity.Session) && video is { } suite && suite.WatchAlong.InParty;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        upcomingMuster = Allows(IslandActivity.Muster) && musters is { } store
            ? IslandActivities.SoonestMuster(store.GoingMusters, store.Mine, nowUnix)
            : null;
        upcomingGameTimer = Allows(IslandActivity.GameTimer) && gameTimers is { Enabled: true } timers
            ? TimerBoard.Tally(timers.Characters, timers.Workshops, nowUnix).Soonest
            : default;
        var music = Allows(IslandActivity.Playback);
        var phonePlayback = music && playback.IsActive;
        var pcMediaLive = music && ReadPcMedia(call || session || phonePlayback);
        var timer = Allows(IslandActivity.Timer) && TimerRemainingSeconds() > 0;
        var fishingLive = Allows(IslandActivity.Fishing) && fishing is { Island.Kind: not FishingIslandKind.None };
        return new IslandSignals(call, session, phonePlayback, timer, upcomingMuster is not null, pcMediaLive,
            TimerBoard.InIslandWindow(upcomingGameTimer, nowUnix), fishingLive);
    }

    private bool Allows(IslandActivity activity)
    {
        var appId = IslandActivities.OwnerAppId(activity);
        return installer.IsInstalled(appId) && configuration.IsIslandEnabled(appId);
    }

    private void DrawContent(Rect screen, PhoneTheme theme, INavigator navigation, in CallView view,
        float presenceValue, float pulseValue, float delta, string? foregroundAppId)
    {
        var scale = UiScale.Current;
        var rest = StatusBar.BaseIsland(screen);
        var compact = LerpRect(CompactBounds(rest, scale), NoticeBounds(screen, rest, scale),
            Math.Clamp(noticeWidth.Value, 0f, 1f));
        var card = ExpandedBounds(screen, rest, scale);
        var morphed = LerpRect(rest, compact, presenceValue);
        var suppress = shownKind != IslandActivity.Call &&
                       string.Equals(IslandActivities.OwnerAppId(shownKind), foregroundAppId, StringComparison.Ordinal);
        if (suppress)
        {
            expanded = false;
        }

        expand.Step(expanded ? 1f : 0f, Motion.Island, delta);
        var expandAmount = Math.Clamp(expand.Value, 0f, 1f);
        var bounds = Swell(LerpRect(morphed, card, expandAmount), pulseValue * (1f - expandAmount));
        lastBounds = bounds;
        lastExpanded = expandAmount > 0.5f;
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        var accent = AccentFor(shownKind);
        var compactAlpha = Math.Clamp(presenceValue * 1.6f - 0.6f, 0f, 1f) * (1f - expandAmount);
        var drawList = ImGui.GetWindowDrawList();
        DrawBubble(drawList, theme, navigation, bounds, scale, expandAmount);
        var rounding = bounds.Height * 0.5f;
        if (expandAmount > 0.02f)
        {
            Elevation.Draw(drawList, bounds.Min, bounds.Max, rounding, scale, 5f + 6f * expandAmount, 3f,
                0.24f * expandAmount);
        }

        Squircle.Fill(drawList, bounds.Min, bounds.Max, rounding, ImGui.GetColorU32(theme.Glass));
        if (expandAmount > 0.01f)
        {
            Material.LiquidGlass(drawList, bounds.Min, bounds.Max, rounding, scale, GlassTone.Dark, 0f, expandAmount);
        }

        if (compactAlpha > 0.01f)
        {
            Squircle.Stroke(drawList, bounds.Min, bounds.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(accent, CompactRingAlpha * compactAlpha)), 1.5f * scale);
        }

        DrawCompact(drawList, bounds, scale, view, accent, compactAlpha);
        var overControl = DrawExpanded(drawList, bounds, scale, theme, navigation, view, accent, expandAmount);
        HandleTap(navigation, bounds, hovered, overControl, expandAmount, presenceValue, suppress);
    }

    private void HandleTap(INavigator navigation, Rect bounds, bool hovered, bool overControl, float expandAmount,
        float presenceValue, bool suppress)
    {
        if (expandAmount < 0.5f)
        {
            if (suppress || shownKind == IslandActivity.Notice || !hovered || presenceValue < ControlThreshold)
            {
                return;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (!UiInteract.Click(bounds.Min, bounds.Max, hovered))
            {
                return;
            }

            expanded = true;
            expandedFrame = ImGui.GetFrameCount();
            UiFeedback.Play(UiSound.IslandExpand);
            return;
        }

        if (hovered && !overControl)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
            {
                OpenOwner(navigation);
                return;
            }
        }

        if (ImGui.GetFrameCount() != expandedFrame && UiInteract.ClickedOutside(bounds.Min, bounds.Max, false))
        {
            expanded = false;
            UiFeedback.Play(UiSound.IslandCollapse);
        }
    }

    private void OpenOwner(INavigator navigation)
    {
        expanded = false;
        switch (shownKind)
        {
            case IslandActivity.Call:
                calls.RequestCallScreen();
                break;
            case IslandActivity.Muster:
                if (upcomingMuster is { } muster)
                {
                    musterLauncher?.RequestDetail(muster.Id);
                }

                break;
        }

        var appId = IslandActivities.OwnerAppId(shownKind);
        if (appId.Length > 0)
        {
            navigation.Open(appId);
        }
    }

    private static Vector4 AccentFor(IslandActivity activity)
    {
        switch (activity)
        {
            case IslandActivity.Call:
                return CallAccent;
            case IslandActivity.Session:
                return SessionAccent;
            case IslandActivity.Timer:
                return TimerAccent;
            case IslandActivity.Muster:
                return MusterAccent;
            case IslandActivity.GameTimer:
                return GameTimerAccent;
            case IslandActivity.Fishing:
                return FishingAccent;
            case IslandActivity.Notice:
                return FocusAccent;
            default:
                return MusicAccent;
        }
    }

    private static string CallStatus(in CallView view) =>
        view.State == CallState.Ringing ? Loc.T(L.Phone.IncomingCallBody) : CallStatusText.Label(view);

    private int TimerRemainingSeconds()
    {
        if (configuration.TimerEndsAtUtc is not { } endsAt)
        {
            return 0;
        }

        return (int)Math.Ceiling((endsAt - DateTime.UtcNow).TotalSeconds);
    }

    private string TimerText(int remaining)
    {
        if (remaining != timerCachedSeconds)
        {
            timerCachedSeconds = remaining;
            timerCachedText = TimeText.MinutesSeconds(remaining);
        }

        return timerCachedText;
    }

    private float TimerFraction(int remaining) => configuration.TimerDurationSeconds > 0
        ? Math.Clamp(remaining / (float)configuration.TimerDurationSeconds, 0f, 1f)
        : 0f;

    private string MusterCountdown(MusterDto muster, bool compact)
    {
        var remaining = muster.StartsAtUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (remaining <= 0)
        {
            return Loc.T(L.Muster.NoticeStartingNow);
        }

        var minutes = (int)((remaining + 59) / 60);
        if (minutes != musterCachedMinutes)
        {
            musterCachedMinutes = minutes;
            musterCachedCountdown = MusterText.Span(minutes * 60L);
            musterCachedStatus = Loc.T(L.Island.StartsIn, musterCachedCountdown);
        }

        return compact ? musterCachedCountdown : musterCachedStatus;
    }

    private static FontAwesomeIcon GameTimerIcon(in RunningTimer timer)
    {
        if (!timer.Voyage)
        {
            return FontAwesomeIcon.Briefcase;
        }

        return timer.Airship ? FontAwesomeIcon.Plane : FontAwesomeIcon.Anchor;
    }

    private string GameTimerText(in RunningTimer timer)
    {
        var remaining = Math.Max(0L, timer.EndUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (remaining != gameTimerCachedSeconds)
        {
            gameTimerCachedSeconds = remaining;
            gameTimerCachedText = TimeText.MinutesSeconds((int)remaining);
        }

        return gameTimerCachedText;
    }

    private static FontAwesomeIcon FishingIcon(in FishingIslandStatus status) =>
        status.Kind == FishingIslandKind.Voyage ? FontAwesomeIcon.Anchor : FontAwesomeIcon.Fish;

    private string FishingCountdown(in FishingIslandStatus status, bool compact)
    {
        var remaining = Math.Max(0L, status.TargetUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (remaining != fishingCachedSeconds || status.Kind != fishingCachedKind || status.Open != fishingCachedOpen)
        {
            fishingCachedSeconds = remaining;
            fishingCachedKind = status.Kind;
            fishingCachedOpen = status.Open;
            fishingCachedCountdown = FishingClock.Countdown(remaining);
            fishingCachedStatus = FishingClock.IslandStatus(status.Kind, status.Open, fishingCachedCountdown);
        }

        return compact ? fishingCachedCountdown : fishingCachedStatus;
    }

    private string ViewersText(int count)
    {
        if (count != viewersCachedCount)
        {
            viewersCachedCount = count;
            viewersCachedText = Loc.Plural(L.Island.Viewers, count);
        }

        return viewersCachedText;
    }

    private static string SessionTitle(VideoSuite suite)
    {
        var entry = suite.WatchAlong.IsViewing ? suite.WatchAlong.ViewingEntry : suite.Queue.Current;
        return entry is { Title.Length: > 0 } ? entry.Title : Loc.T(L.Apps.AetherStream);
    }

    private string SessionStatus(VideoSuite suite)
    {
        if (suite.Player.State == VideoPlaybackState.Paused)
        {
            return Loc.T(L.Music.Paused);
        }

        var count = suite.WatchAlong.Roster.Count;
        if (count > 1)
        {
            return ViewersText(count);
        }

        return suite.WatchAlong.IsHosting ? Loc.T(L.Island.Hosting) : Loc.T(L.Island.Watching);
    }

    private void DrawCompact(ImDrawListPtr drawList, Rect bounds, float scale, in CallView view, Vector4 accent,
        float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var bubbleRadius = bounds.Height * 0.5f - CompactBubbleInset * scale;
        var bubbleCenter = new Vector2(bounds.Min.X + CompactBubbleInset * scale + bubbleRadius, bounds.Center.Y);
        var trailingRight = bounds.Max.X - CompactTrailingInset * scale;
        var trailingMaxWidth = MathF.Max(1f, trailingRight - (bubbleCenter.X + bubbleRadius + CompactTrailingGap * scale));
        switch (shownKind)
        {
            case IslandActivity.Call:
                DrawCallBubble(drawList, bubbleCenter, bubbleRadius, scale, alpha);
                DrawTrailingLabel(drawList, CallStatus(view), trailingRight, bounds.Center.Y,
                    trailingMaxWidth, Ink, alpha);
                break;
            case IslandActivity.Session:
                DrawIconBubble(drawList, bubbleCenter, bubbleRadius, FontAwesomeIcon.Tv, accent, alpha);
                DrawLiveLabel(drawList, trailingRight, bounds.Center.Y, trailingMaxWidth, scale, accent, alpha);
                break;
            case IslandActivity.Playback:
                DrawPlaybackArt(drawList, CompactArtCenter(bubbleCenter, scale), bubbleRadius * CompactArtFraction,
                    alpha);
                Equalizer.Draw(drawList, new Vector2(trailingRight - 3f * scale, bounds.Center.Y), scale,
                    bounds.Height * 0.44f, clock, accent, alpha, playback.IsPlaying);
                break;
            case IslandActivity.PcMedia:
                DrawPcMediaCompact(drawList, bubbleCenter, bubbleRadius, trailingRight, bounds, scale, accent, alpha);
                break;
            case IslandActivity.Timer:
                var remaining = TimerRemainingSeconds();
                DrawTimerRing(drawList, bubbleCenter, bubbleRadius - 3f * scale, 2f * scale, remaining, alpha);
                DrawTrailingLabel(drawList, TimerText(remaining), trailingRight, bounds.Center.Y, trailingMaxWidth,
                    accent, alpha);
                break;
            case IslandActivity.Muster:
                if (upcomingMuster is not { } muster)
                {
                    break;
                }

                DrawIconBubble(drawList, bubbleCenter, bubbleRadius, MusterCategories.Icon(muster.Category), accent,
                    alpha);
                DrawTrailingLabel(drawList, MusterCountdown(muster, true), trailingRight, bounds.Center.Y,
                    trailingMaxWidth, accent, alpha);
                break;
            case IslandActivity.Notice:
                DrawNotice(drawList, bubbleCenter, bubbleRadius, trailingRight, bounds.Center.Y, scale, alpha);
                break;
            case IslandActivity.GameTimer:
                DrawIconBubble(drawList, bubbleCenter, bubbleRadius, GameTimerIcon(upcomingGameTimer), accent, alpha);
                DrawTrailingLabel(drawList, GameTimerText(upcomingGameTimer), trailingRight, bounds.Center.Y,
                    trailingMaxWidth, accent, alpha);
                break;

            case IslandActivity.Fishing:
                if (fishing is not { } alerts)
                {
                    break;
                }

                DrawIconBubble(drawList, bubbleCenter, bubbleRadius, FishingIcon(alerts.Island), accent, alpha);
                DrawTrailingLabel(drawList, FishingCountdown(alerts.Island, true), trailingRight, bounds.Center.Y,
                    trailingMaxWidth, accent, alpha);
                break;
        }
    }

    private void DrawNotice(ImDrawListPtr drawList, Vector2 bubbleCenter, float bubbleRadius, float right,
        float centerY, float scale, float alpha)
    {
        var tint = noticeEnabled ? FocusAccent : QuietInk;
        DrawIconBubble(drawList, bubbleCenter, bubbleRadius, NoticeIcon(), tint, alpha);
        var state = Loc.T(noticeEnabled ? L.Common.On : L.Common.Off);
        var stateSize = Typography.Measure(state, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(right - stateSize.X, centerY - stateSize.Y * 0.5f), state,
            Palette.WithAlpha(tint, alpha), TextStyles.FootnoteEmphasized);
        var titleLeft = bubbleCenter.X + bubbleRadius + CompactTrailingGap * scale;
        var titleWidth = MathF.Max(1f, right - stateSize.X - CompactTrailingGap * scale - titleLeft);
        var title = Typography.FitText(Loc.T(notice == IslandNotice.LockPosition
            ? L.ControlCenter.LockPosition
            : L.Settings.DoNotDisturb), titleWidth, TextStyles.Footnote);
        var titleSize = Typography.Measure(title, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(titleLeft, centerY - titleSize.Y * 0.5f), title,
            Palette.WithAlpha(Ink, alpha), TextStyles.Footnote);
    }

    private void DrawCallBubble(ImDrawListPtr drawList, Vector2 center, float radius, float scale, float alpha)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(CallAccent, IconBubbleAlpha * alpha)),
            32);
        var pulseValue = 0.5f + 0.5f * MathF.Sin(clock * CallPulseSpeed);
        drawList.AddCircleFilled(center, (3.4f + 1.2f * pulseValue) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(CallAccent, alpha)), 16);
    }

    private void DrawPlaybackArt(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        var side = radius * 2f;
        NowPlayingArt.DrawSquircle(drawList, center - new Vector2(radius, radius), side, side * ArtRadiusFraction,
            playback.ArtworkUrl, playback.Title, alpha);
    }

    private static void DrawIconBubble(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon,
        Vector4 accent, float alpha)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, IconBubbleAlpha * alpha)),
            32);
        ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(accent, alpha), radius);
    }

    private void DrawTimerRing(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, int remaining,
        float alpha)
    {
        ProgressRing.Track(drawList, center, radius, thickness, Palette.WithAlpha(TimerAccent, 0.25f * alpha));
        ProgressRing.Fill(drawList, center, radius, thickness, TimerFraction(remaining),
            Palette.WithAlpha(TimerAccent, alpha));
    }

    private static void DrawTrailingLabel(ImDrawListPtr drawList, string text, float right, float centerY,
        float maxWidth, Vector4 color, float alpha)
    {
        var label = Typography.FitText(text, maxWidth, TextStyles.Footnote);
        var size = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), label,
            Palette.WithAlpha(color, alpha), TextStyles.Footnote);
    }

    private void DrawLiveLabel(ImDrawListPtr drawList, float right, float centerY, float maxWidth, float scale,
        Vector4 accent, float alpha)
    {
        var label = Typography.FitText(Loc.T(L.Island.Live), maxWidth, TextStyles.FootnoteEmphasized);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var left = right - size.X;
        Typography.Draw(drawList, new Vector2(left, centerY - size.Y * 0.5f), label, Palette.WithAlpha(accent, alpha),
            TextStyles.FootnoteEmphasized);
        var pulseValue = 0.5f + 0.5f * MathF.Sin(clock * CallPulseSpeed);
        drawList.AddCircleFilled(new Vector2(left - 7f * scale, centerY), (2.2f + 0.8f * pulseValue) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)), 12);
    }

    private bool DrawExpanded(ImDrawListPtr drawList, Rect bounds, float scale, PhoneTheme theme,
        INavigator navigation, in CallView view, Vector4 accent, float alpha)
    {
        if (alpha <= 0.05f)
        {
            return false;
        }

        var iconRadius = CardIconRadius * scale;
        var controlRadius = CardControlRadius * scale;
        var centerY = bounds.Center.Y;
        var iconCenter = new Vector2(bounds.Min.X + CardPadX * scale + iconRadius, centerY);
        var controlCenter = new Vector2(bounds.Max.X - CardPadX * scale - controlRadius, centerY);
        var textLeft = iconCenter.X + iconRadius + CardTextGap * scale;
        var textWidth = MathF.Max(1f, controlCenter.X - controlRadius - CardTextGap * scale - textLeft);
        var active = alpha > ControlThreshold;
        var controlExtent = new Vector2(controlRadius, controlRadius);
        var overControl = active && UiInteract.Hover(controlCenter - controlExtent, controlCenter + controlExtent);
        var controlFill = Palette.WithAlpha(Ink, ControlFillAlpha);
        switch (shownKind)
        {
            case IslandActivity.Call:
            {
                DrawIconBubble(drawList, iconCenter, iconRadius, FontAwesomeIcon.Phone, CallAccent, alpha);
                DrawLines(drawList, view.PeerLabel, TextStyles.Headline, Ink, CallStatus(view),
                    TextStyles.Subheadline, CallAccent, textLeft, textWidth, centerY, scale, alpha, false);
                var ringing = view.State == CallState.Ringing;
                if (RoundButton(drawList, controlCenter, controlRadius,
                        ringing ? FontAwesomeIcon.Phone : FontAwesomeIcon.PhoneSlash,
                        ringing ? CallAccent : theme.Danger, Ink, alpha, active))
                {
                    if (ringing)
                    {
                        calls.Accept();
                    }
                    else
                    {
                        calls.Hangup();
                    }
                }

                break;
            }
            case IslandActivity.Session:
            {
                if (video is not { } suite)
                {
                    break;
                }

                DrawIconBubble(drawList, iconCenter, iconRadius, FontAwesomeIcon.Tv, accent, alpha);
                DrawLines(drawList, SessionTitle(suite), TextStyles.Headline, Ink, SessionStatus(suite),
                    TextStyles.Subheadline, accent, textLeft, textWidth, centerY, scale, alpha, true);
                if (RoundButton(drawList, controlCenter, controlRadius, FontAwesomeIcon.ArrowRight, controlFill, Ink,
                        alpha, active))
                {
                    OpenOwner(navigation);
                }

                break;
            }
            case IslandActivity.Playback:
            {
                DrawPlaybackArt(drawList, iconCenter, iconRadius, alpha);
                DrawLines(drawList, playback.Title, TextStyles.Headline, Ink, playback.Subtitle,
                    TextStyles.Subheadline, accent, textLeft, textWidth, centerY, scale, alpha, true);
                drawList.AddCircleFilled(controlCenter, controlRadius,
                    ImGui.GetColorU32(Palette.WithAlpha(Ink, ControlFillAlpha * alpha)), 32);
                if (TransportButton.Draw(controlCenter, controlRadius,
                        playback.IsPlaying ? TransportAction.Pause : TransportAction.Play, accent, Ink, alpha, active,
                        drawList))
                {
                    playback.TogglePlayPause();
                }

                break;
            }
            case IslandActivity.PcMedia:
                overControl = DrawPcMediaExpanded(drawList, bounds, scale, accent, alpha, active);
                break;
            case IslandActivity.Timer:
            {
                var remaining = TimerRemainingSeconds();
                DrawTimerRing(drawList, iconCenter, iconRadius - 4f * scale, 2.6f * scale, remaining, alpha);
                DrawLines(drawList, Loc.T(L.Clock.TimerTitle), TextStyles.Subheadline, Palette.WithAlpha(Ink, 0.8f),
                    TimerText(remaining), TextStyles.WidgetDisplayCompact, Ink, textLeft, textWidth, centerY, scale,
                    alpha, false);
                if (RoundButton(drawList, controlCenter, controlRadius, FontAwesomeIcon.Stop, controlFill, Ink, alpha,
                        active))
                {
                    configuration.TimerEndsAtUtc = null;
                    configuration.TimerNotified = false;
                    configuration.Save();
                }

                break;
            }
            case IslandActivity.Muster:
            {
                if (upcomingMuster is not { } muster)
                {
                    break;
                }

                DrawIconBubble(drawList, iconCenter, iconRadius, MusterCategories.Icon(muster.Category), accent, alpha);
                DrawLines(drawList, MusterText.HostLabel(muster), TextStyles.Headline, Ink,
                    MusterCountdown(muster, false), TextStyles.Subheadline, accent, textLeft, textWidth, centerY,
                    scale, alpha, false);
                if (RoundButton(drawList, controlCenter, controlRadius, FontAwesomeIcon.ArrowRight, controlFill, Ink,
                        alpha, active))
                {
                    OpenOwner(navigation);
                }

                break;
            }
            case IslandActivity.GameTimer:
            {
                DrawIconBubble(drawList, iconCenter, iconRadius, GameTimerIcon(upcomingGameTimer), accent, alpha);
                DrawLines(drawList, upcomingGameTimer.Name, TextStyles.Headline, Ink,
                    GameTimerText(upcomingGameTimer), TextStyles.Subheadline, accent, textLeft, textWidth, centerY,
                    scale, alpha, true);
                if (RoundButton(drawList, controlCenter, controlRadius, FontAwesomeIcon.ArrowRight, controlFill, Ink,
                        alpha, active))
                {
                    OpenOwner(navigation);
                }

                break;
            }

            case IslandActivity.Fishing:
            {
                if (fishing is not { } alerts)
                {
                    break;
                }

                var status = alerts.Island;
                DrawIconBubble(drawList, iconCenter, iconRadius, FishingIcon(status), accent, alpha);
                DrawLines(drawList, status.Title, TextStyles.Headline, Ink, FishingCountdown(status, false),
                    TextStyles.Subheadline, accent, textLeft, textWidth, centerY, scale, alpha, true);
                if (RoundButton(drawList, controlCenter, controlRadius, FontAwesomeIcon.ArrowRight, controlFill, Ink,
                        alpha, active))
                {
                    OpenOwner(navigation);
                }

                break;
            }
        }

        return overControl;
    }

    private static void DrawLines(ImDrawListPtr drawList, string title, in TextStyle titleStyle, Vector4 titleColor,
        string status, in TextStyle statusStyle, Vector4 statusColor, float left, float maxWidth, float centerY,
        float scale, float alpha, bool marquee)
    {
        var titleSize = Typography.Measure(title, titleStyle);
        var statusText = Typography.FitText(status, maxWidth, statusStyle);
        var statusSize = Typography.Measure(statusText, statusStyle);
        var gap = CardLineGap * scale;
        var top = centerY - (titleSize.Y + gap + statusSize.Y) * 0.5f;
        if (marquee)
        {
            var hovering = UiInteract.Hover(new Vector2(left, top), new Vector2(left + maxWidth, top + titleSize.Y));
            Marquee.DrawLeft(drawList, "dynamicisland.title", title, left, top, maxWidth, titleStyle,
                Palette.WithAlpha(titleColor, alpha), hovering);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, maxWidth, titleStyle),
                Palette.WithAlpha(titleColor, alpha), titleStyle);
        }

        Typography.Draw(drawList, new Vector2(left, top + titleSize.Y + gap), statusText,
            Palette.WithAlpha(statusColor, 0.9f * alpha), statusStyle);
    }

    private void DrawBubble(ImDrawListPtr drawList, PhoneTheme theme, INavigator navigation, Rect bounds, float scale,
        float expandAmount)
    {
        var splitValue = Math.Clamp(split.Value, 0f, 1f);
        var visible = splitValue > 0.02f && expandAmount < 0.6f;
        lastBubbleVisible = visible;
        if (!visible)
        {
            return;
        }

        var alpha = Math.Clamp(splitValue * 1.4f, 0f, 1f) * (1f - expandAmount);
        var radius = bounds.Height * 0.5f;
        var centerX = float.Lerp(bounds.Max.X - radius, bounds.Max.X + BubbleGap * scale + radius, splitValue);
        var center = new Vector2(centerX, bounds.Center.Y);
        lastBubble = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.Glass, alpha)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(MusicAccent, 0.30f * alpha)), 32,
            1.4f * scale);
        Equalizer.Draw(drawList, center, scale, radius * 0.66f, clock, MusicAccent, alpha, playback.IsPlaying);
        var hovered = UiInteract.Hover(lastBubble.Min, lastBubble.Max);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (UiInteract.Click(lastBubble.Min, lastBubble.Max, hovered))
        {
            navigation.Open(IslandActivities.OwnerAppId(IslandActivity.Playback));
        }
    }

    private static bool RoundButton(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon,
        Vector4 fill, Vector4 ink, float alpha, bool active)
    {
        var hovered = active &&
                      UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var color = hovered ? Palette.Mix(fill, Ink, 0.14f) : fill;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha * color.W)), 28);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = IconGlyph.Of(icon);
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(ink, alpha)))
            {
                Typography.Plain(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static Rect CompactBounds(Rect rest, float scale)
    {
        var padY = MathF.Max(0f, (CompactHeight * scale - rest.Height) * 0.5f);
        var pad = new Vector2(CompactPadX * scale, padY);
        return new Rect(rest.Min - pad, rest.Max + pad);
    }

    private FontAwesomeIcon NoticeIcon()
    {
        if (notice == IslandNotice.LockPosition)
        {
            return noticeEnabled ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
        }

        return FontAwesomeIcon.Moon;
    }

    private static Rect NoticeBounds(Rect screen, Rect rest, float scale)
    {
        var compact = CompactBounds(rest, scale);
        var halfWidth = MathF.Min(screen.Width * 0.5f - ExpandedSideInset * scale, NoticeHalfWidth * scale);
        var centerX = rest.Center.X;
        return new Rect(new Vector2(MathF.Min(compact.Min.X, centerX - halfWidth), compact.Min.Y),
            new Vector2(MathF.Max(compact.Max.X, centerX + halfWidth), compact.Max.Y));
    }

    private static Rect ExpandedBounds(Rect screen, Rect rest, float scale)
    {
        var halfWidth = MathF.Min(screen.Width * 0.5f - ExpandedSideInset * scale, ExpandedHalfWidth * scale);
        var top = rest.Min.Y - ExpandedLift * scale;
        var centerX = screen.Center.X;
        return new Rect(new Vector2(centerX - halfWidth, top),
            new Vector2(centerX + halfWidth, top + ExpandedHeight * scale));
    }

    private static Rect Swell(Rect rect, float amount)
    {
        if (amount <= 0.001f)
        {
            return rect;
        }

        var grow = new Vector2(rect.Width * (PulseWidthScale - 1f) * 0.5f * amount, 0f);
        return new Rect(rect.Min - grow, rect.Max + grow);
    }

    private static Rect LerpRect(Rect from, Rect to, float amount) =>
        new(Vector2.Lerp(from.Min, to.Min, amount), Vector2.Lerp(from.Max, to.Max, amount));
}
