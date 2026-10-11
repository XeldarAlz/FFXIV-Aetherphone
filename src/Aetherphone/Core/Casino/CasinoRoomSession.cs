using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed record CasinoRoomState(
    string RoomId,
    int Epoch,
    long Seq,
    CasinoRoomSnapshotDto Snapshot,
    CasinoWheelRoomStateDto? Wheel,
    CasinoBingoRoomStateDto? Bingo,
    CasinoBlackjackRoomStateDto? Blackjack,
    CasinoRaceRoomStateDto? Race = null,
    CasinoHoldemRoomStateDto? Holdem = null);

internal sealed record CasinoRoomPrivate(
    string RoomId,
    int Epoch,
    long Seq,
    CasinoPrivateDto Payload,
    CasinoBlackjackYouDto? Blackjack,
    CasinoHoldemYouDto? Holdem = null);

internal enum CasinoRoomApply
{
    Ignore,
    Apply,
    Resync,
}

internal sealed class CasinoRoomSession
{
    private const long ResyncCooldownMilliseconds = 2_000;

    private const long SkewReanchorMilliseconds = 5_000;
    private const int SkewSmoothingWeight = 4;

    private const long UnreachableAfterMilliseconds = 12_000;

    private readonly RealtimeSignalBus signals;
    private readonly object gate = new();

    private volatile CasinoRoomState? state;
    private volatile CasinoRoomPrivate? privateState;
    private volatile string roomId = string.Empty;
    private volatile string closedReason = string.Empty;
    private volatile bool attached;
    private volatile bool awaitingSnapshot;
    private volatile bool handedOff;
    private long skewMilliseconds;
    private long resyncAskedAtUnixMs;
    private long touchedAtTick;
    private bool skewAnchored;

    public CasinoRoomSession(RealtimeSignalBus signals)
    {
        this.signals = signals;
    }

    public CasinoRoomState? State => state;

    public CasinoRoomPrivate? Private => privateState;

    public string RoomId => roomId;

    public bool Attached => attached;

    public bool AwaitingSnapshot => awaitingSnapshot;

    public string ClosedReason => closedReason;

    public bool HandedOff => handedOff;

    public long SkewMilliseconds => Volatile.Read(ref skewMilliseconds);

    public bool Unreachable(long nowTick)
    {
        return Unreachable(attached, Volatile.Read(ref touchedAtTick), nowTick);
    }

    internal static bool Unreachable(bool socketAttached, long touchedAtTick, long nowTick)
    {
        if (socketAttached || touchedAtTick == 0)
        {
            return false;
        }

        return nowTick - touchedAtTick >= UnreachableAfterMilliseconds;
    }

    public long ServerNowUnixMs(long localNowUnixMs)
    {
        return localNowUnixMs + SkewMilliseconds;
    }

    public long RemainingMilliseconds(long deadlineUnixMs, long localNowUnixMs)
    {
        if (deadlineUnixMs <= 0)
        {
            return 0;
        }

        var remaining = deadlineUnixMs - ServerNowUnixMs(localNowUnixMs);
        return remaining > 0 ? remaining : 0;
    }

    public void AbsorbClock(long serverNowUnixMs, long localNowUnixMs)
    {
        AbsorbServerTime(serverNowUnixMs, localNowUnixMs);
    }

    public void Enter(string nextRoomId)
    {
        if (nextRoomId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (string.Equals(roomId, nextRoomId, StringComparison.Ordinal) && (attached || awaitingSnapshot))
            {
                return;
            }

            roomId = nextRoomId;
            state = null;
            privateState = null;
            closedReason = string.Empty;
            attached = false;
            awaitingSnapshot = true;
            resyncAskedAtUnixMs = 0;
            Touch();
            Send(SignalType.CasinoAttach, nextRoomId);
        }
    }

    public void Leave()
    {
        lock (gate)
        {
            var leaving = roomId;
            ClearRoom();
            if (leaving.Length > 0)
            {
                Send(SignalType.CasinoDetach, leaving);
            }
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            ClearRoom();
        }
    }

    public void Claim()
    {
        lock (gate)
        {
            var current = roomId;
            if (current.Length == 0)
            {
                return;
            }

            handedOff = false;
            Send(SignalType.CasinoClaim, current);
        }
    }

    public void OnRealtimeConnected(bool connected)
    {
        lock (gate)
        {
            var current = roomId;
            if (current.Length == 0)
            {
                return;
            }

            if (!connected)
            {
                attached = false;
                awaitingSnapshot = true;
                Touch();
                return;
            }

            awaitingSnapshot = true;
            Send(SignalType.CasinoAttach, current);
        }
    }

    public void Receive(CasinoSignal signal, long localNowUnixMs)
    {
        var payload = signal.Payload;
        if (payload is null)
        {
            return;
        }

        var current = roomId;
        if (current.Length == 0 || !string.Equals(payload.RoomId, current, StringComparison.Ordinal))
        {
            return;
        }

        Touch();
        switch (signal.Type)
        {
            case SignalType.CasinoAttached:
                attached = true;
                closedReason = string.Empty;
                AbsorbSnapshot(payload, localNowUnixMs);
                return;
            case SignalType.CasinoSnapshot:
                AbsorbSnapshot(payload, localNowUnixMs);
                return;
            case SignalType.CasinoEvent:
                AbsorbEvent(payload, localNowUnixMs);
                return;
            case SignalType.CasinoPrivate:
                handedOff = false;
                AbsorbPrivate(payload, localNowUnixMs);
                return;
            case SignalType.CasinoHandled:
                handedOff = true;
                return;
            case SignalType.CasinoDeclined:
            case SignalType.CasinoEnded:
                Close(payload.RoomId, signal.Reason ?? string.Empty);
                return;
        }
    }

    public void AbsorbHttpState(string requestedRoomId, CasinoRoomSnapshotDto fresh, long localNowUnixMs)
    {
        if (!string.Equals(roomId, requestedRoomId, StringComparison.Ordinal)
            || !string.Equals(fresh.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        Touch();
        AbsorbServerTime(fresh.ServerNowUnixMs, localNowUnixMs);
        Absorb(requestedRoomId, fresh.Epoch, fresh.Seq, fresh);
    }

    public void AbsorbHttpPrivate(string requestedRoomId, int epoch, long seq, CasinoBlackjackYouDto hand)
    {
        lock (gate)
        {
            if (!string.Equals(roomId, requestedRoomId, StringComparison.Ordinal)
                || !AcceptsPrivate(privateState, epoch, seq))
            {
                return;
            }

            privateState = new CasinoRoomPrivate(requestedRoomId, epoch, seq,
                new CasinoPrivateDto(CasinoWire.BlackjackHandEvent, string.Empty), hand);
        }
    }

    public void AbsorbHttpHoldemPrivate(string requestedRoomId, int epoch, long seq, CasinoHoldemYouDto hand)
    {
        lock (gate)
        {
            if (!string.Equals(roomId, requestedRoomId, StringComparison.Ordinal)
                || !AcceptsPrivate(privateState, epoch, seq))
            {
                return;
            }

            privateState = new CasinoRoomPrivate(requestedRoomId, epoch, seq,
                new CasinoPrivateDto(CasinoWire.BlackjackHandEvent, string.Empty), null, hand);
        }
    }

    public void CloseFromHttp(string requestedRoomId, string reason)
    {
        Close(requestedRoomId, reason);
    }

    internal static bool AcceptsSnapshot(CasinoRoomState? held, int epoch, long seq)
    {
        if (held is null)
        {
            return true;
        }

        if (epoch != held.Epoch)
        {
            return epoch > held.Epoch;
        }

        return seq >= held.Seq;
    }

    internal static bool AcceptsPrivate(CasinoRoomPrivate? held, int epoch, long seq)
    {
        if (held is null)
        {
            return true;
        }

        if (epoch != held.Epoch)
        {
            return epoch > held.Epoch;
        }

        return seq >= held.Seq;
    }

    internal static CasinoRoomApply Decide(CasinoRoomState? held, int epoch, long seq)
    {
        if (held is null)
        {
            return CasinoRoomApply.Resync;
        }

        if (epoch < held.Epoch)
        {
            return CasinoRoomApply.Ignore;
        }

        if (epoch > held.Epoch)
        {
            return CasinoRoomApply.Resync;
        }

        if (seq <= held.Seq)
        {
            return CasinoRoomApply.Ignore;
        }

        return seq == held.Seq + 1 ? CasinoRoomApply.Apply : CasinoRoomApply.Resync;
    }

    internal static CasinoRoomState Applied(CasinoRoomState held, int epoch, long seq, CasinoRoomEventDto change)
    {
        var next = held.Snapshot with
        {
            State = change.State,
            Phase = change.Phase,
            PhaseEndsAtUnixMs = change.PhaseEndsAtUnixMs,
            RoundIndex = change.RoundIndex,
            GameState = change.GameState,
            Occupancy = change.Occupancy,
            Epoch = epoch,
            Seq = seq,
        };

        return Build(held.RoomId, epoch, seq, next);
    }

    internal static CasinoRoomState Build(string roomId, int epoch, long seq, CasinoRoomSnapshotDto snapshot)
    {
        if (string.Equals(snapshot.GameKind, CasinoWire.WheelKind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoWheelRoomStateDto), null, null);
        }

        if (string.Equals(snapshot.GameKind, CasinoWire.BingoKind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot, null,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoBingoRoomStateDto), null);
        }

        if (string.Equals(snapshot.GameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot, null, null, null,
                Holdem: Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoHoldemRoomStateDto));
        }

        if (string.Equals(snapshot.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot, null, null,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto));
        }

        if (string.Equals(snapshot.GameKind, CasinoWire.RaceKind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot, null, null, null,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoRaceRoomStateDto));
        }

        return new CasinoRoomState(roomId, epoch, seq, snapshot, null, null, null);
    }

    internal static long SmoothedSkew(long held, long sample)
    {
        var drift = sample - held;
        if (drift >= SkewReanchorMilliseconds || drift <= -SkewReanchorMilliseconds)
        {
            return sample;
        }

        return held + drift / SkewSmoothingWeight;
    }

    private static TState? Parse<TState>(string gameState,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TState> typeInfo)
        where TState : class
    {
        if (gameState.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(gameState, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void AbsorbSnapshot(CasinoPayload payload, long localNowUnixMs)
    {
        var snapshot = payload.Snapshot;
        if (snapshot is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        Absorb(payload.RoomId, payload.Epoch, payload.Seq, snapshot);
    }

    private void Absorb(string absorbedRoomId, int epoch, long seq, CasinoRoomSnapshotDto snapshot)
    {
        lock (gate)
        {
            if (!string.Equals(roomId, absorbedRoomId, StringComparison.Ordinal)
                || !AcceptsSnapshot(state, epoch, seq))
            {
                return;
            }

            state = Build(absorbedRoomId, epoch, seq, snapshot);
            awaitingSnapshot = false;
            resyncAskedAtUnixMs = 0;
        }
    }

    private void AbsorbEvent(CasinoPayload payload, long localNowUnixMs)
    {
        var change = payload.Event;
        if (change is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        var asksForResync = false;
        lock (gate)
        {
            if (!string.Equals(roomId, payload.RoomId, StringComparison.Ordinal))
            {
                return;
            }

            var held = state;
            var decision = Decide(held, payload.Epoch, payload.Seq);
            if (decision == CasinoRoomApply.Apply && held is not null)
            {
                state = Applied(held, payload.Epoch, payload.Seq, change);
            }
            else if (decision == CasinoRoomApply.Resync)
            {
                awaitingSnapshot = true;
                asksForResync = AsksForResync(localNowUnixMs);
            }
        }

        if (asksForResync)
        {
            Send(SignalType.CasinoResync, payload.RoomId);
        }
    }

    private void AbsorbPrivate(CasinoPayload payload, long localNowUnixMs)
    {
        var personal = payload.Private;
        if (personal is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        lock (gate)
        {
            if (!string.Equals(roomId, payload.RoomId, StringComparison.Ordinal)
                || !AcceptsPrivate(privateState, payload.Epoch, payload.PairSeq))
            {
                return;
            }

            var holdem = string.Equals(state?.Snapshot.GameKind, HoldemRules.Kind, StringComparison.Ordinal);
            privateState = holdem
                ? new CasinoRoomPrivate(payload.RoomId, payload.Epoch, payload.PairSeq, personal, null,
                    BuildHoldemPrivate(personal))
                : new CasinoRoomPrivate(payload.RoomId, payload.Epoch, payload.PairSeq, personal,
                    BuildPrivate(personal));
        }
    }

    internal static CasinoBlackjackYouDto? BuildPrivate(CasinoPrivateDto personal)
    {
        if (!string.Equals(personal.EventKind, CasinoWire.BlackjackHandEvent, StringComparison.Ordinal))
        {
            return null;
        }

        return Parse(personal.Payload, AethernetJsonContext.Default.CasinoBlackjackYouDto);
    }

    internal static CasinoHoldemYouDto? BuildHoldemPrivate(CasinoPrivateDto personal)
    {
        if (!string.Equals(personal.EventKind, CasinoWire.BlackjackHandEvent, StringComparison.Ordinal))
        {
            return null;
        }

        return Parse(personal.Payload, AethernetJsonContext.Default.CasinoHoldemYouDto);
    }

    private bool AsksForResync(long localNowUnixMs)
    {
        if (resyncAskedAtUnixMs != 0 && localNowUnixMs - resyncAskedAtUnixMs < ResyncCooldownMilliseconds)
        {
            return false;
        }

        resyncAskedAtUnixMs = localNowUnixMs;
        return true;
    }

    private void AbsorbServerTime(long serverNowUnixMs, long localNowUnixMs)
    {
        if (serverNowUnixMs <= 0)
        {
            return;
        }

        lock (gate)
        {
            var sample = serverNowUnixMs - localNowUnixMs;
            Volatile.Write(ref skewMilliseconds, skewAnchored ? SmoothedSkew(skewMilliseconds, sample) : sample);
            skewAnchored = true;
        }
    }

    private void Close(string closingRoomId, string reason)
    {
        lock (gate)
        {
            if (!string.Equals(roomId, closingRoomId, StringComparison.Ordinal))
            {
                return;
            }

            ClearRoom();
            closedReason = reason;
        }
    }

    private void ClearRoom()
    {
        roomId = string.Empty;
        state = null;
        privateState = null;
        closedReason = string.Empty;
        attached = false;
        awaitingSnapshot = false;
        handedOff = false;
        resyncAskedAtUnixMs = 0;
        Volatile.Write(ref touchedAtTick, 0);
    }

    private void Touch()
    {
        Volatile.Write(ref touchedAtTick, Environment.TickCount64);
    }

    private void Send(string type, string targetRoomId)
    {
        signals.TrySend(new CallControl
        {
            Type = type,
            Casino = new CasinoPayload { RoomId = targetRoomId },
        });
    }
}
