using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class DynamicIsland
{
    private const ImGuiWindowFlags IslandFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private enum ActivityKind
    {
        None,
        Call,
        Music,
        Timer,
    }

    private const float PresenceSmoothTime = 0.14f;
    private const float SplitSmoothTime = 0.12f;
    private const float ExpandSmoothTime = 0.16f;
    private const float CompactPadX = 8f;
    private const float CompactPadY = 3f;
    private const float BubbleGap = 7f;
    private const float CallExpandedHeight = 104f;
    private const float CallExpandedHalfWidth = 138f;
    private const float MusicExpandedHeight = 120f;
    private const float MusicExpandedHalfWidth = 142f;
    private const float TimerExpandedHeight = 92f;
    private const float TimerExpandedHalfWidth = 118f;
    private const float ControlThreshold = 0.6f;

    private static readonly Vector4 MusicAccent = AppAccents.For("music");
    private static readonly Vector4 CallAccent = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 TimerAccent = new(1.00f, 0.62f, 0.18f, 1f);
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);

    private readonly PlaybackHub playback;
    private readonly CallHub calls;
    private readonly Configuration configuration;
    private int timerCachedSeconds = -1;
    private string timerCachedText = string.Empty;
    private Spring presence;
    private Spring split;
    private Spring expand;
    private float clock;
    private ActivityKind shownKind = ActivityKind.None;
    private Rect lastBounds;
    private Rect lastBubble;
    private bool lastBubbleVisible;

    public DynamicIsland(PlaybackHub playback, CallHub calls, Configuration configuration)
    {
        this.playback = playback;
        this.calls = calls;
        this.configuration = configuration;
    }

    public bool CapturesPointer()
    {
        if (presence.Value < 0.05f)
        {
            return false;
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
        var callActive = view.State is CallState.Dialing or CallState.Connecting or CallState.Active;
        var musicActive = playback.IsActive;
        var timerActive = TimerRemainingSeconds() > 0;
        var primary = callActive ? ActivityKind.Call :
            musicActive ? ActivityKind.Music :
            timerActive ? ActivityKind.Timer : ActivityKind.None;
        if (primary != ActivityKind.None)
        {
            shownKind = primary;
        }

        var delta = FrameClock.Delta;
        clock += delta;
        presence.Step(primary == ActivityKind.None ? 0f : 1f, PresenceSmoothTime, delta);
        split.Step(callActive && musicActive ? 1f : 0f, SplitSmoothTime, delta);
        var presenceValue = Math.Clamp(presence.Value, 0f, 1f);
        if (primary == ActivityKind.None && presenceValue < 0.02f)
        {
            expand.SnapTo(0f);
            lastBounds = StatusBar.BaseIsland(screen);
            lastBubbleVisible = false;
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##dynamicIsland", screen.Size, false, IslandFlags))
        {
            DrawContent(screen, theme, navigation, view, presenceValue, delta, foregroundAppId);
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, INavigator navigation, CallView view, float presenceValue,
        float delta, string? foregroundAppId)
    {
        var scale = UiScale.Current;
        var rest = StatusBar.BaseIsland(screen);
        var compact = Expand(rest, CompactPadX * scale, CompactPadY * scale);
        var expanded = ExpandedBounds(screen, rest, scale);
        var morphed = LerpRect(rest, compact, presenceValue);
        var hoverBounds = LerpRect(morphed, expanded, Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f)));
        var hovered = UiInteract.Hover(hoverBounds.Min, hoverBounds.Max);
        var suppressExpand = shownKind == ActivityKind.Music &&
                             string.Equals(foregroundAppId, "music", StringComparison.Ordinal);
        expand.Step(hovered && presenceValue > 0.6f && !suppressExpand ? 1f : 0f, ExpandSmoothTime, delta);
        var expandEased = Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f));
        var bounds = LerpRect(morphed, expanded, expandEased);
        lastBounds = bounds;
        var accent = shownKind switch
        {
            ActivityKind.Call => CallAccent,
            ActivityKind.Timer => TimerAccent,
            _ => MusicAccent,
        };
        var compactAlpha = Math.Clamp(presenceValue * 1.6f - 0.6f, 0f, 1f) * (1f - expandEased);
        var drawList = ImGui.GetWindowDrawList();
        DrawBubble(drawList, theme, navigation, bounds, rest, scale, expandEased);
        var rounding = float.Lerp(bounds.Height * 0.5f, 28f * scale, expandEased);
        if (expandEased > 0.02f)
        {
            Elevation.Draw(drawList, bounds.Min, bounds.Max, rounding, scale, 5f + 6f * expandEased, 3f,
                0.24f * expandEased);
        }

        drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(theme.Glass), rounding);
        drawList.AddRect(bounds.Min, bounds.Max,
            ImGui.GetColorU32(Palette.WithAlpha(accent, (0.16f + 0.44f * expandEased) * presenceValue)), rounding,
            ImDrawFlags.RoundCornersAll, 1.5f * scale);
        if (shownKind == ActivityKind.Call)
        {
            DrawCallCompact(drawList, bounds, scale, view, compactAlpha);
        }
        else if (shownKind == ActivityKind.Timer)
        {
            DrawTimerCompact(drawList, bounds, scale, compactAlpha);
        }
        else
        {
            DrawMusicCompact(drawList, bounds, scale, compactAlpha);
        }

        var consumed = shownKind switch
        {
            ActivityKind.Call => DrawCallExpanded(drawList, bounds, scale, theme, view, expandEased),
            ActivityKind.Timer => DrawTimerExpanded(drawList, bounds, scale, theme, expandEased),
            _ => DrawMusicExpanded(drawList, bounds, scale, theme, expandEased),
        };
        if (consumed || !hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (shownKind == ActivityKind.Call)
            {
                calls.RequestCallScreen();
            }

            navigation.Open(shownKind switch
            {
                ActivityKind.Call => "message",
                ActivityKind.Timer => "clock",
                _ => "music",
            });
        }
    }

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

    private void DrawTimerCompact(ImDrawListPtr drawList, Rect bounds, float scale, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var remaining = TimerRemainingSeconds();
        var ringRadius = bounds.Height * 0.28f;
        var ringCenter = new Vector2(bounds.Min.X + 10f * scale + ringRadius, bounds.Center.Y);
        ProgressRing.Track(ringCenter, ringRadius, 2f * scale, Palette.WithAlpha(TimerAccent, 0.25f * alpha));
        ProgressRing.Fill(ringCenter, ringRadius, 2f * scale, TimerFraction(remaining),
            Palette.WithAlpha(TimerAccent, alpha));
        var label = TimerText(remaining);
        var size = Typography.Measure(label, 0.82f);
        Typography.Draw(drawList, new Vector2(bounds.Max.X - size.X - 11f * scale, bounds.Center.Y - size.Y * 0.5f),
            label, Palette.WithAlpha(TimerAccent, alpha), 0.82f);
    }

    private bool DrawTimerExpanded(ImDrawListPtr drawList, Rect bounds, float scale, PhoneTheme theme, float alpha)
    {
        if (alpha <= 0.05f)
        {
            return false;
        }

        var remaining = TimerRemainingSeconds();
        var centerX = bounds.Center.X;
        var top = bounds.Min.Y;
        var ringRadius = 15f * scale;
        var ringCenter = new Vector2(centerX - 52f * scale, top + 34f * scale);
        ProgressRing.Track(ringCenter, ringRadius, 2.6f * scale, Palette.WithAlpha(TimerAccent, 0.25f * alpha));
        ProgressRing.Fill(ringCenter, ringRadius, 2.6f * scale, TimerFraction(remaining),
            Palette.WithAlpha(TimerAccent, alpha));
        Typography.DrawCentered(new Vector2(centerX + 14f * scale, top + 34f * scale), TimerText(remaining),
            Palette.WithAlpha(theme.TextStrong, alpha), 1.45f, FontWeight.SemiBold);
        var active = alpha > ControlThreshold;
        if (RoundButton(new Vector2(centerX, top + 68f * scale), 15f * scale, FontAwesomeIcon.Stop,
                Palette.WithAlpha(theme.TextStrong, 0.18f), theme.TextStrong, alpha, active))
        {
            configuration.TimerEndsAtUtc = null;
            configuration.TimerNotified = false;
            configuration.Save();
            return true;
        }

        return false;
    }

    private void DrawBubble(ImDrawListPtr drawList, PhoneTheme theme, INavigator navigation, Rect bounds, Rect rest,
        float scale, float expandEased)
    {
        var splitValue = Math.Clamp(split.Value, 0f, 1f);
        var visible = splitValue > 0.02f && expandEased < 0.6f;
        lastBubbleVisible = visible;
        if (!visible)
        {
            return;
        }

        var alpha = Math.Clamp(splitValue * 1.4f, 0f, 1f) * (1f - expandEased);
        var radius = rest.Height * 0.5f + 3f * scale;
        var centerX = float.Lerp(bounds.Max.X - radius, bounds.Max.X + BubbleGap * scale + radius, splitValue);
        var center = new Vector2(centerX, bounds.Center.Y);
        lastBubble = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.Glass, alpha)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(MusicAccent, 0.30f * alpha)), 32,
            1.4f * scale);
        Equalizer.Draw(drawList, new Vector2(center.X + 5f * scale, center.Y), scale, radius * 0.66f, clock,
            MusicAccent, alpha, playback.IsPlaying);
        var hovered = UiInteract.Hover(lastBubble.Min, lastBubble.Max);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            navigation.Open("music");
        }
    }

    private void DrawMusicCompact(ImDrawListPtr drawList, Rect bounds, float scale, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var discRadius = bounds.Height * 0.34f;
        var discCenter = new Vector2(bounds.Min.X + 9f * scale + discRadius, bounds.Center.Y);
        ArtGradient.DrawDisc(drawList, discCenter, discRadius, ArtGradient.FromName(playback.Title), alpha);
        var eqCenter = new Vector2(bounds.Max.X - 13f * scale, bounds.Center.Y);
        Equalizer.Draw(drawList, eqCenter, scale, bounds.Height * 0.5f, clock, MusicAccent, alpha, playback.IsPlaying);
    }

    private void DrawCallCompact(ImDrawListPtr drawList, Rect bounds, float scale, CallView view, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 3f);
        var dotCenter = new Vector2(bounds.Min.X + 13f * scale, bounds.Center.Y);
        drawList.AddCircleFilled(dotCenter, (3.4f + 1.2f * pulse) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(CallAccent, alpha)), 16);
        var maxWidth = MathF.Max(1f, bounds.Max.X - 11f * scale - (dotCenter.X + 8f * scale));
        var label = Typography.FitText(CallStatusText.Label(view), maxWidth, 0.82f, FontWeight.Regular);
        var size = Typography.Measure(label, 0.82f);
        Typography.Draw(drawList, new Vector2(bounds.Max.X - size.X - 11f * scale, bounds.Center.Y - size.Y * 0.5f),
            label, Palette.WithAlpha(Ink, alpha), 0.82f);
    }

    private bool DrawMusicExpanded(ImDrawListPtr drawList, Rect bounds, float scale, PhoneTheme theme, float alpha)
    {
        if (alpha <= 0.05f)
        {
            return false;
        }

        var left = bounds.Min.X;
        var top = bounds.Min.Y;
        var centerX = bounds.Center.X;
        var discRadius = 19f * scale;
        var discCenter = new Vector2(left + 18f * scale + discRadius, top + 30f * scale);
        ArtGradient.DrawDisc(drawList, discCenter, discRadius, ArtGradient.FromName(playback.Title), alpha);
        var textLeft = discCenter.X + discRadius + 12f * scale;
        var textMaxWidth = bounds.Max.X - 16f * scale - textLeft;
        var titleTop = top + 18f * scale;
        var titleSize = Typography.Measure(playback.Title, 1.0f, FontWeight.SemiBold);
        var titleHovering = UiInteract.Hover(new Vector2(textLeft, titleTop),
            new Vector2(textLeft + textMaxWidth, titleTop + titleSize.Y));
        Marquee.DrawLeft("dynamicisland.music.title", playback.Title, textLeft, titleTop, textMaxWidth,
            new TextStyle(1.0f, FontWeight.SemiBold), Palette.WithAlpha(theme.TextStrong, alpha), titleHovering);
        Typography.Draw(new Vector2(textLeft, top + 40f * scale),
            Typography.FitText(playback.Subtitle, textMaxWidth, 0.8f, FontWeight.Regular),
            Palette.WithAlpha(MusicAccent, 0.9f * alpha), 0.8f);
        var active = alpha > ControlThreshold;
        var controlY = top + 66f * scale;
        var previousCenter = new Vector2(centerX - 46f * scale, controlY);
        var nextCenter = new Vector2(centerX + 46f * scale, controlY);
        var playCenter = new Vector2(centerX, controlY);
        var sideRadius = 16f * scale;
        var playRadius = 18f * scale;
        var hasQueue = playback.HasQueue;
        var consumed = active && (TransportButton.Hovered(playCenter, playRadius) ||
                                  hasQueue && (TransportButton.Hovered(previousCenter, sideRadius) ||
                                               TransportButton.Hovered(nextCenter, sideRadius)));
        if (hasQueue)
        {
            if (TransportButton.Draw(previousCenter, sideRadius, TransportAction.Previous, MusicAccent, Ink, alpha,
                    active))
            {
                playback.Previous();
            }

            if (TransportButton.Draw(nextCenter, sideRadius, TransportAction.Next, MusicAccent, Ink, alpha, active))
            {
                playback.Next();
            }
        }

        if (TransportButton.Draw(playCenter, playRadius,
                playback.IsPlaying ? TransportAction.Pause : TransportAction.Play, MusicAccent, Ink, alpha, active))
        {
            playback.TogglePlayPause();
        }

        if (active)
        {
            var trackY = top + 99f * scale;
            var track = new Rect(new Vector2(left + 22f * scale, trackY - 2.5f * scale),
                new Vector2(bounds.Max.X - 22f * scale, trackY + 2.5f * scale));
            playback.Volume = Scrubber.Draw(track, playback.Volume, MusicAccent,
                Palette.WithAlpha(theme.TextStrong, 0.18f), alpha);
            if (Scrubber.IsHovered(track))
            {
                consumed = true;
            }
        }

        return consumed;
    }

    private bool DrawCallExpanded(ImDrawListPtr drawList, Rect bounds, float scale, PhoneTheme theme, CallView view,
        float alpha)
    {
        if (alpha <= 0.05f)
        {
            return false;
        }

        var centerX = bounds.Center.X;
        var top = bounds.Min.Y;
        Typography.DrawCentered(new Vector2(centerX, top + 20f * scale),
            Typography.FitText(view.PeerLabel, bounds.Width - 32f * scale, 1.05f, FontWeight.Regular),
            Palette.WithAlpha(theme.TextStrong, alpha), 1.05f);
        Typography.DrawCentered(new Vector2(centerX, top + 42f * scale), CallStatusText.Label(view),
            Palette.WithAlpha(CallAccent, 0.9f * alpha), 0.82f);
        var active = alpha > ControlThreshold;
        var buttonY = top + 74f * scale;
        var consumed = false;
        var muteFill = view.Muted ? CallAccent : Palette.WithAlpha(theme.TextStrong, 0.18f);
        if (RoundButton(new Vector2(centerX - 34f * scale, buttonY), 17f * scale,
                view.Muted ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone, muteFill, theme.TextStrong,
                alpha, active))
        {
            calls.ToggleMute();
            consumed = true;
        }

        if (RoundButton(new Vector2(centerX + 34f * scale, buttonY), 17f * scale, FontAwesomeIcon.PhoneSlash,
                theme.Danger, Ink, alpha, active))
        {
            calls.Hangup();
            consumed = true;
        }

        return consumed;
    }

    private static bool RoundButton(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill, Vector4 ink,
        float alpha, bool active)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = active &&
                      UiInteract.Hover(center - new Vector2(radius, radius),
                          center + new Vector2(radius, radius));
        var color = hovered ? Palette.Mix(fill, Ink, 0.14f) : fill;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha * color.W)), 28);
        using (Plugin.Fonts.PushDalamudIcon())
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

    private Rect ExpandedBounds(Rect screen, Rect rest, float scale)
    {
        var halfWidthUnits = shownKind switch
        {
            ActivityKind.Call => CallExpandedHalfWidth,
            ActivityKind.Timer => TimerExpandedHalfWidth,
            _ => MusicExpandedHalfWidth,
        };
        var heightUnits = shownKind switch
        {
            ActivityKind.Call => CallExpandedHeight,
            ActivityKind.Timer => TimerExpandedHeight,
            _ => MusicExpandedHeight,
        };
        var halfWidth = MathF.Min(screen.Width * 0.5f - 14f * scale, halfWidthUnits * scale);
        var height = heightUnits * scale;
        var centerX = screen.Center.X;
        var top = rest.Min.Y - 2f * scale;
        return new Rect(new Vector2(centerX - halfWidth, top), new Vector2(centerX + halfWidth, top + height));
    }

    private static Rect Expand(Rect rect, float padX, float padY) =>
        new(rect.Min - new Vector2(padX, padY), rect.Max + new Vector2(padX, padY));

    private static Rect LerpRect(Rect from, Rect to, float amount) =>
        new(Vector2.Lerp(from.Min, to.Min, amount), Vector2.Lerp(from.Max, to.Max, amount));
}
