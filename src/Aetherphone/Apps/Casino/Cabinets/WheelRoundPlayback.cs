using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum WheelStage
{
    Waiting,
    Betting,
    Locking,
    Spinning,
    Settling,
}

internal sealed class WheelRoundPlayback
{
    public const float SettleHoldSeconds = 1.2f;

    private const float LockSpinRadiansPerSecond = 6.5f;

    private string roundId = string.Empty;
    private WheelStage stage = WheelStage.Waiting;
    private float angle;
    private float restAngle;
    private float spinFromAngle;
    private float sweep;
    private float spinElapsedSeconds;
    private float lockElapsedSeconds;
    private float stageSeconds;
    private int segment = -1;
    private int lastSegment = -1;
    private int peg;
    private bool spinBegun;
    private bool spunLive;

    public WheelStage Stage => stage;

    public string RoundId => roundId;

    public float Angle => angle;

    public int Segment => segment;

    public int LastSegment => lastSegment;

    public float StageSeconds => stageSeconds;

    public bool Landed => spinBegun && spinElapsedSeconds >= WheelChoreography.SpinSeconds;

    public bool SpunLive => spunLive;

    public float SpinProgress => WheelChoreography.Progress(spinElapsedSeconds);

    public void Reset()
    {
        roundId = string.Empty;
        stage = WheelStage.Waiting;
        angle = 0f;
        restAngle = 0f;
        spinFromAngle = 0f;
        sweep = 0f;
        spinElapsedSeconds = 0f;
        lockElapsedSeconds = 0f;
        stageSeconds = 0f;
        segment = -1;
        lastSegment = -1;
        spinBegun = false;
        spunLive = false;
    }

    public void Update(CasinoRoomSnapshotDto? snapshot, CasinoWheelRoomStateDto? board,
        long remainingMilliseconds, float deltaSeconds)
    {
        stageSeconds += deltaSeconds;
        if (snapshot is null)
        {
            return;
        }

        var nextRoundId = RoundKeyOf(snapshot);
        if (!string.Equals(nextRoundId, roundId, StringComparison.Ordinal))
        {
            OpenRound(nextRoundId);
        }

        var drawn = DrawnSegment(board);
        if (drawn >= 0 && !spinBegun)
        {
            BeginSpin(drawn, snapshot.Phase, remainingMilliseconds);
        }

        if (spinBegun)
        {
            AdvanceSpin(deltaSeconds);
            return;
        }

        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            angle = restAngle;
            EnterStage(WheelStage.Betting);
            return;
        }

        lockElapsedSeconds += deltaSeconds;
        angle = restAngle + lockElapsedSeconds * LockSpinRadiansPerSecond;
        EnterStage(WheelStage.Locking);
    }

    internal static string RoundKeyOf(CasinoRoomSnapshotDto snapshot)
    {
        return string.Concat(snapshot.RoomId, "#",
            snapshot.RoundIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    internal static int DrawnSegment(CasinoWheelRoomStateDto? board)
    {
        if (board is null)
        {
            return -1;
        }

        return WheelRules.IsSegment(board.Segment) ? board.Segment : -1;
    }

    internal static float InitialSpinElapsed(int phase, long remainingMilliseconds)
    {
        if (phase != CasinoRoomPhases.Locked)
        {
            return WheelChoreography.SpinSeconds;
        }

        var remainingSeconds = remainingMilliseconds / 1000f;
        var elapsed = WheelChoreography.SpinSeconds - (remainingSeconds - SettleHoldSeconds);
        return elapsed > WheelChoreography.SpinSeconds ? WheelChoreography.SpinSeconds : elapsed;
    }

    private void OpenRound(string nextRoundId)
    {
        roundId = nextRoundId;
        restAngle = WheelChoreography.Normalize(angle);
        angle = restAngle;
        spinFromAngle = restAngle;
        sweep = 0f;
        spinElapsedSeconds = 0f;
        lockElapsedSeconds = 0f;
        segment = -1;
        spinBegun = false;
        spunLive = false;
    }

    private void BeginSpin(int drawn, int phase, long remainingMilliseconds)
    {
        segment = drawn;
        lastSegment = drawn;
        spinFromAngle = angle;
        sweep = WheelChoreography.SweepFor(spinFromAngle, drawn);
        spinElapsedSeconds = InitialSpinElapsed(phase, remainingMilliseconds);
        spinBegun = true;
        spunLive = spinElapsedSeconds < WheelChoreography.SpinSeconds;
        AdvanceSpin(0f);
    }

    private void AdvanceSpin(float deltaSeconds)
    {
        spinElapsedSeconds += deltaSeconds;
        if (spinElapsedSeconds > WheelChoreography.SpinSeconds)
        {
            spinElapsedSeconds = WheelChoreography.SpinSeconds;
        }

        angle = WheelChoreography.AngleAt(spinFromAngle, sweep, spinElapsedSeconds);
        var nextPeg = WheelChoreography.PegOf(angle, WheelRules.SegmentCount);
        if (nextPeg != peg && deltaSeconds > 0f)
        {
            UiFeedback.Play(UiSound.GameTick);
        }

        peg = nextPeg;
        if (spinElapsedSeconds < WheelChoreography.SpinSeconds)
        {
            EnterStage(WheelStage.Spinning);
            return;
        }

        restAngle = WheelChoreography.Normalize(angle);
        EnterStage(WheelStage.Settling);
    }

    private void EnterStage(WheelStage next)
    {
        if (stage == next)
        {
            return;
        }

        stage = next;
        stageSeconds = 0f;
    }
}
