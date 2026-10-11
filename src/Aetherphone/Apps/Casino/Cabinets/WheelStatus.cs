using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum WheelStatusKind : byte
{
    Reconnecting,
    Open,
    Closed,
    Spinning,
    Next,
}

internal readonly struct WheelStatus
{
    public const int NoClock = -1;

    public readonly WheelStatusKind Kind;
    public readonly int Seconds;
    public readonly bool Urgent;

    private WheelStatus(WheelStatusKind kind, int seconds, bool urgent)
    {
        Kind = kind;
        Seconds = seconds;
        Urgent = urgent;
    }

    public bool HasClock => Seconds != NoClock;

    public LocString Label => Kind switch
    {
        WheelStatusKind.Reconnecting => L.Casino.WheelReconnecting,
        WheelStatusKind.Open => L.Casino.WheelPlaceBets,
        WheelStatusKind.Closed => L.Casino.WheelBetsClosed,
        WheelStatusKind.Spinning => L.Casino.WheelSpinning,
        _ => L.Casino.WheelNextSpin,
    };

    public static WheelStatus Of(bool unreachable, int phase, WheelStage stage, long remainingMilliseconds)
    {
        if (unreachable)
        {
            return new WheelStatus(WheelStatusKind.Reconnecting, NoClock, false);
        }

        var seconds = SecondsLeft(remainingMilliseconds);
        if (stage == WheelStage.Settling)
        {
            var next = phase == CasinoRoomPhases.Result ? seconds : seconds + CasinoRoomCadence.WheelResultSeconds;
            return new WheelStatus(WheelStatusKind.Next, next, false);
        }

        if (phase == CasinoRoomPhases.Open)
        {
            return new WheelStatus(WheelStatusKind.Open, seconds,
                TurnTimerRing.IsUrgent(remainingMilliseconds, CasinoRoomCadence.WheelOpenSeconds));
        }

        var kind = stage == WheelStage.Locking ? WheelStatusKind.Closed : WheelStatusKind.Spinning;
        return new WheelStatus(kind, NoClock, false);
    }

    public static int SecondsLeft(long remainingMilliseconds) =>
        (int)((Math.Max(0L, remainingMilliseconds) + 999L) / 1000L);
}
