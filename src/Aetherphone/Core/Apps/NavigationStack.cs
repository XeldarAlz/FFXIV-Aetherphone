using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Apps;

internal enum ShellMotion
{
    None,
    Present,
    Dismiss,
}

internal sealed class NavigationStack : INavigator
{
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly Stack<IPhoneApp> history = new();
    private readonly TransitionPlayer player = new();

    private IPhoneApp? current;
    private IPhoneApp? motionOver;
    private IPhoneApp? motionUnder;
    private ShellMotion motion = ShellMotion.None;

    public NavigationStack(IReadOnlyList<IPhoneApp> apps)
    {
        this.apps = apps;
    }

    public IPhoneApp? Current => current;

    public bool AtHome => current is null;

    public bool IsTransitioning => motion != ShellMotion.None;

    public ShellMotion Motion => motion;

    public float MotionProgress => player.Progress;

    public IPhoneApp MotionOver => motionOver!;

    public IPhoneApp? MotionUnder => motionUnder;

    public void Advance(float deltaSeconds)
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        player.Advance(deltaSeconds);
        if (!player.IsPlaying)
        {
            FinalizeMotion();
        }
    }

    public void OpenApp(IPhoneApp app)
    {
        SettleAny();

        var under = current;
        if (under is not null)
        {
            history.Push(under);
        }

        current = app;
        app.OnOpened();
        Begin(ShellMotion.Present, app, under);
    }

    public void Open(string appId)
    {
        if (current?.Id == appId)
        {
            return;
        }

        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                OpenApp(apps[index]);
                return;
            }
        }
    }

    public void Back()
    {
        if (current is null)
        {
            return;
        }

        SettleAny();

        var leaving = current;
        var under = history.Count > 0 ? history.Pop() : null;
        current = under;
        under?.OnOpened();
        Begin(ShellMotion.Dismiss, leaving, under);
    }

    public void GoHome()
    {
        if (current is null)
        {
            return;
        }

        SettleAny();

        var leaving = current;
        history.Clear();
        current = null;
        Begin(ShellMotion.Dismiss, leaving, null);
    }

    private void Begin(ShellMotion shellMotion, IPhoneApp over, IPhoneApp? under)
    {
        motion = shellMotion;
        motionOver = over;
        motionUnder = under;

        if (shellMotion == ShellMotion.Present)
        {
            player.Start(TransitionTiming.PresentSeconds, TransitionTiming.PresentCurve);
        }
        else
        {
            player.Start(TransitionTiming.DismissSeconds, TransitionTiming.DismissCurve);
        }
    }

    private void SettleAny()
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        player.Finish();
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
        }

        motion = ShellMotion.None;
        motionOver = null;
        motionUnder = null;
    }
}
