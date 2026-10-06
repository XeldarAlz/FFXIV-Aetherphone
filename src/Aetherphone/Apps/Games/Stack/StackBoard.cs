using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Stack;

internal enum StackState : byte
{
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
    public const int PerfectsPerReward = 4;
    public const float RewardWidth = 0.026f;
    public const float MinimumWidth = 0.035f;
    public const float SliceLife = 2.2f;
    private const int RingCapacity = 24;
    private const int SliceCapacity = 12;
    private const float BaseSpeed = 0.60f;
    private const float SpeedPerLevel = 0.021f;
    private const float MaxSpeed = 1.75f;
    private const float SliceGravity = 3.6f;
    private const float SliceKick = 0.6f;
    private const float SliceDrift = 0.45f;
    private const float MissDrift = 0.35f;
    private const float SliceSpin = 2.4f;
    private const int ColorOffsets = 64;

    private readonly StackBlock[] blocks = new StackBlock[RingCapacity];
    private readonly StackSlice[] slices = new StackSlice[SliceCapacity];
    private GameRandom random;
    private ComboMeter combo = ComboMeter.Untimed();
    private int perfectsSinceReward;
    private float direction = 1f;

    public StackState State { get; private set; }

    public int Level { get; private set; }

    public int Score { get; private set; }

    public int Perfects { get; private set; }

    public int BestCombo { get; private set; }

    public int ColorOffset { get; private set; }

    public float MovingCenterX { get; private set; }

    public float MovingWidth { get; private set; }

    public float LastSliceCenterX { get; private set; }

    public int SliceCount { get; private set; }

    public bool WidenedThisDrop { get; private set; }

    public ComboMeter Combo => combo;

    public int Height => Level - 1;

    public StackSlice Slice(int index) => slices[index];

    public StackBlock Block(int level) => blocks[level % RingCapacity];

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        State = StackState.Playing;
        Score = 0;
        Perfects = 0;
        BestCombo = 0;
        SliceCount = 0;
        WidenedThisDrop = false;
        perfectsSinceReward = 0;
        direction = 1f;
        combo.Reset();
        ColorOffset = random.Next(ColorOffsets);
        MovingWidth = StartWidth;
        MovingCenterX = 0.5f;
        ref var seed = ref blocks[0];
        seed.CenterX = 0.5f;
        seed.Width = StartWidth;
        Level = 1;
        SpawnMoving();
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        StepSlices(deltaSeconds);
        if (State != StackState.Playing)
        {
            return;
        }

        combo.Update(deltaSeconds);
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

    public StackDrop DropAt(float centerX)
    {
        if (State != StackState.Playing)
        {
            return StackDrop.None;
        }

        var half = MovingWidth * 0.5f;
        MovingCenterX = Math.Clamp(centerX, half, 1f - half);
        return Drop();
    }

    public StackDrop Drop()
    {
        WidenedThisDrop = false;
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
            AddSlice(MovingCenterX, Level, MovingWidth, MovingCenterX < below.CenterX ? -MissDrift : MissDrift, Level);
            State = StackState.Over;
            combo.Reset();
            return StackDrop.Missed;
        }

        var offset = MathF.Abs(MovingCenterX - below.CenterX);
        if (offset <= PerfectTolerance)
        {
            combo.Hit();
            Perfects++;
            perfectsSinceReward++;
            Score += 1 + combo.Count;
            BestCombo = Math.Max(BestCombo, combo.Count);
            var width = below.Width;
            if (perfectsSinceReward >= PerfectsPerReward)
            {
                perfectsSinceReward = 0;
                var widened = MathF.Min(StartWidth, width + RewardWidth);
                WidenedThisDrop = widened > width;
                width = widened;
            }

            Place(below.CenterX, width);
            return StackDrop.Perfect;
        }

        combo.Reset();
        perfectsSinceReward = 0;
        Score += 1;
        var sliceWidth = MovingWidth - overlap;
        var sliceCenter = movingLeft < belowLeft
            ? movingLeft + sliceWidth * 0.5f
            : movingRight - sliceWidth * 0.5f;
        var sliceDrift = movingLeft < belowLeft ? -SliceDrift : SliceDrift;
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
        slice.VelocityX = drift * random.Range(0.85f, 1.15f);
        slice.VelocityY = SliceKick * random.Range(0.9f, 1.1f);
        slice.Rotation = 0f;
        slice.Spin = drift * SliceSpin * random.Range(0.8f, 1.3f);
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
