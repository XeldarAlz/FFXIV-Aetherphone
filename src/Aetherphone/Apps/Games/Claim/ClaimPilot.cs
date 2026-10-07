using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Claim;

internal enum ClaimPilotPhase : byte
{
    Roam,
    Cut,
    Across,
    Return,
}

internal struct ClaimPilot
{
    private const float MinRoamSeconds = 0.5f;
    private const float MaxRoamSeconds = 2f;
    private const float StallSeconds = 0.12f;
    private const float StartSeconds = 0.4f;
    private const float SlowChance = 0.3f;
    private const int MinCut = 14;
    private const int MaxCut = 46;
    private const int MinAcross = 12;
    private const int MaxAcross = 50;

    private GameRandom random;
    private ClaimPilotPhase phase;
    private ClaimMove roam;
    private ClaimMove inward;
    private ClaimMove side;
    private ClaimMove heading;
    private ClaimDraw draw;
    private float timer;
    private float stalled;
    private float phaseSeconds;
    private int cutLength;
    private int acrossLength;
    private int lastTrail;

    public static ClaimPilot Create(ulong seed)
    {
        var pilot = default(ClaimPilot);
        pilot.random = GameRandom.FromSeed(seed);
        pilot.Restart();
        return pilot;
    }

    public readonly ClaimPilotPhase Phase => phase;

    public void Restart()
    {
        phase = ClaimPilotPhase.Roam;
        roam = ClaimMove.None;
        heading = ClaimMove.None;
        timer = random.Range(MinRoamSeconds, MaxRoamSeconds);
        stalled = 0f;
        phaseSeconds = 0f;
        lastTrail = 0;
    }

    public void Decide(ClaimBoard board, float deltaSeconds, out ClaimMove move, out ClaimDraw drawMode)
    {
        move = ClaimMove.None;
        drawMode = ClaimDraw.None;
        if (board.State != ClaimState.Playing)
        {
            Restart();
            return;
        }

        phaseSeconds += deltaSeconds;
        if (phase != ClaimPilotPhase.Roam && !board.Drawing && (lastTrail > 0 || phaseSeconds > StartSeconds))
        {
            Restart();
        }

        TrackStall(board, deltaSeconds);
        switch (phase)
        {
            case ClaimPilotPhase.Roam:
                Roam(board, deltaSeconds, out move, out drawMode);
                return;
            case ClaimPilotPhase.Cut:
                if (board.Trail.Length >= cutLength || stalled > StallSeconds)
                {
                    phase = ClaimPilotPhase.Across;
                    heading = side;
                    stalled = 0f;
                }

                break;
            case ClaimPilotPhase.Across:
                if (board.Trail.Length >= cutLength + acrossLength || stalled > StallSeconds)
                {
                    phase = ClaimPilotPhase.Return;
                    heading = ClaimBoard.Opposite(inward);
                    stalled = 0f;
                }

                break;
            default:
                if (stalled > StallSeconds)
                {
                    heading = random.Chance(0.5f) ? ClaimBoard.TurnLeft(heading) : ClaimBoard.TurnRight(heading);
                    stalled = 0f;
                }

                break;
        }

        move = heading;
        drawMode = draw;
    }

    private void TrackStall(ClaimBoard board, float deltaSeconds)
    {
        var length = board.Trail.Length;
        if (length != lastTrail || !board.Drawing)
        {
            stalled = 0f;
            lastTrail = length;
            return;
        }

        stalled += deltaSeconds;
    }

    private void Roam(ClaimBoard board, float deltaSeconds, out ClaimMove move, out ClaimDraw drawMode)
    {
        drawMode = ClaimDraw.None;
        timer -= deltaSeconds;
        if (timer <= 0f)
        {
            var cut = InwardDirection(board);
            if (cut != ClaimMove.None)
            {
                inward = cut;
                side = random.Chance(0.5f) ? ClaimBoard.TurnLeft(cut) : ClaimBoard.TurnRight(cut);
                heading = cut;
                cutLength = random.Next(MinCut, MaxCut);
                acrossLength = random.Next(MinAcross, MaxAcross);
                draw = random.Chance(SlowChance) ? ClaimDraw.Slow : ClaimDraw.Fast;
                phase = ClaimPilotPhase.Cut;
                phaseSeconds = 0f;
                lastTrail = 0;
                stalled = 0f;
                move = cut;
                drawMode = draw;
                return;
            }

            timer = random.Range(MinRoamSeconds, MaxRoamSeconds);
        }

        if (roam == ClaimMove.None || !board.IsWalkable(ClaimBoard.Neighbour(board.PlayerCell, roam)))
        {
            roam = WalkableDirection(board, roam);
        }

        move = roam;
    }

    private ClaimMove InwardDirection(ClaimBoard board)
    {
        var start = random.Next(4);
        for (var offset = 0; offset < 4; offset++)
        {
            var direction = (ClaimMove)(1 + (start + offset) % 4);
            var neighbour = ClaimBoard.Neighbour(board.PlayerCell, direction);
            if (neighbour >= 0 && board.Cells[neighbour] == ClaimCell.Open)
            {
                return direction;
            }
        }

        return ClaimMove.None;
    }

    private ClaimMove WalkableDirection(ClaimBoard board, ClaimMove current)
    {
        var back = ClaimBoard.Opposite(current);
        var start = random.Next(4);
        for (var offset = 0; offset < 4; offset++)
        {
            var direction = (ClaimMove)(1 + (start + offset) % 4);
            if (direction != back && board.IsWalkable(ClaimBoard.Neighbour(board.PlayerCell, direction)))
            {
                return direction;
            }
        }

        return back;
    }
}
