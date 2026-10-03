using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Telephony.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Radio;

internal sealed class RadioRoomRouter : IDisposable
{
    private readonly RealtimeSignalBus signals;
    private readonly IFramework? framework;
    private readonly ConcurrentQueue<RadioInbound> inbound = new();

    public RadioRoomRouter(RealtimeSignalBus signals, AethernetSession session, IFramework? framework)
        : this(signals, new RadioRoomSession(signals.TrySend, () => session.CurrentUser?.Id), framework)
    {
    }

    internal RadioRoomRouter(RealtimeSignalBus signals, RadioRoomSession room, IFramework? framework)
    {
        this.signals = signals;
        this.framework = framework;
        Room = room;
        signals.RadioReceived += OnRadio;
        signals.ConnectedChanged += OnConnected;
        if (framework is not null)
        {
            framework.Update += OnFrameworkUpdate;
        }
    }

    public RadioRoomSession Room { get; }

    internal void Drain()
    {
        while (inbound.TryDequeue(out var item))
        {
            if (item.Control is { } control)
            {
                Room.Receive(control);
                continue;
            }

            Room.OnConnectionChanged(item.Connected);
        }

        Room.Tick();
    }

    private void OnRadio(CallControl control)
    {
        inbound.Enqueue(new RadioInbound(control, false));
    }

    private void OnConnected(bool connected)
    {
        inbound.Enqueue(new RadioInbound(null, connected));
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        Drain();
    }

    public void Dispose()
    {
        if (framework is not null)
        {
            framework.Update -= OnFrameworkUpdate;
        }

        signals.RadioReceived -= OnRadio;
        signals.ConnectedChanged -= OnConnected;
        Room.Detach();
    }
}
