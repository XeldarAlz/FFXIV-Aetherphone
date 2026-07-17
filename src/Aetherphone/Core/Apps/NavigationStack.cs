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
    private readonly IAnalyticsService analytics;
    private readonly Stack<IPhoneApp> history = new();
    private Spring cover;
    private float coverTarget;
    private float coverSmoothTime;
    private IPhoneApp? current;
    private IPhoneApp? motionOver;
    private IPhoneApp? motionUnder;
    private ShellMotion motion = ShellMotion.None;
    private Rect? pendingOrigin;
    private Rect? motionOrigin;
    private DateTime appOpenedAt;
    private string? trackedAppId;

    public NavigationStack(IReadOnlyList<IPhoneApp> apps, IAnalyticsService analytics)
    {
        this.apps = apps;
        this.analytics = analytics;
    }

    public event Action<string>? AppOpened;
    public event Action<string>? ReturningHome;
    public IPhoneApp? Current => current;
    public bool AtHome => current is null;
    public bool IsTransitioning => motion != ShellMotion.None;
    public ShellMotion Motion => motion;
    public float MotionProgress => cover.Value;
    public IPhoneApp MotionOver => motionOver!;
    public IPhoneApp? MotionUnder => motionUnder;
    public Rect? MotionOrigin => motionOrigin;

    public void Advance(float deltaSeconds)
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        cover.Step(coverTarget, coverSmoothTime, deltaSeconds);
        if (MathF.Abs(cover.Value - coverTarget) <= TransitionTiming.MotionSettleEpsilon)
        {
            cover.SnapTo(coverTarget);
            FinalizeMotion();
        }
    }

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
        analytics.Track(AnalyticsEvents.AppOpen(app.Id, source));
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
        cover.SnapTo(0f);
        coverTarget = 1f;
        coverSmoothTime = under is null ? TransitionTiming.ZoomPresentSmoothTime : TransitionTiming.PresentSmoothTime;
    }

    private void BeginDismiss(IPhoneApp over, IPhoneApp? under)
    {
        motion = ShellMotion.Dismiss;
        motionOver = over;
        motionUnder = under;
        motionOrigin = null;
        cover.SnapTo(1f);
        coverTarget = 0f;
        coverSmoothTime = under is null ? TransitionTiming.ZoomDismissSmoothTime : TransitionTiming.DismissSmoothTime;
    }

    private void ReverseToPresent()
    {
        if (motionUnder is not null)
        {
            history.Push(motionUnder);
        }

        current = motionOver;
        motion = ShellMotion.Present;
        coverTarget = 1f;
        coverSmoothTime = motionUnder is null ? TransitionTiming.ZoomPresentSmoothTime : TransitionTiming.PresentSmoothTime;
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
        coverTarget = 0f;
        coverSmoothTime = motionUnder is null ? TransitionTiming.ZoomDismissSmoothTime : TransitionTiming.DismissSmoothTime;
    }

    private void SettleAny()
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        cover.SnapTo(motion == ShellMotion.Present ? 1f : 0f);
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
                analytics.Track(AnalyticsEvents.AppClose(trackedAppId, durationMs));
                trackedAppId = null;
            }
        }

        motion = ShellMotion.None;
        motionOver = null;
        motionUnder = null;
        motionOrigin = null;
    }
}
