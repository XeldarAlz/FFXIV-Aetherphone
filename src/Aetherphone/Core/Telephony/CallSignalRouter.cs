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
        signals.BindSender(Send);
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
        if (message.Type.StartsWith(SignalType.CasinoPrefix, StringComparison.Ordinal))
        {
            signals.PublishCasino(new CasinoSignal(message.Type, message.Reason, message.Casino));
            return;
        }

        if (message.Type.StartsWith(SignalType.GamePrefix, StringComparison.Ordinal))
        {
            signals.PublishGame(new GameSignal(message.Type, message.Reason, message.Game));
            return;
        }

        switch (message.Type)
        {
            case SignalType.ChatPing:
                signals.PublishChat(new ChatSignal(message.ContentId, message.Message));
                return;
            case SignalType.KeysStale:
                signals.PublishKeysStale();
                return;
            case SignalType.KeysLinkPending:
                signals.PublishDeviceLinkRequested();
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
            case SignalType.AnnouncePing:
                signals.PublishAnnouncements();
                return;
            case SignalType.PollPing:
                signals.PublishPolls();
                return;
            case SignalType.ContentRemoved:
                if (message.ContentId is { Length: > 0 } removedContentId)
                {
                    signals.PublishContentRemoved(
                        new ContentRemovalSignal(message.App, message.ContentKind, removedContentId, message.ParentId));
                }

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
            if (!message.Type.StartsWith(SignalType.StreamPrefix, StringComparison.Ordinal))
            {
                AepLog.Warning($"[calls] unhandled-signal type={message.Type} call={message.CallId} reason={message.Reason}");
            }

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
        signals.BindSender(null);
        connection.ControlReceived -= OnControl;
        connection.ConnectedChanged -= OnConnected;
        connection.Dispose();
    }
}
