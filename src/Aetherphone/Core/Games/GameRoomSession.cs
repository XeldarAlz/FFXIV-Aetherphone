using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Games;

internal sealed record GameRoomMemberView(
    string UserId,
    string DisplayName,
    int Seat,
    bool Away,
    int Wins);

// The kind-agnostic slice every room screen needs: who is here, who hosts, how far the action
// count has moved and who won. The lobby, the roster and the act guard read this; only the table
// itself reaches into the kind-specific board.
internal sealed record GameRoomRoster(
    string HostUserId,
    GameRoomMemberView[] Players,
    int ActionCount,
    int WinnerSeat);

internal sealed record GameRoomState(
    string RoomId,
    int Epoch,
    long Seq,
    GameRoomSnapshotDto Snapshot,
    UnoRoomStateDto? Uno,
    ChessRoomStateDto? Chess,
    PoolRoomStateDto? Pool,
    ConnectFourRoomStateDto? ConnectFour,
    GameRoomRoster? Roster);

internal sealed record GameRoomPrivate(
    string RoomId,
    int Epoch,
    long Seq,
    UnoYouDto? Uno);

internal enum GameRoomApply
{
    Ignore,
    Apply,
    Resync,
}

// The client half of the game.* room protocol, cut from CasinoRoomSession's cloth: epoch and
// sequence decide whether an event applies, a gap asks for a snapshot instead of guessing, the
// private hand rides its own PairSeq lane, and the server clock is smoothed so turn countdowns
// stay honest across jitter.
internal sealed class GameRoomSession
{
    private const long ResyncCooldownMilliseconds = 2_000;

    private const long SkewReanchorMilliseconds = 5_000;
    private const int SkewSmoothingWeight = 4;

    private const long UnreachableAfterMilliseconds = 12_000;

    private readonly RealtimeSignalBus signals;
    private readonly object gate = new();

    private volatile GameRoomState? state;
    private volatile GameRoomPrivate? privateState;
    private volatile string roomId = string.Empty;
    private volatile string closedReason = string.Empty;
    private volatile bool attached;
    private volatile bool awaitingSnapshot;
    private long skewMilliseconds;
    private long resyncAskedAtUnixMs;
    private long touchedAtTick;
    private bool skewAnchored;

    public GameRoomSession(RealtimeSignalBus signals)
    {
        this.signals = signals;
    }

    public GameRoomState? State => state;

    public GameRoomPrivate? Private => privateState;

    public string RoomId => roomId;

    public bool Attached => attached;

    public bool AwaitingSnapshot => awaitingSnapshot;

    public string ClosedReason => closedReason;

    public long SkewMilliseconds => Volatile.Read(ref skewMilliseconds);

    public bool Unreachable(long nowTick)
    {
        if (attached || Volatile.Read(ref touchedAtTick) == 0)
        {
            return false;
        }

        return nowTick - Volatile.Read(ref touchedAtTick) >= UnreachableAfterMilliseconds;
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
            Send(SignalType.GameAttach, nextRoomId);
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
                Send(SignalType.GameDetach, leaving);
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
            Send(SignalType.GameAttach, current);
        }
    }

    public void Receive(GameSignal signal, long localNowUnixMs)
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
            case SignalType.GameAttached:
                attached = true;
                closedReason = string.Empty;
                AbsorbSnapshot(payload, localNowUnixMs);
                return;
            case SignalType.GameSnapshot:
                AbsorbSnapshot(payload, localNowUnixMs);
                return;
            case SignalType.GameEvent:
                AbsorbEvent(payload, localNowUnixMs);
                return;
            case SignalType.GamePrivate:
                AbsorbPrivate(payload, localNowUnixMs);
                return;
            case SignalType.GameDeclined:
            case SignalType.GameEnded:
                Close(payload.RoomId, signal.Reason ?? string.Empty);
                return;
        }
    }

    public void AbsorbHttpState(string requestedRoomId, GameRoomSnapshotDto fresh, long localNowUnixMs)
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

    public void AbsorbHttpPrivate(string requestedRoomId, int epoch, long seq, UnoYouDto mine)
    {
        lock (gate)
        {
            if (!string.Equals(roomId, requestedRoomId, StringComparison.Ordinal)
                || !AcceptsPrivate(privateState, epoch, seq))
            {
                return;
            }

            privateState = new GameRoomPrivate(requestedRoomId, epoch, seq, mine);
        }
    }

    public void CloseFromHttp(string requestedRoomId, string reason)
    {
        Close(requestedRoomId, reason);
    }

    internal static bool AcceptsSnapshot(GameRoomState? held, int epoch, long seq)
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

    internal static bool AcceptsPrivate(GameRoomPrivate? held, int epoch, long seq)
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

    internal static GameRoomApply Decide(GameRoomState? held, int epoch, long seq)
    {
        if (held is null)
        {
            return GameRoomApply.Resync;
        }

        if (epoch < held.Epoch)
        {
            return GameRoomApply.Ignore;
        }

        if (epoch > held.Epoch)
        {
            return GameRoomApply.Resync;
        }

        if (seq <= held.Seq)
        {
            return GameRoomApply.Ignore;
        }

        return seq == held.Seq + 1 ? GameRoomApply.Apply : GameRoomApply.Resync;
    }

    internal static GameRoomState Applied(GameRoomState held, int epoch, long seq, GameRoomEventDto change)
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

    internal static GameRoomState Build(string roomId, int epoch, long seq, GameRoomSnapshotDto snapshot)
    {
        if (string.Equals(snapshot.GameKind, GameRoomWire.UnoKind, StringComparison.Ordinal))
        {
            var uno = Parse(snapshot.GameState, AethernetJsonContext.Default.UnoRoomStateDto);
            return new GameRoomState(roomId, epoch, seq, snapshot, uno, null, null, null, RosterOf(uno));
        }

        if (string.Equals(snapshot.GameKind, GameRoomWire.ChessKind, StringComparison.Ordinal))
        {
            var chess = Parse(snapshot.GameState, AethernetJsonContext.Default.ChessRoomStateDto);
            return new GameRoomState(roomId, epoch, seq, snapshot, null, chess, null, null, RosterOf(chess));
        }

        if (string.Equals(snapshot.GameKind, GameRoomWire.PoolKind, StringComparison.Ordinal))
        {
            var pool = Parse(snapshot.GameState, AethernetJsonContext.Default.PoolRoomStateDto);
            return new GameRoomState(roomId, epoch, seq, snapshot, null, null, pool, null, RosterOf(pool));
        }

        if (string.Equals(snapshot.GameKind, GameRoomWire.ConnectFourKind, StringComparison.Ordinal))
        {
            var connectFour = Parse(snapshot.GameState, AethernetJsonContext.Default.ConnectFourRoomStateDto);
            return new GameRoomState(roomId, epoch, seq, snapshot, null, null, null, connectFour,
                RosterOf(connectFour));
        }

        return new GameRoomState(roomId, epoch, seq, snapshot, null, null, null, null, null);
    }

    private static GameRoomRoster? RosterOf(ConnectFourRoomStateDto? connectFour)
    {
        if (connectFour is null)
        {
            return null;
        }

        var players = connectFour.Players ?? Array.Empty<ConnectFourPlayerDto>();
        var members = new GameRoomMemberView[players.Length];
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            members[index] = new GameRoomMemberView(player.UserId, player.DisplayName, player.Seat,
                player.Away, player.Wins);
        }

        return new GameRoomRoster(connectFour.HostUserId, members, connectFour.ActionCount, connectFour.WinnerSeat);
    }

    private static GameRoomRoster? RosterOf(PoolRoomStateDto? pool)
    {
        if (pool is null)
        {
            return null;
        }

        var players = pool.Players ?? Array.Empty<PoolPlayerDto>();
        var members = new GameRoomMemberView[players.Length];
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            members[index] = new GameRoomMemberView(player.UserId, player.DisplayName, player.Seat,
                player.Away, player.Wins);
        }

        return new GameRoomRoster(pool.HostUserId, members, pool.ActionCount, pool.WinnerSeat);
    }

    private static GameRoomRoster? RosterOf(UnoRoomStateDto? uno)
    {
        if (uno is null)
        {
            return null;
        }

        var players = uno.Players ?? Array.Empty<UnoPlayerDto>();
        var members = new GameRoomMemberView[players.Length];
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            members[index] = new GameRoomMemberView(player.UserId, player.DisplayName, player.Seat,
                player.Away, player.Wins);
        }

        return new GameRoomRoster(uno.HostUserId, members, uno.ActionCount, uno.WinnerSeat);
    }

    private static GameRoomRoster? RosterOf(ChessRoomStateDto? chess)
    {
        if (chess is null)
        {
            return null;
        }

        var players = chess.Players ?? Array.Empty<ChessPlayerDto>();
        var members = new GameRoomMemberView[players.Length];
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            members[index] = new GameRoomMemberView(player.UserId, player.DisplayName, player.Seat,
                player.Away, player.Wins);
        }

        return new GameRoomRoster(chess.HostUserId, members, chess.ActionCount, chess.WinnerSeat);
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

    private void AbsorbSnapshot(GamePayload payload, long localNowUnixMs)
    {
        var snapshot = payload.Snapshot;
        if (snapshot is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        Absorb(payload.RoomId, payload.Epoch, payload.Seq, snapshot);
    }

    private void Absorb(string absorbedRoomId, int epoch, long seq, GameRoomSnapshotDto snapshot)
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

    private void AbsorbEvent(GamePayload payload, long localNowUnixMs)
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
            if (decision == GameRoomApply.Apply && held is not null)
            {
                state = Applied(held, payload.Epoch, payload.Seq, change);
            }
            else if (decision == GameRoomApply.Resync)
            {
                awaitingSnapshot = true;
                asksForResync = AsksForResync(localNowUnixMs);
            }
        }

        if (asksForResync)
        {
            Send(SignalType.GameResync, payload.RoomId);
        }
    }

    private void AbsorbPrivate(GamePayload payload, long localNowUnixMs)
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

            privateState = new GameRoomPrivate(payload.RoomId, payload.Epoch, payload.PairSeq,
                BuildPrivate(personal));
        }
    }

    internal static UnoYouDto? BuildPrivate(GamePrivateDto personal)
    {
        if (!string.Equals(personal.EventKind, GameRoomWire.UnoHandEvent, StringComparison.Ordinal))
        {
            return null;
        }

        return Parse(personal.Payload, AethernetJsonContext.Default.UnoYouDto);
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
            Game = new GamePayload { RoomId = targetRoomId },
        });
    }
}
