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
    private Spring cover;
    private float targetCover;
    private IPhoneApp? current;
    private IPhoneApp? motionOver;
    private IPhoneApp? motionUnder;
    private ShellMotion motion = ShellMotion.None;
    private DateTime appOpenedAt;
    private string? trackedAppId;

    public NavigationStack(IReadOnlyList<IPhoneApp> apps)
    {
        this.apps = apps;
    }

    public event Action<string>? AppOpened;
    public IPhoneApp? Current => current;
    public bool AtHome => current is null;
    public bool IsTransitioning => motion != ShellMotion.None;
    public ShellMotion Motion => motion;
    public float MotionProgress => cover.Value;
    public IPhoneApp MotionOver => motionOver!;
    public IPhoneApp? MotionUnder => motionUnder;

    public void Advance(float deltaSeconds)
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        var smoothTime = motion == ShellMotion.Present
            ? TransitionTiming.PresentSmoothTime
            : TransitionTiming.DismissSmoothTime;

        cover.Step(targetCover, smoothTime, deltaSeconds);

        if (!cover.IsResting(targetCover, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
        {
            return;
        }

        cover.SnapTo(targetCover);
        FinalizeMotion();
    }

    public void OpenApp(IPhoneApp app) => OpenApp(app, AppOpenSource.Home);

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
        BeginDismiss(leaving, null);
    }

    private void BeginPresent(IPhoneApp over, IPhoneApp? under)
    {
        motion = ShellMotion.Present;
        motionOver = over;
        motionUnder = under;
        cover.SnapTo(0f);
        targetCover = 1f;
    }

    private void BeginDismiss(IPhoneApp over, IPhoneApp? under)
    {
        motion = ShellMotion.Dismiss;
        motionOver = over;
        motionUnder = under;
        cover.SnapTo(1f);
        targetCover = 0f;
    }

    private void ReverseToPresent()
    {
        if (motionUnder is not null)
        {
            history.Push(motionUnder);
        }

        current = motionOver;
        motion = ShellMotion.Present;
        targetCover = 1f;
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
        targetCover = 0f;
    }

    private void SettleAny()
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        cover.SnapTo(targetCover);
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
    }
}
