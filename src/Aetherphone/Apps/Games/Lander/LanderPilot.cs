namespace Aetherphone.Apps.Games.Lander;

internal struct LanderPilot
{
    private const float Clearance = 4f;
    private const float OverPad = 0.12f;
    private const float CruiseSpeed = 3.2f;
    private const float ApproachGain = 0.5f;
    private const float DriftGain = 0.9f;
    private const float MaxLean = 0.55f;
    private const float AngleDeadZone = 0.025f;
    private const float FinalHeight = 2.4f;

    private int target;
    private int seenLevel;
    private int seenLives;

    public static LanderPilot Create() => new() { target = -1 };

    public void Decide(LanderBoard board, out int rotate, out bool thrust)
    {
        rotate = 0;
        thrust = false;
        if (board.State != LanderState.Flying)
        {
            return;
        }

        if (target < 0 || seenLevel != board.Level || seenLives != board.Lives)
        {
            target = NearestPad(board);
            seenLevel = board.Level;
            seenLives = board.Lives;
        }

        if (target < 0)
        {
            return;
        }

        var pad = board.Terrain.Pads[target];
        var position = board.Position;
        var velocity = board.Velocity;
        var offset = pad.Center - position.X;
        var feet = position.Y + LanderBoard.FootDrop;
        var tolerance = MathF.Max(0.1f, pad.Width * 0.5f - LanderBoard.FootSpread - OverPad);
        var aboveTarget = MathF.Abs(offset) < tolerance && MathF.Abs(velocity.X) < 0.6f;
        var height = pad.Y - feet;
        float wantedDrift;
        float wantedDescent;
        if (aboveTarget)
        {
            wantedDrift = Math.Clamp(offset * 1.2f, -0.3f, 0.3f);
            wantedDescent = height > 8f ? 2.2f : height > FinalHeight ? 1.2f : 0.55f;
        }
        else if (MathF.Abs(offset) < pad.Width)
        {
            wantedDrift = Math.Clamp(offset * ApproachGain, -1f, 1f);
            wantedDescent = height > 6f ? 0.6f : -0.2f;
        }
        else
        {
            wantedDrift = Math.Clamp(offset * ApproachGain, -CruiseSpeed, CruiseSpeed);
            var peak = board.Terrain.PeakBetween(position.X, pad.Center + MathF.Sign(offset) * pad.Width);
            var clearance = peak - feet;
            wantedDescent = clearance < Clearance ? -1.2f : clearance < Clearance * 2f ? 0f : 0.8f;
        }

        var lean = Math.Clamp((wantedDrift - velocity.X) * DriftGain / LanderBoard.Thrust, -MaxLean, MaxLean);
        if (aboveTarget && height < FinalHeight)
        {
            lean *= 0.3f;
        }

        var difference = lean - board.Angle;
        if (MathF.Abs(difference) > AngleDeadZone)
        {
            rotate = difference > 0f ? 1 : -1;
        }

        var falling = velocity.Y > wantedDescent;
        var steering = MathF.Abs(lean) > 0.12f && MathF.Abs(difference) < 0.1f && velocity.Y > wantedDescent - 1.5f;
        thrust = falling || steering;
    }

    private static int NearestPad(LanderBoard board)
    {
        var pads = board.Terrain.Pads;
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < pads.Length; index++)
        {
            var distance = MathF.Abs(pads[index].Center - board.Position.X);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = index;
        }

        return best;
    }
}
