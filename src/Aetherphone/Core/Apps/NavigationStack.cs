using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Apps;

internal enum ShellMotion
{
    None,
    Present,
    Dismiss
}

internal sealed class NavigationStack : INavigator
{
    private const long ResumeWindowMilliseconds = 10 * 60 * 1000;

    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly AppInstaller installer;
    private readonly SuspensionGate suspensions;
    private readonly Stack<IPhoneApp> history = new();
    private readonly Dictionary<string, long> closedAtTicks = new(StringComparer.Ordinal);
    private readonly HashSet<string> forgetOnClose = new(StringComparer.Ordinal);
    private bool resumingOpen;
    private bool scrubbing;
    private Spring cover;
    private Spring driftX;
    private Spring driftY;
    private float coverTarget;
    private float coverSmoothTime;
    private IPhoneApp? current;
    private IPhoneApp? motionOver;
    private IPhoneApp? motionUnder;
    private ShellMotion motion = ShellMotion.None;
    private Rect? pendingOrigin;
    private LaunchOrigin pendingOriginKind;
    private Rect? motionOrigin;
    private LaunchOrigin motionOriginKind;

    public NavigationStack(IReadOnlyList<IPhoneApp> apps, AppInstaller installer, SuspensionGate suspensions)
    {
        this.apps = apps;
        this.installer = installer;
        this.suspensions = suspensions;
    }

    public event Action<string>? AppOpened;
    public event Action<string>? ReturningHome;
    public IPhoneApp? Current => current;
    public bool AtHome => current is null;
    public bool IsTransitioning => motion != ShellMotion.None;
    public bool Scrubbing => scrubbing;
    public bool CanGoHome => motion == ShellMotion.Present || scrubbing ||
                             (current is not null && motion == ShellMotion.None);
    public ShellMotion Motion => motion;
    public float MotionProgress => cover.Value;
    public Vector2 MotionDrift => new(driftX.Value, driftY.Value);
    public IPhoneApp MotionOver => motionOver!;
    public IPhoneApp? MotionUnder => motionUnder;
    public Rect? MotionOrigin => motionOrigin;
    public LaunchOrigin MotionOriginKind => motionOriginKind;

    public void Advance(float deltaSeconds)
    {
        if (motion == ShellMotion.None || scrubbing)
        {
            return;
        }

        var step = MathF.Min(deltaSeconds, TransitionTiming.MotionFrameSeconds);
        cover.Step(coverTarget, coverSmoothTime, step);
        driftX.Step(0f, coverSmoothTime, step);
        driftY.Step(0f, coverSmoothTime, step);
        if (MathF.Abs(cover.Value - coverTarget) <= TransitionTiming.MotionSettleEpsilon)
        {
            cover.SnapTo(coverTarget);
            FinalizeMotion();
        }
    }

    public void OpenAppFrom(IPhoneApp app, Rect origin, LaunchOrigin kind)
    {
        pendingOrigin = origin;
        pendingOriginKind = kind;
        OpenApp(app);
        pendingOrigin = null;
        pendingOriginKind = LaunchOrigin.Icon;
    }

    public void OpenApp(IPhoneApp app)
    {
        if (suspensions.Blocks(app.Id))
        {
            suspensions.ReportBlocked();
            return;
        }

        if (motion == ShellMotion.None && ReferenceEquals(current, app))
        {
            NotifyOpened(app);
            return;
        }

        if (motion == ShellMotion.Dismiss && ReferenceEquals(motionOver, app))
        {
            ReverseToPresent();
            NotifyOpened(app);
            return;
        }

        SettleAny();
        var under = current;

        if (under is not null)
        {
            history.Push(under);
        }

        current = app;
        NotifyOpened(app);
        BeginPresent(app, under);
    }

    private void NotifyOpened(IPhoneApp app)
    {
        forgetOnClose.Remove(app.Id);
        resumingOpen = TryResume(app, requireRecentClose: true);
        if (!resumingOpen)
        {
            app.OnOpened();
        }

        AppOpened?.Invoke(app.Id);
        UiFeedback.Play(UiSound.AppOpen);
    }

    private bool TryResume(IPhoneApp app, bool requireRecentClose)
    {
        if (app is not IResumableApp resumable)
        {
            return false;
        }

        if (requireRecentClose)
        {
            if (!closedAtTicks.TryGetValue(app.Id, out var closedAt) ||
                Environment.TickCount64 - closedAt > ResumeWindowMilliseconds)
            {
                return false;
            }
        }

        resumable.OnResumed();
        return true;
    }

    public bool IsAvailable(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                return apps[index].IsAvailable;
            }
        }

        return false;
    }

    public void CollectOpen(List<IPhoneApp> results)
    {
        results.Clear();
        if (current is not null)
        {
            results.Add(current);
        }

        var now = Environment.TickCount64;
        var sortedFrom = results.Count;
        foreach (var entry in closedAtTicks)
        {
            if (now - entry.Value > ResumeWindowMilliseconds)
            {
                continue;
            }

            var app = FindApp(entry.Key);
            if (app is not IResumableApp || ReferenceEquals(app, current) || !app.IsAvailable ||
                !installer.IsInstalled(app.Id) || suspensions.Blocks(app.Id))
            {
                continue;
            }

            var insertIndex = results.Count;
            while (insertIndex > sortedFrom && closedAtTicks[results[insertIndex - 1].Id] < entry.Value)
            {
                insertIndex--;
            }

            results.Insert(insertIndex, app);
        }
    }

    public void Forget(string appId)
    {
        closedAtTicks.Remove(appId);
        RemoveFromHistory(appId);
        if (current is { } leaving && string.Equals(leaving.Id, appId, StringComparison.Ordinal))
        {
            forgetOnClose.Add(appId);
            GoHome();
            SettleAny();
        }
    }

    public void ForgetAll()
    {
        closedAtTicks.Clear();
        history.Clear();
        if (current is { } leaving)
        {
            forgetOnClose.Add(leaving.Id);
            GoHome();
            SettleAny();
        }
    }

    public void OpenSettled(string appId)
    {
        if (!installer.IsInstalled(appId))
        {
            return;
        }

        var app = FindApp(appId);
        if (app is null || !app.IsAvailable)
        {
            return;
        }

        OpenApp(app);
        SettleAny();
    }

    private IPhoneApp? FindApp(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                return apps[index];
            }
        }

        return null;
    }

    private void RemoveFromHistory(string appId)
    {
        if (history.Count == 0)
        {
            return;
        }

        var retained = new IPhoneApp[history.Count];
        var retainedCount = 0;
        while (history.Count > 0)
        {
            var entry = history.Pop();
            if (!string.Equals(entry.Id, appId, StringComparison.Ordinal))
            {
                retained[retainedCount] = entry;
                retainedCount++;
            }
        }

        for (var index = retainedCount - 1; index >= 0; index--)
        {
            history.Push(retained[index]);
        }
    }

    public void Open(string appId)
    {
        if (!installer.IsInstalled(appId))
        {
            return;
        }

        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId && apps[index].IsAvailable)
            {
                OpenApp(apps[index]);
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
        if (under is not null && !TryResume(under, requireRecentClose: false))
        {
            under.OnOpened();
        }
        if (under is null)
        {
            ReturningHome?.Invoke(leaving.Id);
        }

        BeginDismiss(leaving, under);
    }

    public void GoHome()
    {
        if (motion == ShellMotion.Present && motionUnder is null && ReferenceEquals(motionOver, current))
        {
            var reversing = motionOver!;
            ReverseToDismiss();
            ReturningHome?.Invoke(reversing.Id);
            return;
        }

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

    public bool Scrub(float coverValue, Vector2 drift)
    {
        if (!scrubbing && !TryBeginScrub())
        {
            return false;
        }

        cover.SnapTo(Math.Clamp(coverValue, 0f, 1f));
        driftX.SnapTo(drift.X);
        driftY.SnapTo(drift.Y);
        return true;
    }

    public void ReleaseScrub(bool toHome, float coverVelocity)
    {
        if (!scrubbing)
        {
            return;
        }

        scrubbing = false;
        if (toHome)
        {
            UiFeedback.Play(UiSound.AppClose);
            history.Clear();
            coverTarget = 0f;
            coverSmoothTime = TransitionTiming.ZoomDismissSmoothTime;
            var kick = TransitionTiming.LaunchVelocity(coverSmoothTime);
            cover.Velocity = Math.Clamp(coverVelocity, -kick * TransitionTiming.ScrubKickCapFactor, -kick);
            return;
        }

        ReverseToPresent();
        var returnKick = TransitionTiming.LaunchVelocity(coverSmoothTime);
        cover.Velocity = Math.Clamp(coverVelocity, returnKick * TransitionTiming.ScrubReturnKickFraction,
            returnKick * TransitionTiming.ScrubKickCapFactor);
    }

    private bool TryBeginScrub()
    {
        if (motion != ShellMotion.None || current is null)
        {
            return false;
        }

        var leaving = current;
        current = null;
        ReturningHome?.Invoke(leaving.Id);
        motion = ShellMotion.Dismiss;
        motionOver = leaving;
        motionUnder = null;
        motionOrigin = null;
        motionOriginKind = LaunchOrigin.Icon;
        coverTarget = 0f;
        coverSmoothTime = TransitionTiming.ZoomDismissSmoothTime;
        cover.SnapTo(1f);
        scrubbing = true;
        return true;
    }

    private void BeginPresent(IPhoneApp over, IPhoneApp? under)
    {
        if (!resumingOpen)
        {
            AppVisits.NoteOpened(over.Id);
        }

        resumingOpen = false;
        motion = ShellMotion.Present;
        motionOver = over;
        motionUnder = under;
        motionOrigin = under is null ? pendingOrigin : null;
        motionOriginKind = under is null ? pendingOriginKind : LaunchOrigin.Icon;
        coverTarget = 1f;
        coverSmoothTime = under is null ? TransitionTiming.ZoomPresentSmoothTime : TransitionTiming.PresentSmoothTime;
        cover.Launch(0f, TransitionTiming.LaunchVelocity(coverSmoothTime));
    }

    private void BeginDismiss(IPhoneApp over, IPhoneApp? under)
    {
        UiFeedback.Play(UiSound.AppClose);
        motion = ShellMotion.Dismiss;
        motionOver = over;
        motionUnder = under;
        motionOrigin = null;
        motionOriginKind = LaunchOrigin.Icon;
        coverTarget = 0f;
        coverSmoothTime = under is null ? TransitionTiming.ZoomDismissSmoothTime : TransitionTiming.DismissSmoothTime;
        cover.Launch(1f, -TransitionTiming.LaunchVelocity(coverSmoothTime));
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
            NotifyClosed(motionUnder);
        }
        else if (motion == ShellMotion.Dismiss)
        {
            NotifyClosed(motionOver);
        }

        motion = ShellMotion.None;
        scrubbing = false;
        motionOver = null;
        motionUnder = null;
        motionOrigin = null;
        motionOriginKind = LaunchOrigin.Icon;
        driftX.SnapTo(0f);
        driftY.SnapTo(0f);
    }

    private void NotifyClosed(IPhoneApp? app)
    {
        if (app is null)
        {
            return;
        }

        app.OnClosed();
        if (forgetOnClose.Remove(app.Id))
        {
            return;
        }

        closedAtTicks[app.Id] = Environment.TickCount64;
    }
}
