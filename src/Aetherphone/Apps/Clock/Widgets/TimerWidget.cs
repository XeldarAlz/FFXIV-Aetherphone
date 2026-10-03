using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Clock.Widgets;

internal sealed class TimerWidget : IHomeWidget
{
    private const string TimerRoute = "clock.tab.timer";
    private const int DefaultSeconds = 300;
    private const int SampleSeconds = 600;
    private const int SampleRemaining = 263;
    private const int MainControl = 1;
    private const int PresetControlBase = 10;
    private const float CornerControlUnits = 28f;
    private const float PresetHeightUnits = 28f;
    private const float RingTextWidth = 1.36f;
    private const float RingCaptionGap = 2f;
    private const float RingControlFraction = 0.62f;
    private const int SecondsPerMinute = 60;

    private enum TimerPhase : byte
    {
        Idle,
        Running,
        Done,
    }

    private static readonly int[] PresetMinutes = { 1, 3, 5, 10, 30 };
    private static readonly string[] PresetLabels = { "1", "3", "5", "10", "30" };

    private readonly Configuration configuration;
    private readonly AlarmRinger ringer;
    private CachedText endsAt;

    public TimerWidget(Configuration configuration, AlarmRinger ringer)
    {
        this.configuration = configuration;
        this.ringer = ringer;
    }

    public string Id => "clock.timer";
    public string DisplayName => Loc.T(L.Clock.TimerTitle);
    public string Description => Loc.T(L.WidgetsTime.TimerDescription);
    public string AppId => "clock";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public WidgetRoute Target(in WidgetContext context) => WidgetRoute.Tab(AppId, TimerRoute);

    public float Relevance(string config)
    {
        var phase = Phase(DateTime.UtcNow, out var remaining);
        return phase switch
        {
            TimerPhase.Done when IsRinging || remaining > -SecondsPerMinute => 1f,
            TimerPhase.Running when remaining <= SecondsPerMinute => 0.95f,
            TimerPhase.Running when remaining <= 5 * SecondsPerMinute => 0.75f,
            TimerPhase.Running => 0.45f,
            _ => 0f,
        };
    }

    private int LastDuration => configuration.TimerDurationSeconds > 0 ? configuration.TimerDurationSeconds : DefaultSeconds;

    private bool IsRinging => ringer.IsRinging && ringer.Kind == AlarmRingKind.Timer;

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var utcNow = DateTime.UtcNow;
        var phase = Phase(utcNow, out var remaining);
        var duration = Math.Max(1, configuration.TimerDurationSeconds);
        var endUtc = configuration.TimerEndsAtUtc ?? utcNow;
        if (phase == TimerPhase.Idle && context.Preview)
        {
            phase = TimerPhase.Running;
            remaining = SampleRemaining;
            duration = SampleSeconds;
            endUtc = utcNow.AddSeconds(SampleRemaining);
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, phase, remaining, duration, endUtc);
            return;
        }

        DrawMedium(context, ink, phase, remaining, duration, endUtc);
    }

    private TimerPhase Phase(DateTime utcNow, out double remaining)
    {
        remaining = 0;
        if (IsRinging)
        {
            return TimerPhase.Done;
        }

        if (configuration.TimerEndsAtUtc is not { } endsAt)
        {
            return TimerPhase.Idle;
        }

        remaining = (endsAt - utcNow).TotalSeconds;
        return remaining > 0 ? TimerPhase.Running : TimerPhase.Done;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, TimerPhase phase, double remaining,
        int duration, DateTime endUtc)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var accent = AppAccents.For(AppId);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.Clock.TimerTitle, accent);
        var controlRadius = CornerControlUnits * 0.5f * scale;
        var controlCenter = new Vector2(content.Max.X - controlRadius, content.Min.Y + controlRadius -
                                                                        WidgetMetrics.RowGap * scale);
        DrawMainControl(context, ink, phase, controlCenter, CornerControlUnits, accent);

        var top = headerBottom + WidgetMetrics.Gutter * scale;
        var radius = MathF.Min(content.Width, content.Max.Y - top) * 0.5f;
        var center = new Vector2(content.Center.X, top + radius);
        DrawRing(context, ink, phase, remaining, duration, center, radius, accent, endUtc, true);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, TimerPhase phase, double remaining,
        int duration, DateTime endUtc)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var accent = AppAccents.For(AppId);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.Clock.TimerTitle, accent);
        var presetHeight = PresetHeightUnits * scale;
        var presetTop = content.Max.Y - presetHeight;
        var bodyTop = headerBottom + WidgetMetrics.Gutter * 0.5f * scale;
        var bodyBottom = presetTop - WidgetMetrics.Gutter * scale;
        var radius = MathF.Max(8f * scale, (bodyBottom - bodyTop) * 0.5f);
        var ringCenter = new Vector2(content.Max.X - radius, bodyTop + radius);
        DrawRing(context, ink, phase, remaining, duration, ringCenter, radius, accent, endUtc, false);
        var controlUnits = Math.Clamp(radius * 2f * RingControlFraction / scale, WidgetMetrics.ControlSmall,
            WidgetMetrics.ControlLarge);
        DrawMainControl(context, ink, phase, ringCenter, controlUnits, accent);

        var textWidth = MathF.Max(1f, ringCenter.X - radius - WidgetMetrics.Gutter * scale - content.Min.X);
        var digits = Digits(phase, remaining);
        var hero = WidgetText.FitStyle(digits, WidgetType.DisplayCompact, textWidth, true);
        var heroHeight = Typography.Measure(digits, hero).Y;
        var captionHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var stackTop = (bodyTop + bodyBottom) * 0.5f - (heroHeight + captionHeight) * 0.5f;
        WidgetText.Tabular(drawList, new Vector2(content.Min.X, stackTop), digits,
            phase == TimerPhase.Idle ? ink.Secondary : ink.Primary, hero);
        WidgetText.Draw(drawList, new Vector2(content.Min.X, stackTop + heroHeight), Caption(phase, endUtc, true),
            phase == TimerPhase.Done ? ink.Accent(accent) : ink.Secondary, WidgetType.Caption, textWidth);

        var gap = WidgetMetrics.RowGap * 2f * scale;
        var presetWidth = (content.Width - gap * (PresetMinutes.Length - 1)) / PresetMinutes.Length;
        for (var index = 0; index < PresetMinutes.Length; index++)
        {
            var left = content.Min.X + index * (presetWidth + gap);
            var rect = new Rect(new Vector2(left, presetTop), new Vector2(left + presetWidth, content.Max.Y));
            if (WidgetControls.Button(context, ink, PresetControlBase + index, rect, PresetLabels[index]))
            {
                Start(PresetMinutes[index] * SecondsPerMinute);
            }
        }
    }

    private void DrawRing(in WidgetContext context, in WidgetInk ink, TimerPhase phase, double remaining,
        int duration, Vector2 center, float radius, Vector4 accent, DateTime endUtc, bool withText)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var thickness = WidgetChrome.RingThickness(radius, scale);
        var ringRadius = radius - thickness * 0.5f;
        var fraction = phase switch
        {
            TimerPhase.Running => (float)(remaining / duration),
            TimerPhase.Done when IsRinging || remaining > -SecondsPerMinute => 1f,
            _ => 0f,
        };
        WidgetChrome.Ring(drawList, ink, center, ringRadius, thickness, fraction, accent);
        if (!withText)
        {
            return;
        }

        var innerWidth = (ringRadius - thickness) * RingTextWidth;
        var digits = Digits(phase, remaining);
        var style = WidgetText.FitStyle(digits, WidgetType.Title, innerWidth, true);
        var digitsHeight = Typography.Measure(digits, style).Y;
        var caption = Caption(phase, endUtc, false);
        var captionHeight = Typography.Measure(caption, WidgetType.Caption).Y;
        var top = center.Y - (digitsHeight + RingCaptionGap * scale + captionHeight) * 0.5f;
        WidgetText.TabularCentered(drawList, new Vector2(center.X, top + digitsHeight * 0.5f), digits,
            phase == TimerPhase.Idle ? ink.Secondary : ink.Primary, style, innerWidth);
        var fitted = WidgetText.Fit(caption, innerWidth, WidgetType.Caption, out var captionScale);
        var captionWidth = Typography.Measure(fitted, captionScale, WidgetType.Caption.Weight).X;
        Typography.Draw(drawList,
            new Vector2(center.X - captionWidth * 0.5f, top + digitsHeight + RingCaptionGap * scale), fitted,
            ink.Secondary, captionScale, WidgetType.Caption.Weight);
    }

    private void DrawMainControl(in WidgetContext context, in WidgetInk ink, TimerPhase phase, Vector2 center,
        float diameterUnits, Vector4 accent)
    {
        switch (phase)
        {
            case TimerPhase.Running:
                if (WidgetControls.Button(context, ink, MainControl, center, diameterUnits, FontAwesomeIcon.Times))
                {
                    Cancel();
                }

                return;
            case TimerPhase.Done:
                if (WidgetControls.Button(context, ink, MainControl, center, diameterUnits, FontAwesomeIcon.Stop,
                        accent))
                {
                    Cancel();
                }

                return;
            default:
                if (WidgetControls.Button(context, ink, MainControl, center, diameterUnits, FontAwesomeIcon.Play,
                        accent))
                {
                    Start(LastDuration);
                }

                return;
        }
    }

    private string Digits(TimerPhase phase, double remaining) => phase switch
    {
        TimerPhase.Running => TimeText.Duration((int)Math.Ceiling(remaining)),
        TimerPhase.Done => TimeText.Duration(0),
        _ => TimeText.Duration(LastDuration),
    };

    private string Caption(TimerPhase phase, DateTime endUtc, bool wide)
    {
        switch (phase)
        {
            case TimerPhase.Done:
                return Loc.T(L.Clock.TimerFinished);
            case TimerPhase.Idle:
                return Loc.T(wide ? L.WidgetsTime.PickMinutes : L.Clock.TimerTitle);
        }

        var end = endUtc.ToLocalTime();
        var key = TimeWidgetParts.MinuteKey(end);
        return endsAt.IsCurrent(key)
            ? endsAt.Value
            : endsAt.Store(key, Loc.T(L.WidgetsTime.EndsAt, TimeText.Clock(end)));
    }

    private void Start(int seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        if (IsRinging)
        {
            ringer.Stop();
        }

        configuration.TimerDurationSeconds = seconds;
        configuration.TimerEndsAtUtc = DateTime.UtcNow.AddSeconds(seconds);
        configuration.TimerNotified = false;
        configuration.Save();
    }

    private void Cancel()
    {
        if (IsRinging)
        {
            ringer.Stop();
        }

        configuration.TimerEndsAtUtc = null;
        configuration.TimerNotified = false;
        configuration.Save();
    }

    public void Dispose()
    {
    }
}
