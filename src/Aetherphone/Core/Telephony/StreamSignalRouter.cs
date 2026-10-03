using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal readonly record struct StreamScreenPose(Vector3 Position, float Yaw, float Pitch, float Roll, float Scale,
    float Curve);

internal readonly record struct StreamPublication(
    string Url,
    double PositionSeconds,
    bool Paused,
    uint TerritoryId,
    uint WorldId,
    bool ApprovalRequired,
    bool Discoverable,
    bool CodeEnabled,
    int GuestPermissions,
    StreamMember[]? Members,
    StreamQueueEntry[]? UpcomingQueue,
    StreamScreenPose? Screen);

internal sealed class StreamSignalRouter : IDisposable
{
    private readonly CallSignalRouter calls;

    public StreamSignalRouter(CallSignalRouter calls)
    {
        this.calls = calls;
        calls.Connection.ControlReceived += OnControl;
    }

    public event Action<CallControl>? Joined;
    public event Action<CallControl>? Declined;
    public event Action<CallControl>? RosterReceived;

    public event Action<CallControl>? LeftReceived;
    public event Action<CallControl>? Ended;
    public event Action<CallControl>? StateReceived;
    public event Action<CallControl>? NearbyReceived;

    public event Action<CallControl>? JoinRequested;
    public event Action<CallControl>? JoinPending;

    public event Action<CallControl>? QueueSuggested;
    public event Action<CallControl>? QueueSuggestionResult;

    public event Action<CallControl>? Kicked;

    public event Action<CallControl>? ViewerFailed;

    public event Action<CallControl>? HostChanged;
    public event Action<CallControl>? ControlRequested;
    public event Action<CallControl>? Reacted;

    public bool Connected => calls.Connected;

    public void ReportPlaybackFailure(string url, string? reason)
    {
        calls.Send(new CallControl { Type = SignalType.StreamPlaybackFailed, Url = url, Reason = reason });
    }

    public void PublishState(in StreamPublication publication)
    {
        var screen = publication.Screen;
        calls.Send(new CallControl
        {
            Type = SignalType.StreamState,
            Url = publication.Url,
            PositionSeconds = publication.PositionSeconds,
            Paused = publication.Paused,
            TerritoryId = publication.TerritoryId,
            WorldId = publication.WorldId,
            ApprovalRequired = publication.ApprovalRequired,
            Discoverable = publication.Discoverable,
            CodeEnabled = publication.CodeEnabled,
            GuestPermissions = publication.GuestPermissions,
            Members = publication.Members,
            Features = StreamFeature.Party,
            UpcomingQueue = publication.UpcomingQueue,
            ScreenX = screen?.Position.X,
            ScreenY = screen?.Position.Y,
            ScreenZ = screen?.Position.Z,
            ScreenYaw = screen?.Yaw,
            ScreenScale = screen?.Scale,
            ScreenPitch = screen?.Pitch,
            ScreenRoll = screen?.Roll,
            ScreenCurve = screen?.Curve,
        });
    }

    public void Approve(string userId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamApprove, UserId = userId });
    }

    public void Deny(string userId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamDeny, UserId = userId });
    }

    public void SuggestQueueItem(string url, string suggestionId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamQueueSuggest, Url = url, SuggestionId = suggestionId });
    }

    public void ApproveQueueSuggestion(string suggestionId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamQueueApprove, SuggestionId = suggestionId });
    }

    public void DenyQueueSuggestion(string suggestionId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamQueueDeny, SuggestionId = suggestionId });
    }

    public void Kick(string userId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamKick, UserId = userId });
    }

    public void Join(string hostId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamJoin, HostId = hostId, Features = StreamFeature.Party });
    }

    public void JoinByCode(string code)
    {
        calls.Send(new CallControl { Type = SignalType.StreamJoin, Code = code, Features = StreamFeature.Party });
    }

    public void Transfer(string userId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamTransfer, UserId = userId });
    }

    public void Control(string action, double? positionSeconds = null)
    {
        calls.Send(new CallControl
        {
            Type = SignalType.StreamControl, Action = action, PositionSeconds = positionSeconds,
        });
    }

    public void React(int reaction)
    {
        calls.Send(new CallControl { Type = SignalType.StreamReact, Reaction = reaction });
    }

    public void RequestNearby(uint territoryId, uint worldId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamNearby, TerritoryId = territoryId, WorldId = worldId });
    }

    public void Leave(string? hostId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamLeave, HostId = hostId });
    }

    private void OnControl(CallControl message)
    {
        switch (message.Type)
        {
            case SignalType.StreamJoined:
                Joined?.Invoke(message);
                return;
            case SignalType.StreamDeclined:
                Declined?.Invoke(message);
                return;
            case SignalType.StreamRoster:
                RosterReceived?.Invoke(message);
                return;
            case SignalType.StreamLeft:
                LeftReceived?.Invoke(message);
                return;
            case SignalType.StreamState:
                StateReceived?.Invoke(message);
                return;
            case SignalType.StreamEnded:
                Ended?.Invoke(message);
                return;
            case SignalType.StreamNearbyRoster:
                NearbyReceived?.Invoke(message);
                return;
            case SignalType.StreamJoinRequest:
                JoinRequested?.Invoke(message);
                return;
            case SignalType.StreamJoinPending:
                JoinPending?.Invoke(message);
                return;
            case SignalType.StreamQueueSuggestion:
                QueueSuggested?.Invoke(message);
                return;
            case SignalType.StreamQueueSuggestionResult:
                QueueSuggestionResult?.Invoke(message);
                return;
            case SignalType.StreamKicked:
                Kicked?.Invoke(message);
                return;
            case SignalType.StreamViewerFailed:
                ViewerFailed?.Invoke(message);
                return;
            case SignalType.StreamHostChanged:
                HostChanged?.Invoke(message);
                return;
            case SignalType.StreamControlRequest:
                ControlRequested?.Invoke(message);
                return;
            case SignalType.StreamReaction:
                Reacted?.Invoke(message);
                return;
        }
    }

    public void Dispose()
    {
        calls.Connection.ControlReceived -= OnControl;
    }
}
