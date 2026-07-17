using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private static readonly Vector4 GreenControl = new(0.20f, 0.72f, 0.35f, 1f);
    private static readonly Vector4 RedControl = new(0.90f, 0.26f, 0.24f, 1f);
    private static readonly Vector4 GrayControl = new(0.55f, 0.56f, 0.60f, 1f);
    private static readonly float[] ReadoutScales = { 2.9f, 2.5f, 2.1f, 1.8f, 1.5f };

    private readonly List<double> swLaps;
    private bool swRunning;
    private double swAccumulatedMs;
    private long swStartTick;

    private double StopwatchElapsedMs =>
        swAccumulatedMs + (swRunning ? Environment.TickCount64 - swStartTick : 0);

    private void DrawStopwatch(Rect body, float scale)
    {
        var elapsed = StopwatchElapsedMs;
        var readoutCenter = new Vector2(body.Center.X, body.Min.Y + 66f * scale);
        DrawBigReadout(readoutCenter, FormatStopwatch(elapsed), body.Width - 32f * scale, scale);

        var radius = 34f * scale;
        var buttonsY = body.Min.Y + 148f * scale;
        var margin = 28f * scale;
        var leftCenter = new Vector2(body.Min.X + margin + radius, buttonsY);
        var rightCenter = new Vector2(body.Max.X - margin - radius, buttonsY);

        var canLap = swRunning || elapsed > 0;
        var leftLabel = swRunning ? Loc.T(L.Clock.Lap) : Loc.T(L.Clock.Reset);
        if (DrawCircleButton(leftCenter, radius, leftLabel, GrayControl, canLap))
        {
            if (swRunning)
            {
                swLaps.Add(elapsed);
            }
            else if (elapsed > 0)
            {
                ResetStopwatch();
            }
        }

        var rightLabel = swRunning ? Loc.T(L.Clock.Stop) : Loc.T(L.Clock.Start);
        var rightColor = swRunning ? RedControl : GreenControl;
        if (DrawCircleButton(rightCenter, radius, rightLabel, rightColor, true))
        {
            if (swRunning)
            {
                StopStopwatch();
            }
            else
            {
                StartStopwatch();
            }
        }

        var lapTop = buttonsY + radius + 20f * scale;
        var lapArea = new Rect(new Vector2(body.Min.X, lapTop), body.Max);
        DrawLapList(lapArea, elapsed, scale);
    }

    private void DrawLapList(Rect area, double elapsed, float scale)
    {
        if (swLaps.Count == 0 && !swRunning)
        {
            return;
        }

        using (AppSurface.Begin(area))
        {
            var count = swLaps.Count + (swRunning ? 1 : 0);
            var card = BeginRowCard(count, 40f, scale);
            var rowIndex = 0;
            if (swRunning)
            {
                var lastCumulative = swLaps.Count > 0 ? swLaps[swLaps.Count - 1] : 0.0;
                DrawLapRow(RowAt(card, rowIndex++, 40f, scale), swLaps.Count + 1, elapsed - lastCumulative, ui.Accent);
            }

            for (var index = swLaps.Count - 1; index >= 0; index--)
            {
                var previous = index > 0 ? swLaps[index - 1] : 0.0;
                DrawLapRow(RowAt(card, rowIndex++, 40f, scale), index + 1, swLaps[index] - previous, ui.MutedInk);
            }

            EndRowCard(card, 10f, scale);
        }
    }

    private void DrawLapRow(Rect row, int number, double splitMs, Vector4 valueColor)
    {
        var lineHeight = Typography.Measure("0", TextStyles.Subheadline).Y;
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - lineHeight * 0.5f), Loc.T(L.Clock.LapNumber, number),
            ui.MutedInk, TextStyles.Subheadline);
        var value = FormatStopwatch(splitMs);
        var size = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        Typography.Draw(new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f), value, valueColor,
            TextStyles.SubheadlineEmphasized);
    }

    private void StartStopwatch()
    {
        swRunning = true;
        swStartTick = Environment.TickCount64;
    }

    private void StopStopwatch()
    {
        swAccumulatedMs += Environment.TickCount64 - swStartTick;
        swRunning = false;
    }

    private void ResetStopwatch()
    {
        swRunning = false;
        swAccumulatedMs = 0;
        swStartTick = 0;
        swLaps.Clear();
    }

    private void DrawBigReadout(Vector2 center, string text, float maxWidth, float scale)
    {
        var chosen = ReadoutScales[ReadoutScales.Length - 1];
        for (var index = 0; index < ReadoutScales.Length; index++)
        {
            if (Typography.Measure(text, ReadoutScales[index], FontWeight.Regular).X <= maxWidth)
            {
                chosen = ReadoutScales[index];
                break;
            }
        }

        Typography.DrawCentered(center, text, ui.TitleInk, chosen, FontWeight.Regular);
    }

    private bool DrawCircleButton(Vector2 center, float radius, string label, Vector4 color, bool enabled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = enabled && ImGui.IsMouseHoveringRect(min, max);
        var shown = enabled ? color : Palette.WithAlpha(color, 0.4f);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(shown, hovered ? 0.34f : 0.22f)), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(shown), 40, 2f * ImGuiHelpers.GlobalScale);
        Typography.DrawCentered(drawList, center, label, shown, TextStyles.Subheadline.Scale, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string FormatStopwatch(double milliseconds)
    {
        var totalMs = (long)milliseconds;
        var centis = totalMs / 10 % 100;
        var totalSeconds = totalMs / 1000;
        var seconds = totalSeconds % 60;
        var minutes = totalSeconds / 60 % 60;
        var hours = totalSeconds / 3600;
        return hours > 0
            ? $"{hours}:{minutes:D2}:{seconds:D2}.{centis:D2}"
            : $"{minutes:D2}:{seconds:D2}.{centis:D2}";
    }
}
