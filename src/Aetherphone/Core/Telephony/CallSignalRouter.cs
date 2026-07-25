using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class CallSignalRouter : IDisposable
{
    private readonly RealtimeConnection connection;
    private readonly RealtimeSignalBus signals;

    public CallSignalRouter(AethernetSession session, RealtimeSignalBus signals)
    {
        this.signals = signals;
        connection = new RealtimeConnection(session);
        connection.ControlReceived += OnControl;
        connection.ConnectedChanged += OnConnected;
    }

    public event Action<Guid, CallControl>? IncomingReceived;
    public event Action<Guid, CallControl>? RosterReceived;
    public event Action<Guid, CallControl>? DeclinedReceived;
    public event Action<Guid, CallControl>? UnavailableReceived;
    public event Action<Guid, CallControl>? EndedReceived;
    public event Action<Guid, CallControl>? HandledElsewhereReceived;
    public event Action<bool>? ConnectedChanged;

    public bool Connected => connection.Connected;
    public RealtimeConnection Connection => connection;

    public void Start() => connection.Start();

    public void Stop() => connection.Stop();

    public void Send(CallControl control)
    {
        _ = connection.SendControlAsync(control);
    }

    private void OnConnected(bool isConnected)
    {
        signals.SetActive(isConnected);
        ConnectedChanged?.Invoke(isConnected);
    }

    private void OnControl(CallControl message)
    {
        switch (message.Type)
        {
            case SignalType.ChatPing:
                signals.PublishChat();
                return;
            case SignalType.VelvetPing:
                signals.PublishVelvet();
                return;
            case SignalType.GramPing:
                signals.PublishGram();
                return;
            case SignalType.SocialPing:
                signals.PublishSocial();
                return;
            case SignalType.MusterPing:
                signals.PublishMuster();
                return;
        }

        var target = message.Type switch
        {
            SignalType.Incoming => IncomingReceived,
            SignalType.Roster => RosterReceived,
            SignalType.Declined => DeclinedReceived,
            SignalType.Unavailable => UnavailableReceived,
            SignalType.Ended => EndedReceived,
            SignalType.Handled => HandledElsewhereReceived,
            _ => null,
        };

        if (target is null)
        {
            AepLog.Warning($"[calls] unhandled-signal type={message.Type} call={message.CallId} reason={message.Reason}");
            return;
        }

        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        target.Invoke(id, message);
    }

    public void Dispose()
    {
        connection.ControlReceived -= OnControl;
        connection.ConnectedChanged -= OnConnected;
        connection.Dispose();
    }
}
