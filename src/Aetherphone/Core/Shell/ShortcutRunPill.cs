using Aetherphone.Core.Animation;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell;

internal sealed class ShortcutRunPill : IDisposable
{
    private const float PresenceSmoothTime = 0.16f;
    private const float TopGap = 12f;
    private const float SideMargin = 14f;
    private const float PadX = 13f;
    private const float PadY = 7f;
    private const float IconGap = 8f;
    private const float LabelGap = 10f;
    private const float SlideDistance = 10f;
    private const float OutcomeSeconds = 2.6f;
    private const float MinTitleWidth = 40f;

    private readonly ShortcutRunner runner;
    private Spring presence;
    private Rect stopBounds;
    private bool stopLive;
    private float outcomeClock;
    private bool hasOutcome;
    private ShortcutRunOutcome outcome;
    private string outcomeName = string.Empty;
    private Vector4 outcomeAccent;

    public ShortcutRunPill(ShortcutRunner runner)
    {
        this.runner = runner;
        runner.Finished += OnFinished;
    }

    public bool IsVisible => runner.IsRunning || outcomeClock > 0f || presence.Value > 0.02f;

    public bool CapturesPointer() => stopLive && UiInteract.Hover(stopBounds.Min, stopBounds.Max);

    public void Draw(Rect screen, PhoneTheme theme, float delta, bool suppressed)
    {
        if (outcomeClock > 0f)
        {
            outcomeClock -= delta;
        }

        var view = runner.Snapshot();
        var wanted = !suppressed && (view.IsRunning || outcomeClock > 0f);
        if (!wanted && !hasOutcome)
        {
            presence.SnapTo(0f);
        }
        else
        {
            presence.Step(wanted ? 1f : 0f, PresenceSmoothTime, delta);
        }

        var alpha = Math.Clamp(presence.Value, 0f, 1f);
        if (alpha < 0.02f)
        {
            stopLive = false;
            return;
        }

        DrawPill(screen, theme, view, alpha);
    }

    private void DrawPill(Rect screen, PhoneTheme theme, in ShortcutRunView view, float alpha)
    {
        var scale = UiScale.Current;
        var running = view.IsRunning;
        var accent = running ? view.Accent : outcomeAccent;
        var failed = !running && outcome != ShortcutRunOutcome.Completed;
        var tone = failed ? theme.Danger : accent;
        var title = running ? ShortcutRunText.Name(view.Name) : ShortcutRunText.Outcome(outcome, outcomeName);
        var status = running ? ShortcutRunText.Status(view) : string.Empty;

        var titleHeight = Typography.Measure(title, TextStyles.FootnoteEmphasized).Y;
        var iconSize = titleHeight * 0.98f;
        var stopRadius = running ? titleHeight * 0.66f : 0f;
        var statusWidth = status.Length > 0 ? Typography.Measure(status, TextStyles.Caption1).X : 0f;
        var trailing = running ? LabelGap * scale + statusWidth + IconGap * scale + stopRadius * 2f : 0f;
        var fixedWidth = PadX * 2f * scale + iconSize + IconGap * scale + trailing;
        var available = screen.Width - SideMargin * 2f * scale;
        var fitted = Typography.FitText(title, MathF.Max(MinTitleWidth * scale, available - fixedWidth),
            TextStyles.FootnoteEmphasized);
        var titleWidth = Typography.Measure(fitted, TextStyles.FootnoteEmphasized).X;
        var width = MathF.Min(fixedWidth + titleWidth, available);
        var height = titleHeight + PadY * 2f * scale;

        var island = StatusBar.BaseIsland(screen);
        var top = island.Max.Y + TopGap * scale - SlideDistance * scale * (1f - alpha);
        var bounds = new Rect(new Vector2(screen.Center.X - width * 0.5f, top),
            new Vector2(screen.Center.X + width * 0.5f, top + height));
        var rounding = height * 0.5f;
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        Elevation.Draw(drawList, bounds.Min, bounds.Max, rounding, scale, 4f, 2f, 0.18f * alpha);
        drawList.AddRectFilled(bounds.Min, bounds.Max,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Glass, alpha * theme.Glass.W)), rounding);
        DrawProgress(drawList, bounds, rounding, view, tone, alpha, running);
        drawList.AddRect(bounds.Min, bounds.Max, ImGui.GetColorU32(Palette.WithAlpha(tone, 0.42f * alpha)), rounding,
            ImDrawFlags.RoundCornersAll, Metrics.Stroke.Thin * scale);

        var iconCenter = new Vector2(bounds.Min.X + PadX * scale + iconSize * 0.5f, bounds.Center.Y);
        ProgressRing.CenterIcon(drawList, iconCenter, Glyph(view, running, failed),
            Palette.WithAlpha(tone, alpha), iconSize);
        var titleLeft = iconCenter.X + iconSize * 0.5f + IconGap * scale;
        Typography.Draw(drawList, new Vector2(titleLeft, bounds.Center.Y - titleHeight * 0.5f), fitted,
            Palette.WithAlpha(ChromeInk.TextStrong, alpha), TextStyles.FootnoteEmphasized);
        if (status.Length > 0)
        {
            var statusSize = Typography.Measure(status, TextStyles.Caption1);
            Typography.Draw(drawList,
                new Vector2(titleLeft + titleWidth + LabelGap * scale, bounds.Center.Y - statusSize.Y * 0.5f), status,
                Palette.WithAlpha(ChromeInk.TextSecondary, alpha), TextStyles.Caption1);
        }

        DrawStop(drawList, bounds, stopRadius, alpha, running, scale);
        drawList.PopClipRect();
    }

    private static void DrawProgress(ImDrawListPtr drawList, in Rect bounds, float rounding, in ShortcutRunView view,
        Vector4 tone, float alpha, bool running)
    {
        if (!running || view.StepCount < 2)
        {
            return;
        }

        drawList.PushClipRect(bounds.Min, new Vector2(bounds.Min.X + bounds.Width * view.Progress, bounds.Max.Y), true);
        drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(Palette.WithAlpha(tone, 0.16f * alpha)),
            rounding);
        drawList.PopClipRect();
    }

    private void DrawStop(ImDrawListPtr drawList, in Rect bounds, float radius, float alpha, bool running, float scale)
    {
        if (!running)
        {
            stopLive = false;
            return;
        }

        var center = new Vector2(bounds.Max.X - PadX * scale - radius, bounds.Center.Y);
        var corner = new Vector2(radius, radius);
        stopBounds = new Rect(center - corner, center + corner);
        stopLive = true;
        var hovered = UiInteract.Hover(stopBounds.Min, stopBounds.Max);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ChromeInk.TextStrong, (hovered ? 0.26f : 0.14f) * alpha)), 24);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Times,
            Palette.WithAlpha(ChromeInk.TextStrong, alpha), radius * 0.92f);
        if (UiInteract.HoverClickCircle(center, radius))
        {
            runner.Cancel();
        }
    }

    private static FontAwesomeIcon Glyph(in ShortcutRunView view, bool running, bool failed)
    {
        if (running)
        {
            return view.Holding ? FontAwesomeIcon.HourglassHalf : FontAwesomeIcon.Bolt;
        }

        return failed ? FontAwesomeIcon.ExclamationCircle : FontAwesomeIcon.Check;
    }

    private void OnFinished(ShortcutRunView view, ShortcutRunOutcome finished)
    {
        if (finished == ShortcutRunOutcome.Cancelled)
        {
            hasOutcome = false;
            return;
        }

        hasOutcome = true;
        outcome = finished;
        outcomeName = view.Name;
        outcomeAccent = view.Accent;
        outcomeClock = OutcomeSeconds;
    }

    public void Dispose() => runner.Finished -= OnFinished;
}
