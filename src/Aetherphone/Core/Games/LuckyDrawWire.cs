namespace Aetherphone.Core.Games;

internal static class LuckyDrawWire
{
    public const int PhaseIdle = 0;

    public const int PhaseTurn = 1;

    public const int PhaseTarget = 2;

    public const int PhaseRoundOver = 3;

    public const int PhaseMatchOver = 4;

    public const int SeatActive = 0;

    public const int SeatStayed = 1;

    public const int SeatFrozen = 2;

    public const int SeatBusted = 3;

    public const int SeatOut = 4;

    public const string EventRound = "round";

    public const string EventDrew = "drew";

    public const string EventKept = "kept";

    public const string EventBust = "bust";

    public const string EventSwept = "swept";

    public const string EventSaved = "saved";

    public const string EventSeven = "seven";

    public const string EventChanceKept = "chanceKept";

    public const string EventChanceGiven = "chanceGiven";

    public const string EventChanceDiscarded = "chanceDiscarded";

    public const string EventChooseTarget = "chooseTarget";

    public const string EventFrozen = "frozen";

    public const string EventFlipThree = "flipThree";

    public const string EventDeferred = "deferred";

    public const string EventDiscarded = "discarded";

    public const string EventStayed = "stayed";

    public const string EventTurn = "turn";

    public const string EventTimeout = "timeout";

    public const string EventLeft = "left";

    public const string EventRoundOver = "roundOver";

    public const string EventMatchOver = "matchOver";
}
