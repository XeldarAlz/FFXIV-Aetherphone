using Aetherphone.Core.Home;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core;

internal sealed class FrameworkTicker : IDisposable
{
    private readonly IFramework framework;
    private readonly long intervalMilliseconds;
    private readonly Action onTick;
    private readonly AppGate gate;
    private long lastTickMilliseconds;

    public FrameworkTicker(IFramework framework, long intervalMilliseconds, Action onTick, AppGate gate = default)
    {
        this.framework = framework;
        this.intervalMilliseconds = intervalMilliseconds;
        this.onTick = onTick;
        this.gate = gate;
        framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework owner)
    {
        if (!gate.Open)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - lastTickMilliseconds < intervalMilliseconds)
        {
            return;
        }

        lastTickMilliseconds = now;
        onTick();
    }

    public void Dispose() => framework.Update -= OnUpdate;
}
