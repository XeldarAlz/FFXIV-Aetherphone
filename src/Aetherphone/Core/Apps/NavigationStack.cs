using Aetherphone.Core.Analytics;
using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Apps;

internal enum ShellMotion
{
    None,
    Present,
    Dismiss
}

internal sealed class NavigationStack : INavigator
{
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly Stack<IPhoneApp> history = new();
    private float phase;
    private float cover;
    private float coverDuration;
    private IPhoneApp? current;
    private IPhoneApp? motionOver;
    private IPhoneApp? motionUnder;
    private ShellMotion motion = ShellMotion.None;
    private Rect? pendingOrigin;
    private Rect? motionOrigin;
    private DateTime appOpenedAt;
    private string? trackedAppId;

    public NavigationStack(IReadOnlyList<IPhoneApp> apps)
    {
        this.apps = apps;
    }

    public event Action<string>? AppOpened;
    public event Action<string>? ReturningHome;
    public IPhoneApp? Current => current;
    public bool AtHome => current is null;
    public bool IsTransitioning => motion != ShellMotion.None;
    public ShellMotion Motion => motion;
    public float MotionProgress => cover;
    public IPhoneApp MotionOver => motionOver!;
    public IPhoneApp? MotionUnder => motionUnder;
    public Rect? MotionOrigin => motionOrigin;

    public void Advance(float deltaSeconds)
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        var step = coverDuration <= 0.0001f ? 1f : deltaSeconds / coverDuration;
        phase = MathF.Min(1f, phase + step);
        cover = CoverFromPhase();

        if (phase < 1f)
        {
            return;
        }

        cover = motion == ShellMotion.Present ? 1f : 0f;
        FinalizeMotion();
    }

    private float CoverFromPhase() =>
        motion == ShellMotion.Present ? Easing.EaseOutQuint(phase) : 1f - Easing.EaseOutQuint(phase);

    private static float InvertEaseOutQuint(float value) =>
        1f - MathF.Pow(1f - Math.Clamp(value, 0f, 1f), 0.2f);

    public void OpenApp(IPhoneApp app) => OpenApp(app, AppOpenSource.Home);

    public void OpenAppFrom(IPhoneApp app, string source, Rect origin)
    {
        pendingOrigin = origin;
        OpenApp(app, source);
        pendingOrigin = null;
    }

    public void OpenApp(IPhoneApp app, string source)
    {
        if (motion == ShellMotion.None && ReferenceEquals(current, app))
        {
            return;
        }

        if (motion == ShellMotion.Dismiss && ReferenceEquals(motionOver, app))
        {
            ReverseToPresent();
            return;
        }

        SettleAny();
        var under = current;

        if (under is not null)
        {
            history.Push(under);
        }

        current = app;
        app.OnOpened();
        appOpenedAt = DateTime.UtcNow;
        trackedAppId = app.Id;
        Plugin.Analytics.Track(AnalyticsEvents.AppOpen(app.Id, source));
        AppOpened?.Invoke(app.Id);
        BeginPresent(app, under);
    }

    public void Open(string appId) => Open(appId, AppOpenSource.CrossApp);

    public void Open(string appId, string source)
    {
        if (current?.Id == appId && motion == ShellMotion.None)
        {
            return;
        }

        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                OpenApp(apps[index], source);
                return;
            }
        }
    }

    public void Back()
    {
        if (motion == ShellMotion.Present && ReferenceEquals(motionOver, current))
        {
            ReverseToDismiss();
            return;
        }

        if (current is null)
        {
            return;
        }

        SettleAny();
        var leaving = current;
        var under = history.Count > 0 ? history.Pop() : null;
        current = under;
        under?.OnOpened();
        if (under is null)
        {
            ReturningHome?.Invoke(leaving.Id);
        }

        BeginDismiss(leaving, under);
    }

    public void GoHome()
    {
        SettleAny();

        if (current is null)
        {
            return;
        }

        var leaving = current;
        history.Clear();
        current = null;
        ReturningHome?.Invoke(leaving.Id);
        BeginDismiss(leaving, null);
    }

    private void BeginPresent(IPhoneApp over, IPhoneApp? under)
    {
        motion = ShellMotion.Present;
        motionOver = over;
        motionUnder = under;
        motionOrigin = under is null ? pendingOrigin : null;
        phase = 0f;
        cover = 0f;
        coverDuration = under is null ? TransitionTiming.ZoomPresentDuration : TransitionTiming.PresentDuration;
    }

    private void BeginDismiss(IPhoneApp over, IPhoneApp? under)
    {
        motion = ShellMotion.Dismiss;
        motionOver = over;
        motionUnder = under;
        motionOrigin = null;
        phase = 0f;
        cover = 1f;
        coverDuration = under is null ? TransitionTiming.ZoomDismissDuration : TransitionTiming.DismissDuration;
    }

    private void ReverseToPresent()
    {
        if (motionUnder is not null)
        {
            history.Push(motionUnder);
        }

        current = motionOver;
        motion = ShellMotion.Present;
        phase = InvertEaseOutQuint(cover);
        coverDuration = motionUnder is null ? TransitionTiming.ZoomPresentDuration : TransitionTiming.PresentDuration;
    }

    private void ReverseToDismiss()
    {
        var under = motionUnder;

        if (under is not null && history.Count > 0 && ReferenceEquals(history.Peek(), under))
        {
            history.Pop();
        }

        current = under;
        motion = ShellMotion.Dismiss;
        phase = InvertEaseOutQuint(1f - cover);
        coverDuration = motionUnder is null ? TransitionTiming.ZoomDismissDuration : TransitionTiming.DismissDuration;
    }

    private void SettleAny()
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        phase = 1f;
        cover = motion == ShellMotion.Present ? 1f : 0f;
        FinalizeMotion();
    }

    private void FinalizeMotion()
    {
        if (motion == ShellMotion.Present)
        {
            motionUnder?.OnClosed();
        }
        else if (motion == ShellMotion.Dismiss)
        {
            motionOver?.OnClosed();
            if (trackedAppId is not null && string.Equals(trackedAppId, motionOver?.Id, StringComparison.Ordinal))
            {
                var durationMs = (DateTime.UtcNow - appOpenedAt).TotalMilliseconds;
                Plugin.Analytics.Track(AnalyticsEvents.AppClose(trackedAppId, durationMs));
                trackedAppId = null;
            }
        }

        motion = ShellMotion.None;
        motionOver = null;
        motionUnder = null;
        motionOrigin = null;
    }
}
