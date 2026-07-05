using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
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
    private readonly ThemeProvider themes;
    private readonly PlaybackHub playback;
    private readonly DragTracker drag = new();
    private Spring offset;
    private float target;
    private bool open;

    public ControlCenter(ThemeProvider themes, PlaybackHub playback)
    {
        this.themes = themes;
        this.playback = playback;
    }

    public bool IsActive => open || drag.Active || offset.Value > 0.01f;
    public bool CapturesPointer => IsActive;

    public void Draw(Rect screen, PhoneTheme theme, float delta, bool gesturesEnabled)
    {
        HandleGesture(screen, delta, gesturesEnabled);
        var eased = offset.Value;
        if (eased <= 0.001f && !drag.Active)
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
        var gap = 12f * scale;
        var toggleTop = panelTop + 62f * scale;
        var toggleWidth = (width - 2f * gap) / 3f;
        var toggleHeight = MathF.Min(toggleWidth * 0.94f, 92f * scale);
        var dnd = new Rect(new Vector2(left, toggleTop), new Vector2(left + toggleWidth, toggleTop + toggleHeight));
        var pin = new Rect(new Vector2(left + toggleWidth + gap, toggleTop),
            new Vector2(left + 2f * toggleWidth + gap, toggleTop + toggleHeight));
        var idle = new Rect(new Vector2(right - toggleWidth, toggleTop), new Vector2(right, toggleTop + toggleHeight));
        if (ControlTile.Toggle(dl, dnd, FontAwesomeIcon.Moon, Loc.T(L.Settings.DoNotDisturb), Plugin.Cfg.DoNotDisturb,
                theme.Accent, theme, opacity, interactive))
        {
            Plugin.Cfg.DoNotDisturb = !Plugin.Cfg.DoNotDisturb;
            Plugin.Cfg.Save();
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

        var swatchTop = toggleTop + toggleHeight + 22f * scale;
        Typography.Draw(dl, new Vector2(left, swatchTop), Loc.T(L.Settings.Appearance).ToUpperInvariant(),
            Palette.WithAlpha(theme.TextMuted, opacity), 0.72f, FontWeight.Medium);
        var accents = ThemeCatalog.Accents;
        var swatchRadius = 13f * scale;
        var swatchY = swatchTop + 26f * scale;
        var swatchSpacing = width / accents.Count;
        for (var index = 0; index < accents.Count; index++)
        {
            var center = new Vector2(left + swatchSpacing * (index + 0.5f), swatchY);
            var selected = accents[index].Name == Plugin.Cfg.AccentName;
            if (ControlTile.Swatch(dl, center, swatchRadius, accents[index].Color, selected, opacity, interactive) &&
                !selected)
            {
                Plugin.Cfg.AccentName = accents[index].Name;
                themes.Apply(Plugin.Cfg);
                Plugin.Cfg.Save();
            }
        }

        var sliderTop = swatchY + swatchRadius + 18f * scale;
        var sliderHeight = MathF.Min(150f * scale,
            screen.Max.Y - 0.18f * screen.Height - sliderTop - (playback.IsActive ? 96f * scale : 8f * scale));
        sliderHeight = MathF.Max(sliderHeight, 96f * scale);
        var sliderWidth = (width - gap) / 2f;
        var brightnessRect = new Rect(new Vector2(left, sliderTop),
            new Vector2(left + sliderWidth, sliderTop + sliderHeight));
        var volumeRect = new Rect(new Vector2(right - sliderWidth, sliderTop),
            new Vector2(right, sliderTop + sliderHeight));
        var baseBright = Math.Clamp((Plugin.Cfg.TextZoom - 1.0f) / 0.5f, 0f, 1f);
        var newBright = ControlTile.VerticalSlider(dl, brightnessRect, baseBright, FontAwesomeIcon.Sun, theme, opacity,
            interactive, out var brightReleased);
        if (brightReleased)
        {
            var zoom = 1.0f + Math.Clamp(newBright, 0f, 1f) * 0.5f;
            if (MathF.Abs(zoom - Plugin.Cfg.TextZoom) > 0.001f)
            {
                Plugin.Cfg.TextZoom = zoom;
                Plugin.Fonts.SetZoom(zoom);
                Plugin.Cfg.Save();
            }
        }

        var newVolume = ControlTile.VerticalSlider(dl, volumeRect, playback.Volume, FontAwesomeIcon.VolumeUp, theme,
            opacity, interactive, out _);
        if (MathF.Abs(newVolume - playback.Volume) > 0.0005f)
        {
            playback.Volume = newVolume;
        }

        if (playback.IsActive)
        {
            DrawNowPlaying(
                new Rect(new Vector2(left, sliderTop + sliderHeight + 14f * scale),
                    new Vector2(right, sliderTop + sliderHeight + 14f * scale + 78f * scale)), theme, scale, opacity,
                interactive);
        }
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
                var topBand = new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + 48f * scale));
                drag.Begin(topBand);
            }

            if (drag.Active)
            {
                var fraction = Math.Clamp(drag.Delta.Y / openDistance, 0f, 1f);
                offset.SnapTo(fraction);
                target = fraction;
            }

            if (drag.Released(out var totalDelta, out var velocity))
            {
                var commit = totalDelta.Y / openDistance > CommitFraction || velocity > fling;
                open = commit;
                target = commit ? 1f : 0f;
            }
        }
        else
        {
            var bottomZone = new Rect(new Vector2(screen.Min.X, screen.Max.Y - height * 0.18f), screen.Max);
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
