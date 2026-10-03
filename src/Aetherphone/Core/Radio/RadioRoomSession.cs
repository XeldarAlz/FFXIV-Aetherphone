using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Radio;

internal sealed class RadioRoomSession : ILiveChatFeed
{
    public const int MessageCapacity = RadioChatRing.Capacity;
    public const int MaxChatLength = 300;
    public const int MaxPinnedLength = 200;
    public const int MaxRequestLength = 200;
    public const int ReactionKinds = 8;
    public const int DefaultMuteMinutes = 10;
    public const int MaxMuteMinutes = 24 * 60;
    public const int ReactionCapacity = 32;
    public const long ReactionLifetimeMilliseconds = 2500;

    private readonly Func<CallControl, bool> send;
    private readonly Func<string?> currentUserId;
    private readonly Func<long> tickClock;
    private readonly Func<long> unixClock;
    private readonly Func<long, string> formatClock;
    private readonly RadioChatRing messages = new();
    private readonly RadioReactionPulse[] reactions = new RadioReactionPulse[ReactionCapacity];
    private readonly HashSet<string> hiddenUserIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> mutedUsers = new(StringComparer.Ordinal);
    private readonly RadioChatPacer chatPacer = new();
    private readonly RadioCooldownGate reactionGate = new(RadioRoomPacing.ReactionMilliseconds);
    private readonly RadioCooldownGate requestGate = new(RadioRoomPacing.RequestMilliseconds);
    private readonly RadioCooldownGate attachGate = new(RadioRoomPacing.AttachMilliseconds);
    private RadioRequestEntry[] requests = Array.Empty<RadioRequestEntry>();
    private int reactionStart;
    private int reactionCount;
    private long attachDueTick;
    private RadioRefusal pendingRefusal;
    private bool hasRefusal;

    public RadioRoomSession(Func<CallControl, bool> send, Func<string?> currentUserId, Func<long>? tickClock = null,
        Func<long>? unixClock = null, Func<long, string>? formatClock = null)
    {
        this.send = send;
        this.currentUserId = currentUserId;
        this.tickClock = tickClock ?? DefaultTick;
        this.unixClock = unixClock ?? DefaultUnix;
        this.formatClock = formatClock ?? DefaultClockLabel;
    }

    public string StationId { get; private set; } = string.Empty;

    public RadioRoomStatus Status { get; private set; }

    public bool IsAttached => Status == RadioRoomStatus.Attached;

    public int Version { get; private set; }

    public int ReactionVersion { get; private set; }

    public string? Pinned { get; private set; }

    public bool RequestsOpen { get; private set; }

    public int ListenerCount { get; private set; }

    public bool IsDj { get; private set; }

    public bool IsModerator { get; private set; }

    public bool CanModerate => IsDj || IsModerator;

    public bool IsLive { get; private set; }

    public long MutedUntilUnixMs { get; private set; }

    public bool HasOwnRequest { get; private set; }

    public int MessageCount => messages.Count;

    public int RequestCount => requests.Length;

    public int ReactionCount => reactionCount;

    public bool HasRefusal => hasRefusal;

    public RadioChatEntry MessageAt(int index)
    {
        return messages.At(index);
    }

    int ILiveChatFeed.Count => messages.Count;

    RadioChatEntry ILiveChatFeed.At(int index) => messages.At(index);

    public RadioRequestEntry RequestAt(int index)
    {
        return requests[index];
    }

    public RadioReactionPulse ReactionAt(int index)
    {
        return reactions[(reactionStart + index) % ReactionCapacity];
    }

    public bool TryTakeRefusal(out RadioRefusal refusal)
    {
        refusal = pendingRefusal;
        if (!hasRefusal)
        {
            return false;
        }

        hasRefusal = false;
        return true;
    }

    public bool IsMuted()
    {
        return MutedUntilUnixMs > unixClock();
    }

    public long MutedUntilFor(string userId)
    {
        if (!mutedUsers.TryGetValue(userId, out var until) || until <= unixClock())
        {
            return 0;
        }

        return until;
    }

    public void Attach(string stationId)
    {
        if (stationId.Length == 0)
        {
            return;
        }

        if (string.Equals(StationId, stationId, StringComparison.Ordinal)
            && Status is RadioRoomStatus.Attaching or RadioRoomStatus.Attached)
        {
            return;
        }

        ClearRoom();
        StationId = stationId;
        Status = RadioRoomStatus.Attaching;
        Bump();
        RequestAttach();
    }

    public void Detach()
    {
        if (StationId.Length == 0)
        {
            return;
        }

        var leaving = StationId;
        ClearRoom();
        StationId = string.Empty;
        Status = RadioRoomStatus.Idle;
        Bump();
        send(new CallControl { Type = SignalType.RadioDetach, StationId = leaving });
    }

    public void OnConnectionChanged(bool connected)
    {
        if (StationId.Length == 0)
        {
            return;
        }

        chatPacer.Reset();
        reactionGate.Reset();
        requestGate.Reset();
        if (Status != RadioRoomStatus.Attaching)
        {
            Status = RadioRoomStatus.Attaching;
            Bump();
        }

        if (!connected)
        {
            attachDueTick = 0;
            return;
        }

        attachGate.Reset();
        RequestAttach();
    }

    public void Tick()
    {
        var now = tickClock();
        if (attachDueTick != 0 && now >= attachDueTick && Status == RadioRoomStatus.Attaching)
        {
            RequestAttach();
        }

        PruneReactions(now);
    }

    public void Receive(CallControl message)
    {
        if (StationId.Length == 0)
        {
            return;
        }

        if (message.StationId is { Length: > 0 } named && !string.Equals(named, StationId, StringComparison.Ordinal))
        {
            return;
        }

        switch (message.Type)
        {
            case SignalType.RadioRoom:
                ApplyRoom(message);
                return;
            case SignalType.RadioPresence:
                ApplyPresence(message);
                return;
            case SignalType.RadioMessage:
                ApplyMessage(message);
                return;
            case SignalType.RadioDeleted:
                if (message.MessageId is { } deletedId && RemoveMessages(deletedId, null) > 0)
                {
                    Bump();
                }

                return;
            case SignalType.RadioMuted:
                ApplyMuted(message);
                return;
            case SignalType.RadioPinned:
                Pinned = message.Text;
                Bump();
                return;
            case SignalType.RadioReaction:
                ApplyReaction(message);
                return;
            case SignalType.RadioRequests:
                ApplyRequests(message.Requests, message.RequestsOpen);
                Bump();
                return;
            case SignalType.RadioRefused:
                ApplyRefusal(message);
                return;
        }
    }

    public bool CanSendChat()
    {
        if (!IsAttached || (!CanModerate && IsMuted()))
        {
            return false;
        }

        return chatPacer.CanSend(tickClock());
    }

    public bool SendChat(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxChatLength || !CanSendChat())
        {
            return false;
        }

        if (!send(new CallControl { Type = SignalType.RadioChat, StationId = StationId, Text = trimmed }))
        {
            return false;
        }

        chatPacer.Take(tickClock());
        return true;
    }

    public bool Delete(long messageId)
    {
        if (!IsAttached || FindMessage(messageId) is not { } entry || (!entry.IsMine && !CanModerate))
        {
            return false;
        }

        return send(new CallControl { Type = SignalType.RadioDelete, StationId = StationId, MessageId = messageId });
    }

    public bool Mute(string userId, int minutes = DefaultMuteMinutes)
    {
        if (!CanTargetUser(userId))
        {
            return false;
        }

        return send(new CallControl
        {
            Type = SignalType.RadioMute,
            StationId = StationId,
            UserId = userId,
            Minutes = Math.Clamp(minutes, 1, MaxMuteMinutes),
        });
    }

    public bool Unmute(string userId)
    {
        if (!CanTargetUser(userId))
        {
            return false;
        }

        return send(new CallControl { Type = SignalType.RadioUnmute, StationId = StationId, UserId = userId });
    }

    public bool Pin(string text)
    {
        var trimmed = text.Trim();
        if (!IsAttached || !CanModerate || trimmed.Length == 0 || trimmed.Length > MaxPinnedLength)
        {
            return false;
        }

        return send(new CallControl { Type = SignalType.RadioPin, StationId = StationId, Text = trimmed });
    }

    public bool Unpin()
    {
        if (!IsAttached || !CanModerate || Pinned is null)
        {
            return false;
        }

        return send(new CallControl { Type = SignalType.RadioUnpin, StationId = StationId });
    }

    public bool CanReact()
    {
        return IsAttached && reactionGate.IsOpen(tickClock());
    }

    public bool React(int reaction)
    {
        if (reaction < 0 || reaction >= ReactionKinds || !CanReact())
        {
            return false;
        }

        if (!send(new CallControl { Type = SignalType.RadioReact, StationId = StationId, Reaction = reaction }))
        {
            return false;
        }

        var now = tickClock();
        reactionGate.Take(now);
        AddReaction(new RadioReactionPulse(reaction, now, true));
        return true;
    }

    public bool CanRequest()
    {
        return IsAttached && RequestsOpen && !HasOwnRequest && !IsMuted() && requestGate.IsOpen(tickClock());
    }

    public bool Request(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxRequestLength || !CanRequest())
        {
            return false;
        }

        if (!send(new CallControl { Type = SignalType.RadioRequest, StationId = StationId, Text = trimmed }))
        {
            return false;
        }

        requestGate.Take(tickClock());
        return true;
    }

    public bool AcceptRequest(long requestId)
    {
        return DecideRequest(SignalType.RadioRequestAccept, requestId, false);
    }

    public bool SkipRequest(long requestId)
    {
        return DecideRequest(SignalType.RadioRequestSkip, requestId, true);
    }

    public bool MarkPlayed(long requestId)
    {
        return DecideRequest(SignalType.RadioRequestPlayed, requestId, false);
    }

    public bool SetRequestsOpen(bool open)
    {
        if (!IsAttached || !IsDj || RequestsOpen == open)
        {
            return false;
        }

        return send(new CallControl { Type = SignalType.RadioRequestsOpen, StationId = StationId, Open = open });
    }

    public void HideUser(string userId)
    {
        if (userId.Length == 0 || !hiddenUserIds.Add(userId))
        {
            return;
        }

        var removed = RemoveMessages(-1, userId) > 0;
        if (CountRequestsFrom(userId) > 0)
        {
            requests = FilterRequests(requests, userId);
            removed = true;
        }

        if (removed)
        {
            Bump();
        }
    }

    public bool IsHidden(string userId)
    {
        return hiddenUserIds.Contains(userId);
    }

    private void RequestAttach()
    {
        var now = tickClock();
        if (!attachGate.IsOpen(now))
        {
            attachDueTick = attachGate.OpensAt;
            return;
        }

        attachDueTick = 0;
        if (send(new CallControl { Type = SignalType.RadioAttach, StationId = StationId }))
        {
            attachGate.Take(now);
        }
    }

    private bool CanTargetUser(string userId)
    {
        if (!IsAttached || !CanModerate || userId.Length == 0)
        {
            return false;
        }

        return !string.Equals(userId, currentUserId(), StringComparison.Ordinal);
    }

    private bool DecideRequest(string type, long requestId, bool ownerMayWithdraw)
    {
        if (!IsAttached || FindRequest(requestId) is not { } request)
        {
            return false;
        }

        if (!IsDj && !(ownerMayWithdraw && request.IsMine))
        {
            return false;
        }

        return send(new CallControl { Type = type, StationId = StationId, RequestId = requestId });
    }

    private void ApplyRoom(CallControl message)
    {
        ClearMessages();
        var incoming = message.Messages;
        if (incoming is not null)
        {
            var me = currentUserId();
            for (var index = 0; index < incoming.Length; index++)
            {
                var wire = incoming[index];
                if (wire is null || hiddenUserIds.Contains(wire.UserId))
                {
                    continue;
                }

                AppendMessage(BuildEntry(wire.MessageId, wire.UserId, wire.DisplayName, wire.Handle, wire.AvatarUrl,
                    wire.Text, wire.SentAtUnixMs, wire.IsDj, me));
            }
        }

        mutedUsers.Clear();
        Pinned = message.Pinned;
        ApplyRequests(message.Requests, message.RequestsOpen);
        ListenerCount = message.ListenerCount ?? ListenerCount;
        IsDj = message.IsDj ?? false;
        IsModerator = message.IsModerator ?? false;
        IsLive = message.IsLive ?? false;
        MutedUntilUnixMs = message.MutedUntilUnixMs ?? 0;
        attachDueTick = 0;
        Status = RadioRoomStatus.Attached;
        Bump();
    }

    private void ApplyPresence(CallControl message)
    {
        ListenerCount = message.ListenerCount ?? ListenerCount;
        IsLive = message.IsLive ?? IsLive;
        Bump();
    }

    private void ApplyMessage(CallControl message)
    {
        if (message.MessageId is not { } messageId || message.UserId is not { Length: > 0 } userId
            || hiddenUserIds.Contains(userId))
        {
            return;
        }

        AppendMessage(BuildEntry(messageId, userId, message.DisplayName ?? string.Empty, message.Handle ?? string.Empty,
            message.AvatarUrl, message.Text ?? string.Empty, message.SentAtUnixMs ?? unixClock(), message.IsDj ?? false,
            currentUserId()));
        Bump();
    }

    private void ApplyMuted(CallControl message)
    {
        if (message.UserId is not { Length: > 0 } userId)
        {
            return;
        }

        var until = message.MutedUntilUnixMs ?? 0;
        if (string.Equals(userId, currentUserId(), StringComparison.Ordinal))
        {
            MutedUntilUnixMs = until;
        }
        else if (until > 0)
        {
            mutedUsers[userId] = until;
        }
        else
        {
            mutedUsers.Remove(userId);
        }

        Bump();
    }

    private void ApplyReaction(CallControl message)
    {
        if (message.Reaction is not { } reaction || reaction < 0 || reaction >= ReactionKinds)
        {
            return;
        }

        var userId = message.UserId ?? string.Empty;
        if (hiddenUserIds.Contains(userId) || string.Equals(userId, currentUserId(), StringComparison.Ordinal))
        {
            return;
        }

        AddReaction(new RadioReactionPulse(reaction, tickClock(), false));
    }

    private void ApplyRequests(RadioSongRequest[]? incoming, bool? open)
    {
        RequestsOpen = open ?? RequestsOpen;
        if (incoming is null || incoming.Length == 0)
        {
            requests = Array.Empty<RadioRequestEntry>();
            HasOwnRequest = false;
            return;
        }

        var me = currentUserId();
        var built = new RadioRequestEntry[incoming.Length];
        var count = 0;
        var ownRequest = false;
        for (var index = 0; index < incoming.Length; index++)
        {
            var wire = incoming[index];
            if (wire is null || hiddenUserIds.Contains(wire.UserId))
            {
                continue;
            }

            var isMine = string.Equals(wire.UserId, me, StringComparison.Ordinal);
            ownRequest |= isMine;
            built[count++] = new RadioRequestEntry(wire.RequestId, wire.UserId,
                RadioRoomWire.PublicNameOf(wire.DisplayName, wire.Handle), wire.Handle,
                RadioRoomWire.HandleLabelOf(wire.Handle), wire.AvatarUrl, wire.Text,
                RadioRoomWire.RequestStateOf(wire.State), wire.CreatedAtUnixMs, isMine);
        }

        if (count != built.Length)
        {
            Array.Resize(ref built, count);
        }

        requests = built;
        HasOwnRequest = ownRequest;
    }

    private void ApplyRefusal(CallControl message)
    {
        var action = RadioRoomWire.ActionOf(message.Action);
        var kind = RadioRoomWire.RefusalKindOf(message.Reason);
        if (action == RadioRoomAction.Attach)
        {
            if (kind == RadioRefusalKind.Cooldown)
            {
                attachDueTick = tickClock() + RadioRoomPacing.AttachMilliseconds;
                return;
            }

            Status = RadioRoomStatus.Unavailable;
            attachDueTick = 0;
        }
        else if (kind == RadioRefusalKind.NotInRoom && Status == RadioRoomStatus.Attached)
        {
            Status = RadioRoomStatus.Attaching;
            Bump();
            RequestAttach();
        }

        if (action == RadioRoomAction.React)
        {
            return;
        }

        pendingRefusal = new RadioRefusal(action, kind);
        hasRefusal = true;
        Bump();
    }

    private RadioChatEntry BuildEntry(long messageId, string userId, string displayName, string handle,
        string? avatarUrl, string text, long sentAtUnixMs, bool isDj, string? me)
    {
        return RadioRoomWire.BuildEntry(messageId, userId, displayName, handle, avatarUrl, text, sentAtUnixMs,
            formatClock(sentAtUnixMs), isDj, me);
    }

    private void AppendMessage(RadioChatEntry entry)
    {
        messages.Append(entry);
    }

    private int RemoveMessages(long messageId, string? userId)
    {
        return messages.Remove(messageId, userId);
    }

    private RadioChatEntry? FindMessage(long messageId)
    {
        return messages.Find(messageId);
    }

    private RadioRequestEntry? FindRequest(long requestId)
    {
        for (var index = 0; index < requests.Length; index++)
        {
            if (requests[index].RequestId == requestId)
            {
                return requests[index];
            }
        }

        return null;
    }

    private int CountRequestsFrom(string userId)
    {
        var count = 0;
        for (var index = 0; index < requests.Length; index++)
        {
            if (string.Equals(requests[index].UserId, userId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static RadioRequestEntry[] FilterRequests(RadioRequestEntry[] source, string userId)
    {
        var kept = new List<RadioRequestEntry>(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            if (!string.Equals(source[index].UserId, userId, StringComparison.Ordinal))
            {
                kept.Add(source[index]);
            }
        }

        return kept.ToArray();
    }

    private void AddReaction(RadioReactionPulse pulse)
    {
        if (reactionCount < ReactionCapacity)
        {
            reactions[(reactionStart + reactionCount) % ReactionCapacity] = pulse;
            reactionCount++;
        }
        else
        {
            reactions[reactionStart] = pulse;
            reactionStart = (reactionStart + 1) % ReactionCapacity;
        }

        ReactionVersion++;
    }

    private void PruneReactions(long nowTick)
    {
        var pruned = false;
        while (reactionCount > 0 && nowTick - reactions[reactionStart].AtTick >= ReactionLifetimeMilliseconds)
        {
            reactionStart = (reactionStart + 1) % ReactionCapacity;
            reactionCount--;
            pruned = true;
        }

        if (pruned)
        {
            ReactionVersion++;
        }
    }

    private void ClearMessages()
    {
        messages.Clear();
    }

    private void ClearRoom()
    {
        ClearMessages();
        reactionStart = 0;
        reactionCount = 0;
        ReactionVersion++;
        requests = Array.Empty<RadioRequestEntry>();
        mutedUsers.Clear();
        Pinned = null;
        RequestsOpen = false;
        ListenerCount = 0;
        IsDj = false;
        IsModerator = false;
        IsLive = false;
        MutedUntilUnixMs = 0;
        HasOwnRequest = false;
        hasRefusal = false;
        attachDueTick = 0;
        chatPacer.Reset();
        reactionGate.Reset();
        requestGate.Reset();
    }

    private void Bump()
    {
        Version++;
    }

    private static long DefaultTick()
    {
        return Environment.TickCount64;
    }

    private static long DefaultUnix()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string DefaultClockLabel(long unixMs)
    {
        return TimeText.Clock(unixMs / 1000);
    }
}
