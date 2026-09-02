using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeFramework : IFramework
{
    private const int TicksPerSecond = 60;
    private readonly int mainThreadId = Environment.CurrentManagedThreadId;
    private readonly ConcurrentQueue<Action> queued = new();
    private readonly List<ScheduledAction> scheduled = new();
    private readonly object scheduleGate = new();
    private readonly FrameworkTaskScheduler scheduler;
    private long tickCount;

    public FakeFramework()
    {
        scheduler = new FrameworkTaskScheduler(this);
    }

    public event IFramework.OnUpdateDelegate? Update;

    public DateTime LastUpdate { get; private set; } = DateTime.Now;

    public DateTime LastUpdateUTC { get; private set; } = DateTime.UtcNow;

    public TimeSpan UpdateDelta { get; private set; } = TimeSpan.FromSeconds(1.0 / TicksPerSecond);

    public bool IsInFrameworkUpdateThread => Environment.CurrentManagedThreadId == mainThreadId;

    public bool IsFrameworkUnloading => false;

    public long TickCount => tickCount;

    public void Tick(TimeSpan delta)
    {
        tickCount += 1;
        UpdateDelta = delta;
        LastUpdate += delta;
        LastUpdateUTC += delta;
        while (queued.TryDequeue(out var action))
        {
            Invoke(action);
        }

        RunDue();
        var handlers = Update?.GetInvocationList();
        if (handlers is null)
        {
            return;
        }

        for (var index = 0; index < handlers.Length; index++)
        {
            var handler = (IFramework.OnUpdateDelegate)handlers[index];
            try
            {
                handler(this);
            }
            catch (Exception exception)
            {
                HarnessLog.Failure($"framework update {handler.Method.DeclaringType?.Name}.{handler.Method.Name}", exception);
            }
        }
    }

    public TaskFactory GetTaskFactory() => new(scheduler);

    public Task DelayTicks(long numTicks, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource();
        Schedule(numTicks, () => completion.TrySetResult());
        return completion.Task;
    }

    public Task Run(Action action, CancellationToken cancellationToken = default) => RunOnFrameworkThread(action);

    public Task<T> Run<T>(Func<T> action, CancellationToken cancellationToken = default) => RunOnFrameworkThread(action);

    public Task Run(Func<Task> action, CancellationToken cancellationToken = default) => RunOnFrameworkThread(action);

    public Task<T> Run<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) => RunOnFrameworkThread(action);

    public Task<T> RunOnFrameworkThread<T>(Func<T> func)
    {
        if (IsInFrameworkUpdateThread)
        {
            return Task.FromResult(func());
        }

        var completion = new TaskCompletionSource<T>();
        queued.Enqueue(() => Complete(completion, func));
        return completion.Task;
    }

    public Task RunOnFrameworkThread(Action action) => RunOnFrameworkThread(() =>
    {
        action();
        return true;
    });

    public Task<T> RunOnFrameworkThread<T>(Func<Task<T>> func) =>
        IsInFrameworkUpdateThread ? func() : RunOnFrameworkThread<Task<T>>(func).Unwrap();

    public Task RunOnFrameworkThread(Func<Task> func) =>
        IsInFrameworkUpdateThread ? func() : RunOnFrameworkThread<Task>(func).Unwrap();

    public Task<T> RunOnTick<T>(Func<T> func, TimeSpan delay = default, int delayTicks = 0,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<T>();
        Schedule(DelayFor(delay, delayTicks), () => Complete(completion, func));
        return completion.Task;
    }

    public Task RunOnTick(Action action, TimeSpan delay = default, int delayTicks = 0,
        CancellationToken cancellationToken = default) => RunOnTick(() =>
    {
        action();
        return true;
    }, delay, delayTicks, cancellationToken);

    public Task<T> RunOnTick<T>(Func<Task<T>> func, TimeSpan delay = default, int delayTicks = 0,
        CancellationToken cancellationToken = default) => RunOnTick<Task<T>>(func, delay, delayTicks, cancellationToken).Unwrap();

    public Task RunOnTick(Func<Task> func, TimeSpan delay = default, int delayTicks = 0,
        CancellationToken cancellationToken = default) => RunOnTick<Task>(func, delay, delayTicks, cancellationToken).Unwrap();

    public IDebouncer CreateDebouncer(TimeSpan delay, Action action) => new FakeDebouncer(this, DelayFor(delay, 0), action);

    internal void Schedule(long delayTicks, Action action)
    {
        lock (scheduleGate)
        {
            scheduled.Add(new ScheduledAction(tickCount + Math.Max(delayTicks, 1), action));
        }
    }

    internal void Enqueue(Action action) => queued.Enqueue(action);

    private static long DelayFor(TimeSpan delay, int delayTicks) =>
        Math.Max(delayTicks, (long)(delay.TotalSeconds * TicksPerSecond));

    private void RunDue()
    {
        List<Action>? due = null;
        lock (scheduleGate)
        {
            for (var index = scheduled.Count - 1; index >= 0; index--)
            {
                if (scheduled[index].DueTick > tickCount)
                {
                    continue;
                }

                due ??= new List<Action>();
                due.Add(scheduled[index].Action);
                scheduled.RemoveAt(index);
            }
        }

        if (due is null)
        {
            return;
        }

        for (var index = due.Count - 1; index >= 0; index--)
        {
            Invoke(due[index]);
        }
    }

    private static void Invoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            HarnessLog.Failure("framework action", exception);
        }
    }

    private static void Complete<T>(TaskCompletionSource<T> completion, Func<T> func)
    {
        try
        {
            completion.TrySetResult(func());
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private readonly record struct ScheduledAction(long DueTick, Action Action);

    private sealed class FrameworkTaskScheduler : TaskScheduler
    {
        private readonly FakeFramework framework;

        public FrameworkTaskScheduler(FakeFramework framework)
        {
            this.framework = framework;
        }

        protected override IEnumerable<Task> GetScheduledTasks() => Array.Empty<Task>();

        protected override void QueueTask(Task task) => framework.Enqueue(() => TryExecuteTask(task));

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) =>
            framework.IsInFrameworkUpdateThread && TryExecuteTask(task);
    }

    private sealed class FakeDebouncer : IDebouncer
    {
        private readonly FakeFramework framework;
        private readonly long delayTicks;
        private readonly Action action;
        private int version;
        private bool pending;

        public FakeDebouncer(FakeFramework framework, long delayTicks, Action action)
        {
            this.framework = framework;
            this.delayTicks = delayTicks;
            this.action = action;
        }

        public bool IsPending => pending;

        public void Debounce()
        {
            version += 1;
            var expected = version;
            pending = true;
            framework.Schedule(delayTicks, () =>
            {
                if (expected != version)
                {
                    return;
                }

                pending = false;
                action();
            });
        }

        public void Cancel()
        {
            version += 1;
            pending = false;
        }

        public void Dispose() => Cancel();
    }
}
