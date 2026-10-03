using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal sealed class JamSignalRouter : IDisposable
{
    private readonly CallSignalRouter calls;

    public JamSignalRouter(CallSignalRouter calls)
    {
        this.calls = calls;
        calls.Connection.ControlReceived += OnControl;
        calls.ConnectedChanged += OnConnectedChanged;
    }

    public event Action<CallControl>? Received;
    public event Action<bool>? ConnectedChanged;

    public bool Connected => calls.Connected;

    public void Start(string? title) => Send(new CallControl { Type = SignalType.JamStart, Title = title });

    public void Join(string code) => Send(new CallControl { Type = SignalType.JamJoin, Code = code });

    public void Leave() => Send(new CallControl { Type = SignalType.JamLeave });

    public void End() => Send(new CallControl { Type = SignalType.JamEnd });

    public void Kick(string userId) => Send(new CallControl { Type = SignalType.JamKick, UserId = userId });

    public void Transfer(string userId) => Send(new CallControl { Type = SignalType.JamTransfer, UserId = userId });

    public void Approve(string userId) => Send(new CallControl { Type = SignalType.JamApprove, UserId = userId });

    public void Deny(string userId) => Send(new CallControl { Type = SignalType.JamDeny, UserId = userId });

    public void Invite(string userId) => Send(new CallControl { Type = SignalType.JamInvite, UserId = userId });

    public void React(int reaction) => Send(new CallControl { Type = SignalType.JamReact, Reaction = reaction });

    public void Chat(string text) => Send(new CallControl { Type = SignalType.JamChat, Text = text });

    public void DeleteMessage(long messageId)
    {
        Send(new CallControl { Type = SignalType.JamDeleteMessage, MessageId = messageId });
    }

    public void Nearby(uint territoryId, uint worldId)
    {
        Send(new CallControl { Type = SignalType.JamNearby, TerritoryId = territoryId, WorldId = worldId });
    }

    public void Settings(int? guestPermissions, bool? approvalRequired, string? title, bool? discoverable = null)
    {
        Send(new CallControl
        {
            Type = SignalType.JamSettings,
            GuestPermissions = guestPermissions,
            ApprovalRequired = approvalRequired,
            Title = title,
            Discoverable = discoverable,
        });
    }

    public void State(JamTrack? track, double positionSeconds, bool paused)
    {
        Send(new CallControl
        {
            Type = SignalType.JamState,
            Track = track,
            PositionSeconds = Math.Max(0d, positionSeconds),
            Paused = paused,
        });
    }

    public void Control(string action, double? positionSeconds)
    {
        Send(new CallControl { Type = SignalType.JamControl, Action = action, PositionSeconds = positionSeconds });
    }

    public void Send(CallControl control) => calls.Send(control);

    private void OnControl(CallControl message)
    {
        if (message.Type.StartsWith(SignalType.JamPrefix, StringComparison.Ordinal))
        {
            Received?.Invoke(message);
        }
    }

    private void OnConnectedChanged(bool connected) => ConnectedChanged?.Invoke(connected);

    public void Dispose()
    {
        calls.Connection.ControlReceived -= OnControl;
        calls.ConnectedChanged -= OnConnectedChanged;
    }
}
