using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class AlarmOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private static readonly Vector4 Orange = new(1.00f, 0.58f, 0.00f, 1f);
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);
    private static readonly Vector4 Scrim = new(0.02f, 0.03f, 0.05f, 0.88f);
    private static readonly Vector4 StopFill = new(1f, 1f, 1f, 0.16f);
    private readonly AlarmRinger ringer;
    private Spring presence;
    private float clock;

    public AlarmOverlay(AlarmRinger ringer)
    {
        this.ringer = ringer;
    }

    public bool IsRinging => ringer.IsRinging;

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var ringing = ringer.IsRinging;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        presence.Step(ringing ? 1f : 0f, Motion.Appear, delta);
        if (presence.Value <= 0.01f)
        {
            if (!ringing)
            {
                presence.SnapTo(0f);
            }

            return;
        }

        clock += delta;
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##alarmRinging", screen.Size, false, OverlayFlags))
        {
            DrawContent(screen, theme, Math.Clamp(presence.Value, 0f, 1f), ringing);
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, float reveal, bool live)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var alpha = Math.Clamp(reveal * 1.4f, 0f, 1f);
        var rise = (1f - reveal) * 26f * scale;
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(Scrim with { W = Scrim.W * alpha }));
        var centerX = screen.Center.X;
        var timer = ringer.Kind == AlarmRingKind.Timer;
        DrawBell(drawList, new Vector2(centerX, screen.Min.Y + 120f * scale + rise), timer, alpha);
        var headline = timer ? Loc.T(L.Clock.TimerTitle) : TimeText.Clock(DateTime.Now);
        var caption = timer
            ? Loc.T(L.Clock.TimerFinished)
            : ringer.Label.Length > 0 ? ringer.Label : Loc.T(L.Clock.Alarm);
        var headlineY = screen.Min.Y + 200f * scale + rise;
        Typography.DrawCentered(drawList, new Vector2(centerX, headlineY), headline, Ink with { W = alpha },
            TextStyles.WidgetDisplay);
        var captionWidth = screen.Width - Metrics.Space.Xl * 2f * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, headlineY + 46f * scale),
            Typography.FitText(caption, captionWidth, TextStyles.Title3), Palette.WithAlpha(theme.TextMuted, alpha),
            TextStyles.Title3);
        var buttonWidth = MathF.Min(screen.Width - Metrics.Space.Xl * 2f * scale, 240f * scale);
        var buttonHeight = 52f * scale;
        if (ringer.CanSnooze)
        {
            var snoozeCenter = new Vector2(centerX, screen.Max.Y - 170f * scale + rise);
            if (Pill(drawList, snoozeCenter, buttonWidth, buttonHeight, Orange, Loc.T(L.Clock.Snooze),
                    "##alarmSnooze", alpha, live))
            {
                ringer.Snooze(DateTime.UtcNow);
            }
        }

        var stopCenter = new Vector2(centerX, screen.Max.Y - 96f * scale + rise);
        if (Pill(drawList, stopCenter, buttonWidth, buttonHeight, StopFill, Loc.T(L.Clock.Stop), "##alarmStop",
                alpha, live))
        {
            ringer.Stop();
        }
    }

    private void DrawBell(ImDrawListPtr drawList, Vector2 center, bool timer, float alpha)
    {
        var scale = UiScale.Current;
        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 2.6f);
        var radius = 34f * scale;
        drawList.AddCircle(center, radius + (6f + 7f * pulse) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(Orange, (0.30f + 0.25f * pulse) * alpha)), 64, 2.5f * scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(Orange, alpha)), 64);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = IconGlyph.Of(timer ? FontAwesomeIcon.HourglassHalf : FontAwesomeIcon.Bell);
            var size = ImGui.CalcTextSize(glyph);
            drawList.AddText(center - size * 0.5f, ImGui.GetColorU32(Ink with { W = alpha }), glyph);
        }
    }

    private static bool Pill(ImDrawListPtr drawList, Vector2 center, float width, float height, Vector4 fill,
        string label, string key, float alpha, bool live)
    {
        var half = new Vector2(width, height) * 0.5f;
        var rect = new Rect(center - half, center + half);
        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        var pose = MotionButton.Animate(rect, key, hovered, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var face = pose.Face;
        var color = Palette.Mix(fill, Ink, 0.12f * pose.Hover);
        Squircle.Fill(drawList, face.Min, face.Max, face.Height * 0.5f,
            ImGui.GetColorU32(color with { W = color.W * alpha }));
        Typography.DrawCentered(drawList, face.Center, label, Ink with { W = alpha }, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
