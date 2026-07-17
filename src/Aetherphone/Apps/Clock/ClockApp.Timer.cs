using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private int pickerHours;
    private int pickerMinutes = 5;
    private int pickerSeconds;
    private bool timerPaused;
    private int pausedRemaining;

    private void DrawTimer(Rect body, float scale)
    {
        var endsAt = configuration.TimerEndsAtUtc;
        if (endsAt.HasValue)
        {
            var remaining = (int)Math.Ceiling((endsAt.Value - DateTime.UtcNow).TotalSeconds);
            if (remaining > 0)
            {
                DrawTimerActive(body, remaining, false, scale);
            }
            else
            {
                DrawTimerFinished(body, scale);
            }

            return;
        }

        if (timerPaused)
        {
            DrawTimerActive(body, pausedRemaining, true, scale);
            return;
        }

        DrawTimerIdle(body, scale);
    }

    private void DrawTimerIdle(Rect body, float scale)
    {
        var margin = 20f * scale;
        var left = body.Min.X + margin;
        var right = body.Max.X - margin;
        var stepTop = body.Min.Y + 44f * scale;
        var stepHeight = 56f * scale;
        var gap = 10f * scale;
        var columnWidth = (right - left - gap * 2f) / 3f;

        var hourRect = new Rect(new Vector2(left, stepTop), new Vector2(left + columnWidth, stepTop + stepHeight));
        var minuteRect = new Rect(new Vector2(hourRect.Max.X + gap, stepTop),
            new Vector2(hourRect.Max.X + gap + columnWidth, stepTop + stepHeight));
        var secondRect = new Rect(new Vector2(minuteRect.Max.X + gap, stepTop), new Vector2(right, stepTop + stepHeight));

        StepperField.Draw(ui, hourRect, pickerHours.ToString("D2"), scale, () => pickerHours = (pickerHours + 23) % 24,
            () => pickerHours = (pickerHours + 1) % 24);
        StepperField.Draw(ui, minuteRect, pickerMinutes.ToString("D2"), scale,
            () => pickerMinutes = (pickerMinutes + 59) % 60, () => pickerMinutes = (pickerMinutes + 1) % 60);
        StepperField.Draw(ui, secondRect, pickerSeconds.ToString("D2"), scale,
            () => pickerSeconds = (pickerSeconds + 59) % 60, () => pickerSeconds = (pickerSeconds + 1) % 60);

        var captionY = stepTop + stepHeight + 6f * scale;
        Typography.DrawCentered(new Vector2(hourRect.Center.X, captionY), Loc.T(L.Clock.Hours), ui.MutedInk,
            TextStyles.Caption1);
        Typography.DrawCentered(new Vector2(minuteRect.Center.X, captionY), Loc.T(L.Clock.Minutes), ui.MutedInk,
            TextStyles.Caption1);
        Typography.DrawCentered(new Vector2(secondRect.Center.X, captionY), Loc.T(L.Clock.Seconds), ui.MutedInk,
            TextStyles.Caption1);

        var total = TimerPickerSeconds();
        var radius = 40f * scale;
        var startCenter = new Vector2(body.Center.X, captionY + 60f * scale + radius);
        if (DrawCircleButton(startCenter, radius, Loc.T(L.Clock.Start), GreenControl, total > 0))
        {
            StartTimer(total);
        }
    }

    private void DrawTimerActive(Rect body, int remainingSeconds, bool paused, float scale)
    {
        var ringCenter = new Vector2(body.Center.X, body.Min.Y + 120f * scale);
        var radius = MathF.Min(body.Width * 0.34f, 100f * scale);
        var duration = Math.Max(1, configuration.TimerDurationSeconds);
        var fraction = Math.Clamp(remainingSeconds / (float)duration, 0f, 1f);
        DrawTimerRing(ringCenter, radius, fraction, FormatDuration(remainingSeconds), null, scale);

        var buttonRadius = 34f * scale;
        var buttonsY = ringCenter.Y + radius + 34f * scale;
        var margin = 28f * scale;
        var leftCenter = new Vector2(body.Min.X + margin + buttonRadius, buttonsY);
        var rightCenter = new Vector2(body.Max.X - margin - buttonRadius, buttonsY);

        if (DrawCircleButton(leftCenter, buttonRadius, Loc.T(L.Clock.Cancel), GrayControl, true))
        {
            CancelTimer();
        }

        if (paused)
        {
            if (DrawCircleButton(rightCenter, buttonRadius, Loc.T(L.Clock.Resume), GreenControl, true))
            {
                ResumeTimer();
            }
        }
        else if (DrawCircleButton(rightCenter, buttonRadius, Loc.T(L.Clock.Pause), ui.Accent, true))
        {
            PauseTimer(remainingSeconds);
        }
    }

    private void DrawTimerFinished(Rect body, float scale)
    {
        var ringCenter = new Vector2(body.Center.X, body.Min.Y + 120f * scale);
        var radius = MathF.Min(body.Width * 0.34f, 100f * scale);
        DrawTimerRing(ringCenter, radius, 0f, FormatDuration(0), Loc.T(L.Clock.TimerFinished), scale);

        var buttonRadius = 34f * scale;
        var center = new Vector2(body.Center.X, ringCenter.Y + radius + 34f * scale);
        if (DrawCircleButton(center, buttonRadius, Loc.T(L.Clock.Reset), GreenControl, true))
        {
            CancelTimer();
        }
    }

    private void DrawTimerRing(Vector2 center, float radius, float fraction, string big, string? small, float scale)
    {
        var thickness = 7f * scale;
        ProgressRing.Glow(center, radius, ui.Accent, 0.35f);
        ProgressRing.Track(center, radius, thickness, Palette.WithAlpha(ui.TitleInk, 0.12f));
        ProgressRing.Fill(center, radius, thickness, fraction, ui.Accent);
        ProgressRing.CenterValue(center, big, small, ui.TitleInk, ui.MutedInk, TextStyles.LargeTitle);
    }

    private int TimerPickerSeconds() => pickerHours * 3600 + pickerMinutes * 60 + pickerSeconds;

    private void StartTimer(int totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return;
        }

        configuration.TimerDurationSeconds = totalSeconds;
        configuration.TimerEndsAtUtc = DateTime.UtcNow.AddSeconds(totalSeconds);
        configuration.TimerNotified = false;
        timerPaused = false;
        configuration.Save();
    }

    private void PauseTimer(int remainingSeconds)
    {
        pausedRemaining = remainingSeconds;
        timerPaused = true;
        configuration.TimerEndsAtUtc = null;
        configuration.Save();
    }

    private void ResumeTimer()
    {
        configuration.TimerEndsAtUtc = DateTime.UtcNow.AddSeconds(pausedRemaining);
        configuration.TimerNotified = false;
        timerPaused = false;
        configuration.Save();
    }

    private void CancelTimer()
    {
        configuration.TimerEndsAtUtc = null;
        configuration.TimerNotified = false;
        timerPaused = false;
        configuration.Save();
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:D2}:{seconds:D2}" : $"{minutes:D2}:{seconds:D2}";
    }
}
