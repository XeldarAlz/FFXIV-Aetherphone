using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Online;

internal readonly struct OnlineKindInfo
{
    private const string HostIdPrefix = "games.host.";

    public readonly string Kind;
    public readonly string AccentId;
    public readonly string HostId;
    public readonly LocString Hint;
    public readonly int MaxPlayers;

    public OnlineKindInfo(string kind, string accentId, LocString hint, int maxPlayers)
    {
        Kind = kind;
        AccentId = accentId;
        HostId = HostIdPrefix + accentId;
        Hint = hint;
        MaxPlayers = maxPlayers;
    }
}

internal static class OnlineGameArt
{
    public const int MinPlayers = 2;
    private const int UnoMaxPlayers = 6;
    private const int LuckyDrawMaxPlayers = 6;
    private const int MiniGolfMaxPlayers = 4;
    private const int DuelMaxPlayers = 2;

    public static readonly OnlineKindInfo[] Infos =
    {
        new(GameRoomWire.UnoKind, "uno", L.Games.OnlineHostHint, UnoMaxPlayers),
        new(GameRoomWire.ChessKind, "chess", L.Games.OnlineChessHostHint, DuelMaxPlayers),
        new(GameRoomWire.PoolKind, "pool", L.Games.OnlinePoolHostHint, DuelMaxPlayers),
        new(GameRoomWire.ConnectFourKind, "connectfour", L.Games.OnlineConnectFourHostHint, DuelMaxPlayers),
        new(GameRoomWire.BroadsideKind, "broadside", L.Games.OnlineBroadsideHostHint, DuelMaxPlayers),
        new(GameRoomWire.LuckyDrawKind, OnlineLuckyDrawTable.AccentId, L.Games.OnlineLuckyDrawHostHint,
            LuckyDrawMaxPlayers),
        new(GameRoomWire.CraterKind, "crater", L.Games.OnlineCraterHostHint, GameRoomWire.CraterMaxPlayers),
        new(GameRoomWire.MiniGolfKind, OnlineMiniGolfTable.AccentId, L.Games.OnlineMiniGolfHostHint,
            MiniGolfMaxPlayers),
    };

    public static readonly string[] Kinds = KindsOf(Infos);

    public static int IndexOf(string? kind)
    {
        for (var index = 0; index < Infos.Length; index++)
        {
            if (string.Equals(Infos[index].Kind, kind, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public static ref readonly OnlineKindInfo Info(string? kind)
    {
        var index = IndexOf(kind);
        return ref Infos[index < 0 ? 0 : index];
    }

    public static string AccentId(string kind) => Info(kind).AccentId;

    public static Vector4 Accent(string kind) => AppAccents.For(AccentId(kind));

    public static int MaxPlayers(string kind) => Info(kind).MaxPlayers;

    public static string Hint(string kind)
    {
        ref readonly var info = ref Info(kind);
        return Loc.T(info.Hint, GameNumber.Label(info.MaxPlayers));
    }

    private static string[] KindsOf(OnlineKindInfo[] infos)
    {
        var kinds = new string[infos.Length];
        for (var index = 0; index < infos.Length; index++)
        {
            kinds[index] = infos[index].Kind;
        }

        return kinds;
    }
}
