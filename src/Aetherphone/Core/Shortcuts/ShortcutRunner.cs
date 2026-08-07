using Aetherphone.Core.Linkpearl;
using Aetherphone.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Shortcuts;

internal enum ShortcutRunOutcome : byte
{
    Completed,
    Cancelled,
    CommandRejected,
    PluginUnavailable,
    LinkRejected,
    NotLoggedIn,
    GameBusy,
}

internal readonly struct ShortcutRunView
{
    public readonly Guid Id;
    public readonly string Name;
    public readonly Vector4 Accent;
    public readonly int Step;
    public readonly int StepCount;
    public readonly bool Holding;

    public ShortcutRunView(Guid id, string name, Vector4 accent, int step, int stepCount, bool holding)
    {
        Id = id;
        Name = name;
        Accent = accent;
        Step = step;
        StepCount = stepCount;
        Holding = holding;
    }

    public bool IsRunning => Id != Guid.Empty;

    public float Progress => StepCount > 0 ? Math.Clamp((float)Step / StepCount, 0f, 1f) : 0f;
}

internal sealed class ShortcutRunner : IDisposable
{
    public const float MaxWaitSeconds = 60f;
    public const float MinWaitSeconds = 0.1f;
    private const float StepGapSeconds = 0.05f;

    private static readonly ConditionFlag[] ChatSwallowedConditions =
    {
        ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51, ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78,
    };

    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly List<ShortcutStep> queue = new();
    private ShortcutHold hold;
    private Guid runningId;
    private string runningName = string.Empty;
    private Vector4 runningAccent;
    private int cursor;
    private float wait;
    private bool subscribed;

    public ShortcutRunner(IClientState clientState, ICondition condition)
    {
        this.clientState = clientState;
        this.condition = condition;
    }

    public event Action<ShortcutRunView, ShortcutRunOutcome>? Finished;

    public bool IsRunning => runningId != Guid.Empty;

    public static bool NeedsGame(ShortcutStepKind kind) => kind == ShortcutStepKind.Command;

    public ShortcutRunView Snapshot() => new(runningId, runningName, runningAccent,
        Math.Clamp(cursor, 1, Math.Max(queue.Count, 1)), queue.Count, hold.IsWaiting);

    public void Run(ShortcutEntry entry)
    {
        if (entry.Steps.Count == 0)
        {
            return;
        }

        if (!Plugin.Framework.IsInFrameworkUpdateThread)
        {
            var snapshot = entry.Copy();
            snapshot.Id = entry.Id;
            _ = Plugin.Framework.RunOnFrameworkThread(() => Run(snapshot));
            return;
        }

        Cancel();
        queue.Clear();
        for (var index = 0; index < entry.Steps.Count; index++)
        {
            queue.Add(entry.Steps[index]);
        }

        runningId = entry.Id;
        runningName = entry.Name;
        runningAccent = ShortcutTint.Resolve(entry.Tint);
        cursor = 0;
        wait = 0f;
        hold.Clear();
        Subscribe();
    }

    public void Cancel()
    {
        if (runningId == Guid.Empty)
        {
            return;
        }

        if (!Plugin.Framework.IsInFrameworkUpdateThread)
        {
            _ = Plugin.Framework.RunOnFrameworkThread(Cancel);
            return;
        }

        Stop(ShortcutRunOutcome.Cancelled);
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        subscribed = true;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (runningId == Guid.Empty)
        {
            Unsubscribe();
            return;
        }

        var delta = (float)framework.UpdateDelta.TotalSeconds;
        if (wait > 0f)
        {
            wait -= delta;
            if (wait > 0f)
            {
                return;
            }

            wait = 0f;
        }

        if (cursor >= queue.Count)
        {
            Stop(ShortcutRunOutcome.Completed);
            return;
        }

        var step = queue[cursor];
        if (NeedsGame(step.Kind) && !TryClearGate(delta))
        {
            return;
        }

        cursor++;
        if (!Execute(step, out var failure))
        {
            Stop(failure);
            return;
        }

        if (cursor >= queue.Count && wait <= 0f)
        {
            Stop(ShortcutRunOutcome.Completed);
        }
    }

    private bool TryClearGate(float delta)
    {
        if (!clientState.IsLoggedIn)
        {
            Stop(ShortcutRunOutcome.NotLoggedIn);
            return false;
        }

        switch (hold.Advance(ChatInputSwallowed(), delta))
        {
            case ShortcutHoldOutcome.Waiting:
                return false;
            case ShortcutHoldOutcome.Expired:
                Stop(ShortcutRunOutcome.GameBusy);
                return false;
            default:
                return true;
        }
    }

    private bool ChatInputSwallowed()
    {
        for (var index = 0; index < ChatSwallowedConditions.Length; index++)
        {
            if (condition[ChatSwallowedConditions[index]])
            {
                return true;
            }
        }

        return false;
    }

    private bool Execute(ShortcutStep step, out ShortcutRunOutcome failure)
    {
        failure = ShortcutRunOutcome.Completed;
        switch (step.Kind)
        {
            case ShortcutStepKind.Wait:
                wait = Math.Clamp(step.Seconds, 0f, MaxWaitSeconds);
                return true;
            case ShortcutStepKind.OpenPlugin:
                if (!PluginCatalog.TryOpenMainUi(step.Text))
                {
                    failure = ShortcutRunOutcome.PluginUnavailable;
                    return false;
                }

                wait = StepGapSeconds;
                return true;
            case ShortcutStepKind.OpenUrl:
                return OpenLink(step.Text, out failure);
            default:
                return ExecuteCommand(step, out failure);
        }
    }

    private bool OpenLink(string url, out ShortcutRunOutcome failure)
    {
        failure = ShortcutRunOutcome.Completed;
        var opened = true;
        UrlActions.OpenInBrowser(url.Trim(), exception =>
        {
            opened = false;
            AepLog.Warning($"Shortcut \"{runningName}\" could not open {url}: {exception.Message}");
        });

        if (!opened)
        {
            failure = ShortcutRunOutcome.LinkRejected;
            return false;
        }

        wait = StepGapSeconds;
        return true;
    }

    private bool ExecuteCommand(ShortcutStep step, out ShortcutRunOutcome failure)
    {
        failure = ShortcutRunOutcome.Completed;
        var line = ShortcutCommandText.Split(step.Text, out var inlineWait);
        if (line.Length == 0)
        {
            wait = Math.Clamp(inlineWait, 0f, MaxWaitSeconds);
            return true;
        }

        if (!ChatSender.TrySendSanitised(line))
        {
            AepLog.Warning($"Shortcut \"{runningName}\" could not send: {line}");
            failure = ShortcutRunOutcome.CommandRejected;
            return false;
        }

        wait = inlineWait > 0f ? Math.Clamp(inlineWait, 0f, MaxWaitSeconds) : StepGapSeconds;
        return true;
    }

    private void Stop(ShortcutRunOutcome outcome)
    {
        if (runningId == Guid.Empty)
        {
            return;
        }

        var finished = Snapshot();
        runningId = Guid.Empty;
        runningName = string.Empty;
        runningAccent = default;
        queue.Clear();
        cursor = 0;
        wait = 0f;
        hold.Clear();
        Unsubscribe();
        Finished?.Invoke(finished, outcome);
    }

    public void Dispose()
    {
        runningId = Guid.Empty;
        queue.Clear();
        Unsubscribe();
    }
}
