namespace Aetherphone.Apps.Games.Stack;

internal enum StackState : byte
{
    Ready,
    Playing,
    Over,
}

internal enum StackDrop : byte
{
    None,
    Placed,
    Perfect,
    Missed,
}

internal struct StackBlock
{
    public float CenterX;
    public float Width;
}

internal struct StackSlice
{
    public float CenterX;
    public float Level;
    public float Width;
    public float VelocityX;
    public float VelocityY;
    public float Rotation;
    public float Spin;
    public float Life;
    public int ColorLevel;
}

internal sealed class StackBoard
{
    public const int VisibleLevels = 15;
    public const float StartWidth = 0.58f;
    public const float PerfectTolerance = 0.011f;
    private const int RingCapacity = 24;
    private const int SliceCapacity = 12;
    private const float BaseSpeed = 0.60f;
    private const float SpeedPerLevel = 0.021f;
    private const float MaxSpeed = 1.75f;
    private const float SliceGravity = 3.6f;
    private const float SliceLife = 2.2f;
    private const float RewardWidth = 0.026f;
    private const float MinimumWidth = 0.035f;
    private const int PerfectsPerReward = 4;
    private readonly StackBlock[] blocks = new StackBlock[RingCapacity];
    private readonly StackSlice[] slices = new StackSlice[SliceCapacity];
    private int perfectsSinceReward;
    private float direction = 1f;
    public StackState State { get; private set; } = StackState.Ready;
    public int Level { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public float MovingCenterX { get; private set; }
    public float MovingWidth { get; private set; }
    public float LastSliceCenterX { get; private set; }
    public int SliceCount { get; private set; }
    public StackSlice Slice(int index) => slices[index];
    public StackBlock Block(int level) => blocks[level % RingCapacity];

    public void Reset()
    {
        State = StackState.Ready;
        Level = 0;
        Score = 0;
        Combo = 0;
        SliceCount = 0;
        perfectsSinceReward = 0;
        direction = 1f;
        MovingWidth = StartWidth;
        MovingCenterX = 0.5f;
        ref var seed = ref blocks[0];
        seed.CenterX = 0.5f;
        seed.Width = StartWidth;
        Level = 1;
        SpawnMoving();
    }

    public void Begin()
    {
        if (State != StackState.Ready)
        {
            return;
        }

        State = StackState.Playing;
    }

    public void Step(float deltaSeconds)
    {
        StepSlices(deltaSeconds);
        if (State != StackState.Playing)
        {
            return;
        }

        var half = MovingWidth * 0.5f;
        var speed = MathF.Min(MaxSpeed, BaseSpeed + SpeedPerLevel * Level);
        MovingCenterX += direction * speed * deltaSeconds;
        if (MovingCenterX > 1f - half)
        {
            MovingCenterX = 1f - half;
            direction = -1f;
        }
        else if (MovingCenterX < half)
        {
            MovingCenterX = half;
            direction = 1f;
        }
    }

    public StackDrop Drop()
    {
        if (State != StackState.Playing)
        {
            return StackDrop.None;
        }

        ref readonly var below = ref blocks[(Level - 1) % RingCapacity];
        var belowLeft = below.CenterX - below.Width * 0.5f;
        var belowRight = below.CenterX + below.Width * 0.5f;
        var movingLeft = MovingCenterX - MovingWidth * 0.5f;
        var movingRight = MovingCenterX + MovingWidth * 0.5f;
        var overlapLeft = MathF.Max(belowLeft, movingLeft);
        var overlapRight = MathF.Min(belowRight, movingRight);
        var overlap = overlapRight - overlapLeft;
        if (overlap <= MinimumWidth)
        {
            AddSlice(MovingCenterX, Level, MovingWidth, MovingCenterX < below.CenterX ? -0.35f : 0.35f, Level);
            State = StackState.Over;
            Combo = 0;
            return StackDrop.Missed;
        }

        var offset = MathF.Abs(MovingCenterX - below.CenterX);
        var perfect = offset <= PerfectTolerance;
        if (perfect)
        {
            Combo++;
            perfectsSinceReward++;
            Score += 1 + Combo;
            var width = below.Width;
            if (perfectsSinceReward >= PerfectsPerReward)
            {
                perfectsSinceReward = 0;
                width = MathF.Min(StartWidth, width + RewardWidth);
            }

            Place(below.CenterX, width);
            return StackDrop.Perfect;
        }

        Combo = 0;
        perfectsSinceReward = 0;
        Score += 1;
        var sliceWidth = MovingWidth - overlap;
        var sliceCenter = movingLeft < belowLeft
            ? movingLeft + sliceWidth * 0.5f
            : movingRight - sliceWidth * 0.5f;
        var sliceDrift = movingLeft < belowLeft ? -0.45f : 0.45f;
        AddSlice(sliceCenter, Level, sliceWidth, sliceDrift, Level);
        LastSliceCenterX = sliceCenter;
        Place(overlapLeft + overlap * 0.5f, overlap);
        return StackDrop.Placed;
    }

    private void Place(float centerX, float width)
    {
        ref var placed = ref blocks[Level % RingCapacity];
        placed.CenterX = centerX;
        placed.Width = width;
        Level++;
        MovingWidth = width;
        SpawnMoving();
    }

    private void SpawnMoving()
    {
        var half = MovingWidth * 0.5f;
        if ((Level & 1) == 0)
        {
            MovingCenterX = half;
            direction = 1f;
            return;
        }

        MovingCenterX = 1f - half;
        direction = -1f;
    }

    private void AddSlice(float centerX, int level, float width, float drift, int colorLevel)
    {
        if (SliceCount >= SliceCapacity)
        {
            return;
        }

        ref var slice = ref slices[SliceCount];
        slice.CenterX = centerX;
        slice.Level = level;
        slice.Width = width;
        slice.VelocityX = drift;
        slice.VelocityY = 0.6f;
        slice.Rotation = 0f;
        slice.Spin = drift * 2.4f;
        slice.Life = SliceLife;
        slice.ColorLevel = colorLevel;
        SliceCount++;
    }

    private void StepSlices(float deltaSeconds)
    {
        for (var index = SliceCount - 1; index >= 0; index--)
        {
            ref var slice = ref slices[index];
            slice.Life -= deltaSeconds;
            if (slice.Life <= 0f)
            {
                slices[index] = slices[SliceCount - 1];
                SliceCount--;
                continue;
            }

            slice.VelocityY -= SliceGravity * deltaSeconds;
            slice.Level += slice.VelocityY * deltaSeconds;
            slice.CenterX += slice.VelocityX * deltaSeconds;
            slice.Rotation += slice.Spin * deltaSeconds;
        }
    }
}
