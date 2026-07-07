using Dalamud.Plugin.Services;

namespace Aetherphone.Core;

internal sealed class FrameworkTicker : IDisposable
{
    private readonly IFramework framework;
    private readonly long intervalMilliseconds;
    private readonly Action onTick;
    private long lastTickMilliseconds;

    public FrameworkTicker(IFramework framework, long intervalMilliseconds, Action onTick)
    {
        this.framework = framework;
        this.intervalMilliseconds = intervalMilliseconds;
        this.onTick = onTick;
        framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework owner)
    {
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
