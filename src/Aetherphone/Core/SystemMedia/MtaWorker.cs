namespace Aetherphone.Core.SystemMedia;

internal sealed class MtaWorker : IDisposable
{
    public const int Exit = -2;

    private const int FailureBackoffMilliseconds = 5000;
    private const int JoinTimeoutMilliseconds = 3000;

    private readonly Func<int> tick;
    private readonly Action teardown;
    private readonly string name;
    private readonly AutoResetEvent wake = new(false);
    private readonly Thread thread;
    private volatile bool stopping;

    public MtaWorker(string name, Func<int> tick, Action teardown)
    {
        this.name = name;
        this.tick = tick;
        this.teardown = teardown;
        thread = new Thread(Run) { IsBackground = true, Name = name, };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    public void Wake()
    {
        if (stopping)
        {
            return;
        }

        wake.Set();
    }

    public void Dispose()
    {
        stopping = true;
        wake.Set();
        if (thread.Join(JoinTimeoutMilliseconds))
        {
            wake.Dispose();
            return;
        }

        AepLog.Warning($"[SystemMedia] {name} did not stop within {JoinTimeoutMilliseconds} ms");
    }

    private void Run()
    {
        var enteredApartment = WinRt.EnterMultithreadedApartment();
        try
        {
            var wait = 0;
            while (!stopping)
            {
                if (wait != 0)
                {
                    wake.WaitOne(wait);
                }

                if (stopping)
                {
                    break;
                }

                wait = SafeTick();
                if (wait == Exit)
                {
                    break;
                }
            }
        }
        finally
        {
            SafeTeardown();
            if (enteredApartment)
            {
                WinRt.LeaveApartment();
            }
        }
    }

    private int SafeTick()
    {
        try
        {
            return tick();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[SystemMedia] {name} tick failed");
            return FailureBackoffMilliseconds;
        }
    }

    private void SafeTeardown()
    {
        try
        {
            teardown();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[SystemMedia] {name} teardown failed");
        }
    }
}
