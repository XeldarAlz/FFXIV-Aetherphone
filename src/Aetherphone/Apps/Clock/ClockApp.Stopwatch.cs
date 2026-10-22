using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const float FaceMaxHeight = 270f;
    private const float FaceWidthFraction = 0.82f;
    private const float FaceDotsGap = 10f;
    private const float FaceDotRadius = 3.5f;
    private const float FaceDotSpacing = 12f;
    private const float ControlsGap = 18f;
    private const float ControlsSideInset = 8f;
    private const float LapRowHeight = 46f;
    private const float DialMinorTicks = 240f;
    private const float SubDialFraction = 0.24f;
    private const float SubDialOffset = 0.40f;
    private const float DialReadoutOffset = 0.36f;
    private const double MillisecondsPerSecond = 1000.0;

    private static readonly TextStyle[] ReadoutLadder =
    {
        TextStyles.WidgetDisplay, TextStyles.WidgetDisplayCompact, TextStyles.Title1,
    };

    private static readonly Vector4 LapHandBlue = new(0.04f, 0.52f, 1f, 1f);

    private readonly List<double> swLaps;
    private readonly List<string> swLapTexts = new();
    private readonly List<string> swLapNames = new();
    private string swCurrentLapName = string.Empty;
    private CultureInfo? swNamesCulture;
    private int swBestLap = -1;
    private int swWorstLap = -1;
    private bool swRunning;
    private bool swAnalog;
    private double swAccumulatedMs;
    private long swStartTick;
    private Spring swFaceBlend;

    private double StopwatchElapsedMs =>
        swAccumulatedMs + (swRunning ? Environment.TickCount64 - swStartTick : 0);

    private void DrawStopwatch(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var elapsed = StopwatchElapsedMs;
        EnsureLapNames();
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("clock.stopwatch"))
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            DrawStopwatchFace(elapsed, width, scale);
            DrawStopwatchControls(elapsed, width, scale);
            DrawLapList(elapsed, width, scale);
            ImGui.Dummy(new Vector2(0f, ClockArt.BottomPad * scale));
        }

        AppHeader.EndLargeTitle(in navBar, context, "clock.stopwatch.nav", Loc.T(L.Clock.TabStopwatch),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawStopwatchFace(double elapsed, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var faceHeight = MathF.Min(FaceMaxHeight * scale, width * FaceWidthFraction);
        var dotsHeight = (FaceDotsGap + FaceDotRadius * 2f) * scale;
        var face = new Rect(origin, origin + new Vector2(width, faceHeight));
        var drawList = ImGui.GetWindowDrawList();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var blend = Math.Clamp(swFaceBlend.Step(swAnalog ? 1f : 0f, Motion.Appear, delta), 0f, 1f);
        if (blend < 0.99f)
        {
            DrawDigitalReadout(drawList, face, elapsed, 1f - blend);
        }

        if (blend > 0.01f)
        {
            DrawAnalogDial(drawList, face, elapsed, blend, scale);
        }

        var dotsY = face.Max.Y + FaceDotsGap * scale + FaceDotRadius * scale;
        var spacing = FaceDotSpacing * scale;
        for (var dot = 0; dot < 2; dot++)
        {
            var active = (dot == 1) == swAnalog;
            var center = new Vector2(face.Center.X + (dot - 0.5f) * spacing, dotsY);
            drawList.AddCircleFilled(center, FaceDotRadius * scale,
                ImGui.GetColorU32(active ? ui.TitleInk : Palette.WithAlpha(ui.MutedInk, 0.5f)), 12);
        }

        var hovered = UiInteract.Hover(face.Min, face.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(face, Loc.T(L.Clock.SwitchFace));
        if (UiInteract.Click(face.Min, face.Max, hovered))
        {
            swAnalog = !swAnalog;
        }

        ClockArt.Advance(origin, width, faceHeight + dotsHeight, ControlsGap, scale);
    }

    private void DrawDigitalReadout(ImDrawListPtr drawList, Rect face, double elapsed, float alpha)
    {
        Split(elapsed, out var hours, out var minutes, out var seconds, out var centis);
        var style = ReadoutLadder[^1];
        var width = 0f;
        for (var index = 0; index < ReadoutLadder.Length; index++)
        {
            width = ClockArt.DurationWidth(hours, minutes, seconds, centis, true, ReadoutLadder[index]);
            if (width <= face.Width)
            {
                style = ReadoutLadder[index];
                break;
            }
        }

        width = ClockArt.DurationWidth(hours, minutes, seconds, centis, true, style);
        var height = Typography.LineHeight(style);
        ClockArt.DrawDuration(drawList, new Vector2(face.Center.X - width * 0.5f, face.Center.Y - height * 0.5f),
            hours, minutes, seconds, centis, true, style, ui.TitleInk with { W = ui.TitleInk.W * alpha });
    }

    private void DrawAnalogDial(ImDrawListPtr drawList, Rect face, double elapsed, float alpha, float scale)
    {
        var radius = face.Height * 0.5f - Metrics.Space.Xxs * scale;
        var center = face.Center;
        var tickCount = (int)DialMinorTicks;
        for (var tick = 0; tick < tickCount; tick++)
        {
            var angle = tick / DialMinorTicks * MathF.Tau - MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var second = tick % 4 == 0;
            var major = tick % 20 == 0;
            var inner = radius * (major ? 0.88f : second ? 0.91f : 0.94f);
            var ink = major ? ui.TitleInk : second ? ui.BodyInk : ui.MutedInk;
            drawList.AddLine(center + direction * inner, center + direction * radius,
                ImGui.GetColorU32(ink with { W = ink.W * alpha * (second ? 1f : 0.55f) }),
                (major ? 1.8f : 1f) * scale);
        }

        for (var label = 5; label <= 60; label += 5)
        {
            var angle = label / 60f * MathF.Tau - MathF.PI * 0.5f;
            var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.76f;
            Typography.DrawCentered(drawList, position, ClockArt.Plain(label),
                ui.TitleInk with { W = ui.TitleInk.W * alpha }, TextStyles.Subheadline);
        }

        DrawSubDial(drawList, center - new Vector2(0f, radius * SubDialOffset), radius * SubDialFraction, elapsed,
            alpha, scale);
        Split(elapsed, out var hours, out var minutes, out var seconds, out var centis);
        var readoutStyle = TextStyles.Headline;
        var readoutWidth = ClockArt.DurationWidth(hours, minutes, seconds, centis, true, readoutStyle);
        var readoutTop = center.Y + radius * DialReadoutOffset - Typography.LineHeight(readoutStyle) * 0.5f;
        ClockArt.DrawDuration(drawList, new Vector2(center.X - readoutWidth * 0.5f, readoutTop), hours, minutes,
            seconds, centis, true, readoutStyle, ui.TitleInk with { W = ui.TitleInk.W * alpha });

        var lapStart = swLaps.Count > 0 ? swLaps[^1] : 0d;
        if (swLaps.Count > 0)
        {
            DialHand(drawList, center, radius * 0.9f, SecondsFraction(elapsed - lapStart), LapHandBlue, alpha, scale);
        }

        DialHand(drawList, center, radius * 0.9f, SecondsFraction(elapsed), ui.Accent, alpha, scale);
        drawList.AddCircleFilled(center, 4f * scale, ImGui.GetColorU32(ui.Accent with { W = alpha }), 16);
        drawList.AddCircleFilled(center, 1.8f * scale, ImGui.GetColorU32(ui.Palette.BackdropBottom with { W = alpha }),
            12);
    }

    private void DrawSubDial(ImDrawListPtr drawList, Vector2 center, float radius, double elapsed, float alpha,
        float scale)
    {
        for (var tick = 0; tick < 30; tick++)
        {
            var angle = tick / 30f * MathF.Tau - MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var major = tick % 5 == 0;
            drawList.AddLine(center + direction * radius * (major ? 0.80f : 0.88f), center + direction * radius,
                ImGui.GetColorU32(ui.MutedInk with { W = ui.MutedInk.W * alpha }), scale);
        }

        var minutes = elapsed / MillisecondsPerSecond / 60d;
        var fraction = (float)(minutes % 30d / 30d);
        DialHand(drawList, center, radius * 0.86f, fraction, ui.Accent, alpha, scale);
    }

    private static float SecondsFraction(double milliseconds) =>
        (float)(milliseconds / MillisecondsPerSecond % 60d / 60d);

    private static void DialHand(ImDrawListPtr drawList, Vector2 center, float length, float fraction, Vector4 color,
        float alpha, float scale)
    {
        var angle = fraction * MathF.Tau - MathF.PI * 0.5f;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var packed = ImGui.GetColorU32(color with { W = color.W * alpha });
        drawList.AddLine(center - direction * length * 0.16f, center + direction * length, packed, 1.6f * scale);
    }

    private void DrawStopwatchControls(double elapsed, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var radius = ClockArt.ControlRadius * scale;
        var height = radius * 2f;
        var inset = ControlsSideInset * scale;
        var leftCenter = new Vector2(origin.X + inset + radius, origin.Y + radius);
        var rightCenter = new Vector2(origin.X + width - inset - radius, origin.Y + radius);
        var drawList = ImGui.GetWindowDrawList();
        var gapInk = ui.Palette.BackdropBottom;
        var canLap = swRunning || elapsed > 0d;
        var leftLabel = Loc.T(swRunning || elapsed <= 0d ? L.Clock.Lap : L.Clock.Reset);
        if (ClockArt.ControlButton(drawList, "clock.sw.left", leftCenter, radius, leftLabel, ClockArt.NeutralGray,
                gapInk, canLap))
        {
            if (swRunning)
            {
                AddLap(elapsed);
            }
            else
            {
                ResetStopwatch();
            }
        }

        var rightLabel = Loc.T(swRunning ? L.Clock.Stop : L.Clock.Start);
        var rightTint = swRunning ? theme.Danger : ClockArt.StartGreen;
        if (ClockArt.ControlButton(drawList, "clock.sw.right", rightCenter, radius, rightLabel, rightTint, gapInk,
                true))
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

        ClockArt.Advance(origin, width, height, ControlsGap, scale);
    }

    private void DrawLapList(double elapsed, float width, float scale)
    {
        var showCurrent = swRunning || elapsed > 0d;
        var count = swLaps.Count + (showCurrent ? 1 : 0);
        if (count == 0)
        {
            return;
        }

        var rowHeight = LapRowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var row = 0;
        FeedCell.Hairline(drawList, origin.X, origin.X + width, origin.Y, ui.Hairline);
        if (count > swLaps.Count)
        {
            var lapStart = swLaps.Count > 0 ? swLaps[^1] : 0d;
            var top = origin.Y;
            DrawLapName(drawList, origin.X, top, rowHeight, swCurrentLapName, ui.TitleInk);
            Split(elapsed - lapStart, out var hours, out var minutes, out var seconds, out var centis);
            var style = TextStyles.Body;
            var valueWidth = ClockArt.DurationWidth(hours, minutes, seconds, centis, true, style);
            ClockArt.DrawDuration(drawList,
                new Vector2(origin.X + width - valueWidth, top + (rowHeight - Typography.LineHeight(style)) * 0.5f),
                hours, minutes, seconds, centis, true, style, ui.TitleInk);
            row++;
            FeedCell.Hairline(drawList, origin.X, origin.X + width, origin.Y + row * rowHeight, ui.Hairline);
        }

        for (var index = swLaps.Count - 1; index >= 0; index--)
        {
            var top = origin.Y + row * rowHeight;
            if (ImGui.IsRectVisible(new Vector2(origin.X, top), new Vector2(origin.X + width, top + rowHeight)))
            {
                var ink = index == swBestLap ? ClockArt.StartGreen : index == swWorstLap ? theme.Danger : ui.TitleInk;
                DrawLapName(drawList, origin.X, top, rowHeight, swLapNames[index], ink);
                var value = swLapTexts[index];
                var valueWidth = WidgetText.TabularWidth(value, TextStyles.Body);
                WidgetText.Tabular(drawList,
                    new Vector2(origin.X + width - valueWidth,
                        top + (rowHeight - Typography.LineHeight(TextStyles.Body)) * 0.5f), value, ink,
                    TextStyles.Body);
            }

            row++;
            FeedCell.Hairline(drawList, origin.X, origin.X + width, origin.Y + row * rowHeight, ui.Hairline);
        }

        ClockArt.Advance(origin, width, row * rowHeight, 0f, scale);
    }

    private static void DrawLapName(ImDrawListPtr drawList, float left, float top, float rowHeight, string name,
        Vector4 ink)
    {
        Typography.Draw(drawList, new Vector2(left, top + (rowHeight - Typography.LineHeight(TextStyles.Body)) * 0.5f),
            name, ink, TextStyles.Body);
    }

    private void AddLap(double elapsed)
    {
        swLaps.Add(elapsed);
        var index = swLaps.Count - 1;
        Split(LapBoard.Split(swLaps, index), out var hours, out var minutes, out var seconds, out var centis);
        swLapTexts.Add(FormatSplit(hours, minutes, seconds, centis));
        swLapNames.Add(Loc.T(L.Clock.LapNumber, swLaps.Count));
        swCurrentLapName = Loc.T(L.Clock.LapNumber, swLaps.Count + 1);
        LapBoard.Extremes(swLaps, out swBestLap, out swWorstLap);
    }

    private void EnsureLapNames()
    {
        if (ReferenceEquals(swNamesCulture, Loc.Culture) && swCurrentLapName.Length > 0)
        {
            return;
        }

        swNamesCulture = Loc.Culture;
        for (var index = 0; index < swLapNames.Count; index++)
        {
            swLapNames[index] = Loc.T(L.Clock.LapNumber, index + 1);
        }

        swCurrentLapName = Loc.T(L.Clock.LapNumber, swLaps.Count + 1);
    }

    private static string FormatSplit(int hours, int minutes, int seconds, int centis)
    {
        var separator = Loc.Culture.NumberFormat.NumberDecimalSeparator;
        return hours > 0
            ? string.Concat(ClockArt.Plain(hours), ":", ClockArt.Pair(minutes), ":", ClockArt.Pair(seconds),
                separator, ClockArt.Pair(centis))
            : string.Concat(ClockArt.Pair(minutes), ":", ClockArt.Pair(seconds), separator, ClockArt.Pair(centis));
    }

    private static void Split(double milliseconds, out int hours, out int minutes, out int seconds, out int centis)
    {
        var total = (long)Math.Max(0d, milliseconds);
        centis = (int)(total / 10 % 100);
        var totalSeconds = total / 1000;
        seconds = (int)(totalSeconds % 60);
        minutes = (int)(totalSeconds / 60 % 60);
        hours = (int)(totalSeconds / 3600);
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
        swLapTexts.Clear();
        swLapNames.Clear();
        swCurrentLapName = Loc.T(L.Clock.LapNumber, 1);
        swBestLap = -1;
        swWorstLap = -1;
    }
}
