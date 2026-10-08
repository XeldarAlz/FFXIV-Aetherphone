using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoTurnNotifier : IDisposable
{
    public const string AppId = "casino";

    public const string GroupPrefix = "casino:";

    private const long AttentionWindowMilliseconds = 1_200;

    private readonly AethernetSession session;
    private readonly CasinoRoomsStore rooms;
    private readonly NotificationService notifications;
    private readonly Vector4 accent;

    private long attentionStampedAtTick;
    private string spokenTurnKey = string.Empty;

    public CasinoTurnNotifier(AethernetSession session, CasinoRoomsStore rooms, NotificationService notifications,
        Vector4 accent)
    {
        this.session = session;
        this.rooms = rooms;
        this.notifications = notifications;
        this.accent = accent;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void StampAttention()
    {
        Interlocked.Exchange(ref attentionStampedAtTick, Environment.TickCount64);
    }

    public void Forget()
    {
        spokenTurnKey = string.Empty;
    }

    internal static string TurnKeyFor(string handId, int seatIndex, int splitIndex)
    {
        return string.Concat(handId, ":", seatIndex.ToString(Loc.Culture), ":",
            splitIndex.ToString(Loc.Culture));
    }

    internal static string? BlackjackTurnKey(Aethernet.Contracts.CasinoBlackjackRoomStateDto? board,
        Aethernet.Contracts.CasinoBlackjackYouDto? mine)
    {
        if (board is null || mine is null || board.HandId.Length == 0 || mine.ActiveHand < 0
            || !string.Equals(mine.HandId, board.HandId, StringComparison.Ordinal))
        {
            return null;
        }

        return TurnKeyFor(board.HandId, mine.SeatIndex, mine.ActiveHand);
    }

    internal static string? HoldemTurnKey(Aethernet.Contracts.CasinoHoldemRoomStateDto? board,
        Aethernet.Contracts.CasinoHoldemYouDto? mine)
    {
        if (board is null || mine?.Prompt is null || board.HandId.Length == 0 || board.CursorSeat != mine.SeatIndex
            || HoldemActions.OnlyShowOrMuck(mine.Prompt.Actions)
            || !string.Equals(mine.HandId, board.HandId, StringComparison.Ordinal))
        {
            return null;
        }

        return TurnKeyFor(board.HandId, mine.SeatIndex, board.ActionCount);
    }

    internal static bool Watching(long stampedAtTick, long nowTick)
    {
        return stampedAtTick != 0 && nowTick - stampedAtTick <= AttentionWindowMilliseconds;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var key = HoldemTurnKey(rooms.Room.State?.Holdem, rooms.Room.Private?.Holdem)
            ?? BlackjackTurnKey(rooms.Room.State?.Blackjack, rooms.Room.Private?.Blackjack);
        if (key is null || string.Equals(spokenTurnKey, key, StringComparison.Ordinal))
        {
            return;
        }

        spokenTurnKey = key;
        if (Watching(Interlocked.Read(ref attentionStampedAtTick), Environment.TickCount64))
        {
            return;
        }

        var roomId = rooms.Room.RoomId;
        notifications.Notify(new PhoneNotification(AppId, Loc.T(L.Casino.NotifyTurnTitle),
            Loc.T(L.Casino.NotifyTurnBody), DateTime.Now, accent, string.Concat(GroupPrefix, roomId))
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }
}
