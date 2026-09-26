using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Telephony.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Games;

internal enum GameRoomIntent
{
    Created,
    Joined,
    Left,
    Closed,
    Kicked,
}

internal sealed record GameRoomAnswer(GameRoomIntent Intent, bool Granted, string Reason,
    GameRoomCardDto? Room);

internal sealed record GameActOutcome(bool Granted, string Reason);

// The friend rooms store: a directory of the rooms this account is in, one live room session over
// the game.* socket lane, and the HTTP fallback that keeps a dead socket playable. Moves broadcast
// from the acting request server-side, so the fallback poll is a safety net, never the transport.
internal sealed class GameRoomsStore : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ForegroundRoomPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BackgroundRoomPollInterval = TimeSpan.FromSeconds(10);
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const long YouCooldownMilliseconds = 2_000;
    private const int NotFoundStatus = 404;

    private readonly AethernetSession session;
    private readonly GamesClient games;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence directoryCadence;
    private readonly PollCadence roomCadence;
    private readonly GameRoomSession room;
    private readonly StoreWork work = new("GameRooms");
    private readonly Action<int> roomStatusSink;

    private volatile GameRoomCardDto[] rooms = Array.Empty<GameRoomCardDto>();
    private volatile bool loadingRooms;
    private volatile bool loadedRooms;
    private volatile bool intentInFlight;
    private volatile bool actInFlight;
    private GameRoomAnswer? roomAnswer;
    private GameActOutcome? actOutcome;
    private int roomsFailed;
    private int fetchingRooms;
    private int fetchingRoomState;
    private int fetchingYou;
    private int roomGoneStatus;
    private long roomsAttemptedAtTick;
    private long roomAttemptedAtTick;
    private long youAskedAtTick;
    private string? lastAccountId;

    public GameRoomsStore(AethernetSession session, GamesClient games, PhoneVisibility visibility,
        RealtimeSignalBus signals)
    {
        this.session = session;
        this.games = games;
        this.signals = signals;
        directoryCadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        roomCadence = new PollCadence(visibility, ForegroundRoomPollInterval, BackgroundRoomPollInterval);
        room = new GameRoomSession(signals);
        roomStatusSink = OnRoomStatus;
        session.Changed += OnSessionChanged;
        signals.GameReceived += OnGameSignal;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public GameRoomSession Room => room;

    public string AccountId => session.CurrentUser?.Id ?? string.Empty;

    public GameRoomCardDto[] Rooms => rooms;

    public bool LoadingRooms => loadingRooms;

    public bool LoadedRooms => loadedRooms;

    public bool IntentInFlight => intentInFlight;

    public bool ActInFlight => actInFlight;

    public GameRoomAnswer? TakeRoomAnswer()
    {
        return Interlocked.Exchange(ref roomAnswer, null);
    }

    public GameActOutcome? TakeActOutcome()
    {
        return Interlocked.Exchange(ref actOutcome, null);
    }

    public bool TakeRoomsFailure()
    {
        return Interlocked.Exchange(ref roomsFailed, 0) != 0;
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn || !directoryCadence.Due(DateTime.UtcNow))
        {
            return;
        }

        RefreshRooms();
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        directoryCadence.Reset();
        RefreshRooms();
    }

    public void CreateRoom(string gameKind)
    {
        if (intentInFlight || !session.IsSignedIn)
        {
            return;
        }

        intentInFlight = true;
        var clientRoomId = Guid.NewGuid().ToString("N");
        work.Run("create room", async token =>
        {
            var result = await games.CreateRoomAsync(clientRoomId, gameKind, token).ConfigureAwait(false);
            Answer(GameRoomIntent.Created, result?.Granted ?? false, result?.Reason ?? string.Empty,
                result?.Room);
        }, () => intentInFlight = false);
    }

    public void JoinByCode(string code)
    {
        if (intentInFlight || !session.IsSignedIn || code.Length == 0)
        {
            return;
        }

        intentInFlight = true;
        work.Run("join room", async token =>
        {
            var result = await games.JoinByCodeAsync(code, token).ConfigureAwait(false);
            Answer(GameRoomIntent.Joined, result?.Granted ?? false, result?.Reason ?? string.Empty,
                result?.Room);
        }, () => intentInFlight = false);
    }

    public void LeaveRoom(string roomId)
    {
        if (intentInFlight || !session.IsSignedIn || roomId.Length == 0)
        {
            return;
        }

        if (string.Equals(room.RoomId, roomId, StringComparison.Ordinal))
        {
            room.Leave();
        }

        intentInFlight = true;
        work.Run("leave room", async token =>
        {
            var result = await games.LeaveAsync(roomId, token).ConfigureAwait(false);
            Answer(GameRoomIntent.Left, result?.Granted ?? false, result?.Reason ?? string.Empty, null);
        }, () => intentInFlight = false);
    }

    public void CloseRoom(string roomId)
    {
        if (intentInFlight || !session.IsSignedIn || roomId.Length == 0)
        {
            return;
        }

        if (string.Equals(room.RoomId, roomId, StringComparison.Ordinal))
        {
            room.Leave();
        }

        intentInFlight = true;
        work.Run("close room", async token =>
        {
            var result = await games.CloseAsync(roomId, token).ConfigureAwait(false);
            Answer(GameRoomIntent.Closed, result?.Granted ?? false, result?.Reason ?? string.Empty, null);
        }, () => intentInFlight = false);
    }

    public void Kick(string userId)
    {
        var target = room.RoomId;
        if (intentInFlight || !session.IsSignedIn || target.Length == 0 || userId.Length == 0)
        {
            return;
        }

        intentInFlight = true;
        work.Run("kick member", async token =>
        {
            var result = await games.KickAsync(target, userId, token).ConfigureAwait(false);
            Answer(GameRoomIntent.Kicked, result?.Granted ?? false, result?.Reason ?? string.Empty, null);
        }, () => intentInFlight = false);
    }

    public void Enter(string roomId)
    {
        if (roomId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        room.Enter(roomId);
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        Interlocked.Exchange(ref youAskedAtTick, 0);
        RefreshRoomState();
    }

    public void Exit()
    {
        room.Leave();
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
    }

    public void SendStart(int ruleSet = GameRoomWire.RuleSetDefault)
    {
        SendAction(GameRoomWire.ActionStart, ruleSet, -1);
    }

    public void SendPlay(int card, int chosenColor)
    {
        SendAction(GameRoomWire.ActionPlay, card, chosenColor);
    }

    public void SendDraw()
    {
        SendAction(GameRoomWire.ActionDraw, -1, -1);
    }

    public void SendPass()
    {
        SendAction(GameRoomWire.ActionPass, -1, -1);
    }

    public void SendMove(int from, int to, int promotion)
    {
        SendAction(GameRoomWire.ActionMove, -1, promotion, from, to);
    }

    public void SendResign()
    {
        SendAction(GameRoomWire.ActionResign, -1, -1);
    }

    public void SendDrop(int column)
    {
        SendAction(GameRoomWire.ActionDrop, -1, -1, column: column);
    }

    public void SendShoot(float angle, float power)
    {
        SendAction(GameRoomWire.ActionShoot, -1, -1, -1, -1, angle, power);
    }

    public void SendPlace(float x, float y)
    {
        SendAction(GameRoomWire.ActionPlace, -1, -1, -1, -1, 0f, 0f, x, y);
    }

    // Every action names the action count it was decided against; the server refuses a mismatch as
    // stale rather than applying it twice, so a lost response costs one refresh and never a double
    // move.
    private void SendAction(string action, int card, int color, int from = -1, int to = -1,
        float angle = 0f, float power = 0f, float placeX = 0f, float placeY = 0f, int column = -1)
    {
        var target = room.RoomId;
        var roster = room.State?.Roster;
        if (actInFlight || !session.IsSignedIn || target.Length == 0 || roster is null)
        {
            return;
        }

        var request = new GameRoomActionRequest(action, roster.ActionCount, card, color,
            Guid.NewGuid().ToString("N"), from, to, angle, power, placeX, placeY, column);
        actInFlight = true;
        work.Run("room action", async token =>
        {
            var result = await games.ActAsync(target, request, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref actOutcome, new GameActOutcome(false, string.Empty));
                return;
            }

            Interlocked.Exchange(ref actOutcome, new GameActOutcome(result.Granted, result.Reason));
            if (!result.Granted
                && string.Equals(result.Reason, GameRoomWire.ReasonStaleAction, StringComparison.Ordinal))
            {
                Interlocked.Exchange(ref roomAttemptedAtTick, 0);
                roomCadence.RequestImmediate();
            }
        }, () => actInFlight = false);
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    private static long NowUnixMilliseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void Answer(GameRoomIntent intent, bool granted, string reason, GameRoomCardDto? card)
    {
        Interlocked.Exchange(ref roomAnswer, new GameRoomAnswer(intent, granted, reason, card));
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        directoryCadence.RequestImmediate();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn || room.RoomId.Length == 0)
        {
            return;
        }

        SyncYou();
        var nowUtc = DateTime.UtcNow;
        if (signals.RealtimeActive && room.Attached && !room.AwaitingSnapshot)
        {
            roomCadence.Mark(nowUtc);
            return;
        }

        if (!roomCadence.Due(nowUtc))
        {
            return;
        }

        RefreshRoomState();
    }

    // The hand is stale the moment the board has moved past it. While the socket is live the
    // private lane usually lands first and this never fires; on a dead socket it is the only way
    // cards arrive.
    private void SyncYou()
    {
        var target = room.RoomId;
        var board = room.State?.Uno;
        if (target.Length == 0 || board is null)
        {
            return;
        }

        var mine = room.Private?.Uno;
        var seated = false;
        var players = board.Players ?? Array.Empty<UnoPlayerDto>();
        var me = AccountId;
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, me, StringComparison.Ordinal))
            {
                seated = true;
                break;
            }
        }

        if (!seated || (mine is not null && mine.ActionCount >= board.ActionCount))
        {
            return;
        }

        var nowTick = Environment.TickCount64;
        if (Interlocked.Read(ref youAskedAtTick) != 0
            && nowTick - Interlocked.Read(ref youAskedAtTick) < YouCooldownMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetchingYou, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref youAskedAtTick, nowTick);
        work.Run("room you", async token =>
        {
            var you = await games.YouAsync(target, token).ConfigureAwait(false);
            if (you is null || you.Payload.Length == 0)
            {
                return;
            }

            var mineFresh = GameRoomSession.BuildPrivate(new GamePrivateDto(you.EventKind, you.Payload));
            if (mineFresh is not null)
            {
                room.AbsorbHttpPrivate(target, you.Epoch, you.Seq, mineFresh);
            }
        }, () => Interlocked.Exchange(ref fetchingYou, 0));
    }

    private void OnGameSignal(GameSignal signal)
    {
        room.Receive(signal, NowUnixMilliseconds());
        if (string.Equals(signal.Type, SignalType.GameEnded, StringComparison.Ordinal))
        {
            directoryCadence.RequestImmediate();
        }
    }

    private void OnRealtimeConnected(bool connected)
    {
        room.OnRealtimeConnected(connected);
        if (connected)
        {
            directoryCadence.RequestAfterReconnect();
            return;
        }

        roomCadence.Reset();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        room.Reset();
        rooms = Array.Empty<GameRoomCardDto>();
        loadedRooms = false;
        Interlocked.Exchange(ref roomsFailed, 0);
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        Interlocked.Exchange(ref roomAnswer, null);
        Interlocked.Exchange(ref actOutcome, null);
        directoryCadence.Reset();
        roomCadence.Reset();
    }

    private void RefreshRooms()
    {
        if (!session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref roomsAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingRooms, 1) != 0)
        {
            return;
        }

        loadingRooms = true;
        Interlocked.Exchange(ref roomsAttemptedAtTick, Environment.TickCount64);
        work.Run("rooms directory", async token =>
        {
            var directory = await games.RoomsAsync(token).ConfigureAwait(false);
            if (directory is null)
            {
                Interlocked.Exchange(ref roomsFailed, 1);
                return;
            }

            Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
            rooms = directory.Rooms ?? Array.Empty<GameRoomCardDto>();
            loadedRooms = true;
        }, () =>
        {
            loadingRooms = false;
            Interlocked.Exchange(ref fetchingRooms, 0);
        });
    }

    private void OnRoomStatus(int statusCode)
    {
        if (statusCode == NotFoundStatus)
        {
            Interlocked.Exchange(ref roomGoneStatus, 1);
        }
    }

    private void RefreshRoomState()
    {
        var target = room.RoomId;
        if (target.Length == 0 || !session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref roomAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingRoomState, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref roomAttemptedAtTick, Environment.TickCount64);
        Interlocked.Exchange(ref roomGoneStatus, 0);
        work.Run("room state", async token =>
        {
            var fresh = await games.RoomStateAsync(target, roomStatusSink, token).ConfigureAwait(false);
            if (fresh is null)
            {
                if (Interlocked.Exchange(ref roomGoneStatus, 0) != 0)
                {
                    Interlocked.Exchange(ref roomAttemptedAtTick, 0);
                    room.CloseFromHttp(target, GameRoomWire.ReasonEnded);
                }

                return;
            }

            Interlocked.Exchange(ref roomAttemptedAtTick, 0);
            room.AbsorbHttpState(target, fresh, NowUnixMilliseconds());
        }, () => Interlocked.Exchange(ref fetchingRoomState, 0));
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        session.Changed -= OnSessionChanged;
        signals.GameReceived -= OnGameSignal;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
