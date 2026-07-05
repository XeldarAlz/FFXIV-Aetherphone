using System.Numerics;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal sealed class ControlCenter
{
    private const float SmoothTime = 0.20f;
    private const float OpenFraction = 0.55f;
    private const float CommitFraction = 0.30f;
    private const float FlingVelocity = 900f;
    private const float TapSlop = 6f;
    private const float TopBandHeight = 44f;
    private const float DismissBandHeight = 48f;
    private const float SwatchRadius = 9f;
    private const float SwatchSpacing = 27f;
    private const float NowPlayingHeight = 78f;
    private readonly ThemeProvider themes;
    private readonly PlaybackHub playback;
    private readonly CallHub calls;
    private readonly INavigator navigation;
    private readonly NotificationService notifications;
    private readonly NotificationCenter notificationCenter;
    private readonly DragTracker drag = new();
    private Spring offset;
    private float target;
    private bool open;

    public ControlCenter(ThemeProvider themes, PlaybackHub playback, CallHub calls, INavigator navigation,
        NotificationService notifications, NotificationRouter router)
    {
        this.themes = themes;
        this.playback = playback;
        this.calls = calls;
        this.navigation = navigation;
        this.notifications = notifications;
        notificationCenter = new NotificationCenter(notifications, router, Dismiss);
    }

    public bool IsActive => open || offset.Value > 0.01f;
    public bool CapturesPointer => IsActive;

    public void Draw(Rect screen, PhoneTheme theme, float delta, bool gesturesEnabled)
    {
        HandleGesture(screen, delta, gesturesEnabled);
        var eased = offset.Value;
        if (eased <= 0.001f)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetForegroundDrawList();
        var height = screen.Height;
        var rounding = theme.ScreenRounding * scale;
        var panelTop = screen.Min.Y - (1f - eased) * height;
        dl.PushClipRect(screen.Min, screen.Max, true);
        Material.Veil(dl, screen.Min, screen.Max, 0.46f * eased, rounding);
        Material.Frosted(dl, new Vector2(screen.Min.X, panelTop), new Vector2(screen.Max.X, panelTop + height),
            rounding, scale, 1f);
        var opacity = Math.Clamp(eased * 1.6f, 0f, 1f);
        var interactive = open && !drag.Active && offset.Value > 0.96f;
        DrawContents(screen, theme, panelTop, scale, opacity, interactive);
        dl.PopClipRect();
    }

    private void DrawContents(Rect screen, PhoneTheme theme, float panelTop, float scale, float opacity,
        bool interactive)
    {
        var dl = ImGui.GetForegroundDrawList();
        var pad = 22f * scale;
        var left = screen.Min.X + pad;
        var right = screen.Max.X - pad;
        var width = right - left;
        var grabberHalf = 20f * scale;
        var grabberY = panelTop + 14f * scale;
        dl.AddRectFilled(new Vector2(screen.Center.X - grabberHalf, grabberY - 2.5f * scale),
            new Vector2(screen.Center.X + grabberHalf, grabberY + 2.5f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f * opacity)), 2.5f * scale);
        Typography.Draw(dl, new Vector2(left, panelTop + 28f * scale), Loc.T(L.ControlCenter.Title),
            Palette.WithAlpha(theme.TextStrong, opacity), 1.05f, FontWeight.Bold);
        DrawAccentRow(dl, right, panelTop + 38f * scale, scale, opacity, interactive);
        var gap = 12f * scale;
        var controlsTop = panelTop + 64f * scale;
        var tileWidth = (width - 3f * gap) * 0.27f;
        var sliderWidth = (width - 3f * gap) * 0.23f;
        var tileHeight = MathF.Min(tileWidth * 0.94f, 84f * scale);
        var gridHeight = 3f * tileHeight + 2f * gap;
        DrawToggles(dl, theme, left, controlsTop, tileWidth, tileHeight, gap, opacity, interactive);
        DrawSliders(dl, theme, left + 2f * tileWidth + 2f * gap, controlsTop, sliderWidth, gridHeight, gap, opacity,
            interactive);
        var contentBottom = controlsTop + gridHeight;
        if (playback.IsActive)
        {
            var nowPlaying = new Rect(new Vector2(left, contentBottom + 14f * scale),
                new Vector2(right, contentBottom + 14f * scale + NowPlayingHeight * scale));
            DrawNowPlaying(nowPlaying, theme, scale, opacity, interactive);
            contentBottom = nowPlaying.Max.Y;
        }

        DrawNotificationSection(dl, theme, left, right, contentBottom, screen, scale, opacity, interactive);
    }

    private void DrawAccentRow(ImDrawListPtr dl, float right, float centerY, float scale, float opacity,
        bool interactive)
    {
        var accents = ThemeCatalog.Accents;
        var radius = SwatchRadius * scale;
        var spacing = SwatchSpacing * scale;
        var firstCenterX = right - radius - (accents.Count - 1) * spacing;
        for (var index = 0; index < accents.Count; index++)
        {
            var center = new Vector2(firstCenterX + index * spacing, centerY);
            var selected = accents[index].Name == Plugin.Cfg.AccentName;
            if (ControlTile.Swatch(dl, center, radius, accents[index].Color, selected, opacity, interactive) &&
                !selected)
            {
                Plugin.Cfg.AccentName = accents[index].Name;
                themes.Apply(Plugin.Cfg);
                Plugin.Cfg.Save();
            }
        }
    }

    private void DrawToggles(ImDrawListPtr dl, PhoneTheme theme, float left, float top, float tileWidth,
        float tileHeight, float gap, float opacity, bool interactive)
    {
        var row1Top = top + tileHeight + gap;
        var row2Top = row1Top + tileHeight + gap;
        var dnd = TileRect(left, top, tileWidth, tileHeight, 0, gap);
        var callsTile = TileRect(left, top, tileWidth, tileHeight, 1, gap);
        var pin = TileRect(left, row1Top, tileWidth, tileHeight, 0, gap);
        var idle = TileRect(left, row1Top, tileWidth, tileHeight, 1, gap);
        var cameraTile = TileRect(left, row2Top, tileWidth, tileHeight, 0, gap);
        var settingsTile = TileRect(left, row2Top, tileWidth, tileHeight, 1, gap);
        if (ControlTile.Toggle(dl, dnd, FontAwesomeIcon.Moon, Loc.T(L.Settings.DoNotDisturb), Plugin.Cfg.DoNotDisturb,
                theme.Accent, theme, opacity, interactive))
        {
            Plugin.Cfg.DoNotDisturb = !Plugin.Cfg.DoNotDisturb;
            Plugin.Cfg.Save();
        }

        if (ControlTile.Toggle(dl, callsTile, FontAwesomeIcon.Phone, Loc.T(L.Phone.Calls), Plugin.Cfg.CallsEnabled,
                theme.Accent, theme, opacity, interactive))
        {
            calls.SetEnabled(!Plugin.Cfg.CallsEnabled);
        }

        if (ControlTile.Toggle(dl, pin, FontAwesomeIcon.Thumbtack, Loc.T(L.ControlCenter.LockPosition),
                Plugin.Cfg.LockPosition, theme.Accent, theme, opacity, interactive))
        {
            Plugin.Cfg.LockPosition = !Plugin.Cfg.LockPosition;
            Plugin.Cfg.Save();
        }

        if (ControlTile.Toggle(dl, idle, FontAwesomeIcon.HandPointUp, Loc.T(L.Settings.ScrollWhileIdle),
                Plugin.Cfg.ScrollWhileIdle, theme.Accent, theme, opacity, interactive))
        {
            Plugin.Cfg.ScrollWhileIdle = !Plugin.Cfg.ScrollWhileIdle;
            Plugin.Cfg.Save();
        }

        if (ControlTile.Toggle(dl, cameraTile, FontAwesomeIcon.Camera, Loc.T(L.Apps.Camera), false, theme.Accent,
                theme, opacity, interactive))
        {
            navigation.Open("camera", AppOpenSource.ControlCenter);
            Dismiss();
        }

        if (ControlTile.Toggle(dl, settingsTile, FontAwesomeIcon.Cog, Loc.T(L.Apps.Settings), false, theme.Accent,
                theme, opacity, interactive))
        {
            navigation.Open("settings", AppOpenSource.ControlCenter);
            Dismiss();
        }
    }

    private void DrawSliders(ImDrawListPtr dl, PhoneTheme theme, float slidersLeft, float top, float sliderWidth,
        float sliderHeight, float gap, float opacity, bool interactive)
    {
        var brightnessRect = new Rect(new Vector2(slidersLeft, top),
            new Vector2(slidersLeft + sliderWidth, top + sliderHeight));
        var volumeLeft = slidersLeft + sliderWidth + gap;
        var volumeRect = new Rect(new Vector2(volumeLeft, top),
            new Vector2(volumeLeft + sliderWidth, top + sliderHeight));
        var newBright = ControlTile.VerticalSlider(dl, brightnessRect, Plugin.Cfg.ScreenBrightness, FontAwesomeIcon.Sun,
            theme, opacity, interactive, out var brightReleased);
        if (MathF.Abs(newBright - Plugin.Cfg.ScreenBrightness) > 0.0005f)
        {
            Plugin.Cfg.ScreenBrightness = newBright;
        }

        if (brightReleased)
        {
            Plugin.Cfg.Save();
        }

        var volumeIcon = playback.Volume <= 0.001f ? FontAwesomeIcon.VolumeMute
            : playback.Volume < 0.5f ? FontAwesomeIcon.VolumeDown : FontAwesomeIcon.VolumeUp;
        var newVolume = ControlTile.VerticalSlider(dl, volumeRect, playback.Volume, volumeIcon, theme, opacity,
            interactive, out _);
        if (MathF.Abs(newVolume - playback.Volume) > 0.0005f)
        {
            playback.Volume = newVolume;
        }
    }

    private void DrawNotificationSection(ImDrawListPtr dl, PhoneTheme theme, float left, float right,
        float contentBottom, Rect screen, float scale, float opacity, bool interactive)
    {
        var titleTop = contentBottom + 28f * scale;
        var panelTop = titleTop + 34f * scale;
        var panelBottom = screen.Max.Y - DismissBandHeight * scale - 8f * scale;
        if (panelBottom - panelTop < 80f * scale)
        {
            return;
        }

        Typography.Draw(dl, new Vector2(left, titleTop), Loc.T(L.ControlCenter.Notifications),
            Palette.WithAlpha(theme.TextStrong, opacity), 1.05f, FontWeight.Bold);
        var padding = 12f * scale;
        var rounding = 22f * scale;
        var measured = notificationCenter.MeasureHeight(scale) + 2f * padding;
        var panelHeight = MathF.Min(panelBottom - panelTop, measured);
        var panel = new Rect(new Vector2(left, panelTop), new Vector2(right, panelTop + panelHeight));
        Squircle.Fill(dl, panel.Min, panel.Max, rounding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f * opacity)));
        Material.EdgeSquircle(dl, panel.Min, panel.Max, rounding, scale, opacity);
        var inner = new Rect(panel.Min + new Vector2(padding, padding), panel.Max - new Vector2(padding, padding));
        notificationCenter.DrawOverlay(dl, inner, theme, opacity, interactive);
    }

    private static Rect TileRect(float left, float top, float tileWidth, float tileHeight, int column, float gap)
    {
        var columnLeft = left + column * (tileWidth + gap);
        return new Rect(new Vector2(columnLeft, top), new Vector2(columnLeft + tileWidth, top + tileHeight));
    }

    private void Open()
    {
        open = true;
        target = 1f;
        notifications.MarkAllRead();
        notificationCenter.Reset();
    }

    private void Dismiss()
    {
        open = false;
        target = 0f;
    }

    private void DrawNowPlaying(Rect rect, PhoneTheme theme, float scale, float opacity, bool interactive)
    {
        var dl = ImGui.GetForegroundDrawList();
        Squircle.Fill(dl, rect.Min, rect.Max, 18f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * opacity)));
        Material.EdgeSquircle(dl, rect.Min, rect.Max, 18f * scale, scale, opacity);
        Typography.Draw(dl, new Vector2(rect.Min.X + 16f * scale, rect.Min.Y + 14f * scale),
            Truncate(playback.Title, 22), Palette.WithAlpha(theme.TextStrong, opacity), 0.92f, FontWeight.SemiBold);
        Typography.Draw(dl, new Vector2(rect.Min.X + 16f * scale, rect.Min.Y + 36f * scale),
            Truncate(playback.Subtitle, 24), Palette.WithAlpha(theme.TextMuted, opacity), 0.78f);
        var controlY = rect.Max.Y - 22f * scale;
        var accent = theme.Accent;
        var ink = theme.TextStrong;
        if (playback.HasQueue && TransportButton.Draw(new Vector2(rect.Max.X - 92f * scale, controlY), 15f * scale,
                TransportAction.Previous, accent, ink, opacity, interactive, dl))
        {
            playback.Previous();
        }

        if (TransportButton.Draw(new Vector2(rect.Max.X - 54f * scale, controlY), 16f * scale, TransportAction.Stop,
                accent, ink, opacity, interactive, dl))
        {
            playback.Stop();
        }

        if (playback.HasQueue && TransportButton.Draw(new Vector2(rect.Max.X - 18f * scale, controlY), 15f * scale,
                TransportAction.Next, accent, ink, opacity, interactive, dl))
        {
            playback.Next();
        }
    }

    private void HandleGesture(Rect screen, float delta, bool gesturesEnabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = screen.Height;
        var openDistance = height * OpenFraction;
        var fling = FlingVelocity * scale;
        drag.Track(delta);
        if (!open)
        {
            if (gesturesEnabled)
            {
                var topBand = new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + TopBandHeight * scale));
                if (!drag.Active && topBand.Contains(ImGui.GetMousePos()))
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                drag.Begin(topBand);
            }

            if (drag.Released(out var totalDelta, out _) && MathF.Abs(totalDelta.X) < TapSlop * scale &&
                MathF.Abs(totalDelta.Y) < TapSlop * scale)
            {
                Open();
            }
        }
        else
        {
            var bottomZone = new Rect(new Vector2(screen.Min.X, screen.Max.Y - DismissBandHeight * scale), screen.Max);
            drag.Begin(bottomZone);
            if (drag.Active)
            {
                var fraction = Math.Clamp(1f + drag.Delta.Y / openDistance, 0f, 1f);
                offset.SnapTo(fraction);
                target = fraction;
            }

            if (drag.Released(out var totalDelta, out var velocity))
            {
                var tapped = MathF.Abs(totalDelta.Y) < TapSlop * scale;
                var dismiss = tapped || -totalDelta.Y / openDistance > CommitFraction || velocity < -fling;
                open = !dismiss;
                target = dismiss ? 0f : 1f;
            }
        }

        if (!drag.Active)
        {
            offset.Step(target, SmoothTime, delta);
            if (offset.IsResting(target, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
            {
                offset.SnapTo(target);
            }
        }
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max - 1) + "…";
    }
}
