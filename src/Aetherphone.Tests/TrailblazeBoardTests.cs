using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Trailblaze;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TrailblazeBoardTests
{
    private const float Frame = 1f / 60f;
    private const int WarmUpChunk = 0;
    private const int FirstBarrierChunk = 1;
    private const int FirstGapChunk = 2;
    private const int CartChunk = 3;
    private const int PowerChunk = 4;
    private const int MagnetChunk = 23;
    private const int MagnetSideRow = 6;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Drive(1234, 1800, out var firstTrace);
        var second = Drive(1234, 1800, out var secondTrace);
        Drive(99, 1800, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Distance, second.Distance);
        Assert.Equal(first.Gil, second.Gil);
        Assert.Equal(first.State, second.State);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void ChunkChainingIsSeeded()
    {
        var first = Sequence(42, 48);
        var second = Sequence(42, 48);
        var other = Sequence(43, 48);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(WarmUpChunk, first[0]);
        for (var ordinal = 1; ordinal < first.Length; ordinal++)
        {
            Assert.NotEqual(first[ordinal - 1], first[ordinal]);
            Assert.True(TrailblazeChunks.Tier(first[ordinal]) <= TrailblazeBoard.TierCap(ordinal));
        }

        var board = new TrailblazeBoard();
        board.Reset(GameRandom.FromSeed(42));
        RunAutopilot(board, 2400);
        Assert.True(board.ChunksLoaded > 4);
        for (var ordinal = 0; ordinal < Math.Min(board.ChunksLoaded, first.Length); ordinal++)
        {
            Assert.Equal(first[ordinal], board.ChunkAt(ordinal));
        }
    }

    [Fact]
    public void TheHardestTierArrivesOnlyAfterTheWarmUp()
    {
        var sequence = Sequence(7, 64);
        var sawHardest = false;
        for (var ordinal = 0; ordinal < sequence.Length; ordinal++)
        {
            var tier = TrailblazeChunks.Tier(sequence[ordinal]);
            Assert.True(tier <= TrailblazeBoard.TierCap(ordinal));
            sawHardest |= tier == TrailblazeChunks.TierCount - 1;
        }

        Assert.True(sawHardest);
        Assert.Equal(0, TrailblazeBoard.TierCap(0));
        Assert.Equal(TrailblazeChunks.TierCount - 1, TrailblazeBoard.TierCap(40));
    }

    [Fact]
    public void TheLibraryHoldsTwentyFourWellFormedChunksAcrossThreeTiers()
    {
        Assert.Equal(24, TrailblazeChunks.Count);
        var perTier = new int[TrailblazeChunks.TierCount];
        for (var chunk = 0; chunk < TrailblazeChunks.Count; chunk++)
        {
            Assert.True(TrailblazeChunks.IsWellFormed(chunk), $"chunk {chunk}");
            perTier[TrailblazeChunks.Tier(chunk)]++;
        }

        for (var tier = 0; tier < perTier.Length; tier++)
        {
            Assert.Equal(8, perTier[tier]);
        }

        for (var row = 0; row < TrailblazeChunks.Rows; row++)
        {
            for (var lane = 0; lane < TrailblazeChunks.Lanes; lane++)
            {
                Assert.Equal(TrailblazeCell.Open, TrailblazeChunks.Cell(WarmUpChunk, row, lane));
            }
        }
    }

    [Fact]
    public void EveryChunkHasAPassableLaneAtEveryRow()
    {
        for (var chunk = 0; chunk < TrailblazeChunks.Count; chunk++)
        {
            for (var row = 0; row < TrailblazeChunks.Rows; row++)
            {
                var passable = false;
                for (var lane = 0; lane < TrailblazeChunks.Lanes; lane++)
                {
                    passable |= TrailblazeChunks.Cell(chunk, row, lane) != TrailblazeCell.Cart;
                }

                Assert.True(passable, $"chunk {chunk} row {row} is a wall of carts");
            }
        }
    }

    [Fact]
    public void EveryChunkCanBeCrossedFromAnyLaneWithoutDeadEnds()
    {
        const int lanes = TrailblazeChunks.Lanes;
        const int rows = TrailblazeChunks.Rows;
        for (var chunk = 0; chunk < TrailblazeChunks.Count; chunk++)
        {
            for (var lane = 0; lane < lanes; lane++)
            {
                Assert.True(Open(chunk, 0, lane), $"chunk {chunk} starts with a cart");
                Assert.True(Open(chunk, rows - 1, lane), $"chunk {chunk} ends with a cart");
            }

            var survives = new bool[rows, lanes];
            for (var lane = 0; lane < lanes; lane++)
            {
                survives[rows - 1, lane] = true;
            }

            for (var row = rows - 2; row >= 0; row--)
            {
                for (var lane = 0; lane < lanes; lane++)
                {
                    if (!Open(chunk, row, lane))
                    {
                        continue;
                    }

                    for (var next = Math.Max(0, lane - 1); next <= Math.Min(lanes - 1, lane + 1); next++)
                    {
                        survives[row, lane] |= survives[row + 1, next] && Switchable(chunk, row, lane, next);
                    }
                }
            }

            var reached = new bool[rows, lanes];
            for (var lane = 0; lane < lanes; lane++)
            {
                reached[0, lane] = true;
            }

            for (var row = 0; row < rows; row++)
            {
                for (var lane = 0; lane < lanes; lane++)
                {
                    if (!reached[row, lane])
                    {
                        continue;
                    }

                    Assert.True(survives[row, lane], $"chunk {chunk} traps a runner in lane {lane} at row {row}");
                    if (row + 1 >= rows)
                    {
                        continue;
                    }

                    for (var next = Math.Max(0, lane - 1); next <= Math.Min(lanes - 1, lane + 1); next++)
                    {
                        reached[row + 1, next] |= Open(chunk, row + 1, next) && Switchable(chunk, row, lane, next);
                    }
                }
            }
        }
    }

    private static bool Open(int chunk, int row, int lane) => TrailblazeChunks.Cell(chunk, row, lane) != TrailblazeCell.Cart;

    private static bool Switchable(int chunk, int row, int lane, int next) =>
        lane == next ? Open(chunk, row + 1, lane) : Open(chunk, row, next) || Open(chunk, row + 1, lane);

    [Fact]
    public void LootNeverSitsInsideACartOrFloatsLowOverAPit()
    {
        for (var chunk = 0; chunk < TrailblazeChunks.Count; chunk++)
        {
            for (var row = 0; row < TrailblazeChunks.Rows; row++)
            {
                for (var lane = 0; lane < TrailblazeChunks.Lanes; lane++)
                {
                    var cell = TrailblazeChunks.Cell(chunk, row, lane);
                    var loot = TrailblazeChunks.Loot(chunk, row, lane);
                    if (cell == TrailblazeCell.Cart)
                    {
                        Assert.Equal(TrailblazeLoot.None, loot);
                    }

                    if (cell == TrailblazeCell.Gap)
                    {
                        Assert.NotEqual(TrailblazeLoot.Coins, loot);
                    }

                    if (loot == TrailblazeLoot.Power)
                    {
                        Assert.Equal(TrailblazeCell.Open, cell);
                    }

                    if (loot != TrailblazeLoot.Arc || (row > 0 && TrailblazeChunks.Loot(chunk, row - 1, lane) == TrailblazeLoot.Arc))
                    {
                        continue;
                    }

                    Assert.True(row + 1 < TrailblazeChunks.Rows &&
                                TrailblazeChunks.Loot(chunk, row + 1, lane) == TrailblazeLoot.Arc,
                        $"chunk {chunk} lane {lane} has a one-cell coin arc at row {row}");
                }
            }
        }
    }

    [Fact]
    public void CollisionDependsOnLaneAndHeight()
    {
        var standing = TrailblazeBoard.BodyHeight;
        var sliding = TrailblazeBoard.SlideBodyHeight;
        var jumpPeak = TrailblazeBoard.JumpVelocity * TrailblazeBoard.JumpVelocity / (2f * TrailblazeBoard.Gravity);

        Assert.True(TrailblazeBoard.Collides(TrailblazeCell.Cart, 1, 1f, 0f, standing));
        Assert.True(TrailblazeBoard.Collides(TrailblazeCell.Cart, 1, 1f, jumpPeak, jumpPeak + standing));
        Assert.False(TrailblazeBoard.Collides(TrailblazeCell.Cart, 0, 1f, 0f, standing));
        Assert.True(TrailblazeBoard.Collides(TrailblazeCell.Cart, 0, 0.5f, 0f, standing));
        Assert.False(TrailblazeBoard.Collides(TrailblazeCell.Cart, 1, 1f, TrailblazeBoard.FlyHeight,
            TrailblazeBoard.FlyHeight + standing));

        Assert.True(TrailblazeBoard.Collides(TrailblazeCell.Barrier, 2, 2f, 0f, standing));
        Assert.False(TrailblazeBoard.Collides(TrailblazeCell.Barrier, 2, 2f, 0f, sliding));
        Assert.True(TrailblazeBoard.Collides(TrailblazeCell.Barrier, 2, 2f, jumpPeak, jumpPeak + standing));
        Assert.False(TrailblazeBoard.Collides(TrailblazeCell.Barrier, 1, 2f, 0f, standing));
        Assert.False(TrailblazeBoard.Collides(TrailblazeCell.Barrier, 2, 2f, TrailblazeBoard.FlyHeight,
            TrailblazeBoard.FlyHeight + standing));

        Assert.False(TrailblazeBoard.Collides(TrailblazeCell.Gap, 1, 1f, 0f, standing));
        Assert.True(TrailblazeBoard.Drops(1, 1f, 0f, false, 11.5f, 10f, TrailblazeBoard.GapLength));
        Assert.False(TrailblazeBoard.Drops(1, 1f, 0.6f, true, 11.5f, 10f, TrailblazeBoard.GapLength));
        Assert.False(TrailblazeBoard.Drops(0, 1f, 0f, false, 11.5f, 10f, TrailblazeBoard.GapLength));
        Assert.False(TrailblazeBoard.Drops(1, 1f, 0f, false, 9f, 10f, TrailblazeBoard.GapLength));
    }

    [Fact]
    public void RunningIntoABarrierCrashesButSlidingUnderItScoresAStunt()
    {
        var crash = Opening(FirstBarrierChunk);
        RunUntilOver(crash, 900);
        Assert.Equal(TrailblazeDeath.Crash, crash.Death);

        var duck = Opening(FirstBarrierChunk);
        var stunts = RunReacting(duck, TrailblazeCell.Barrier, 300);
        Assert.Equal(TrailblazeState.Running, duck.State);
        Assert.Contains(TrailblazeStunt.Duck, stunts);
        Assert.True(duck.Stunts >= 1);
    }

    [Fact]
    public void ARunnerWhoNeverJumpsFallsIntoTheFirstPit()
    {
        var fall = Opening(FirstGapChunk);
        RunUntilOver(fall, 900);
        Assert.Equal(TrailblazeDeath.Fall, fall.Death);

        var leap = Opening(FirstGapChunk);
        var stunts = RunReacting(leap, TrailblazeCell.Gap, 360);
        Assert.Equal(TrailblazeState.Running, leap.State);
        Assert.Contains(TrailblazeStunt.Leap, stunts);
    }

    [Fact]
    public void SteeringIntoACartAlongsideBumpsInsteadOfMoving()
    {
        var board = Opening(CartChunk);
        board.Move(TrailblazeMove.Left);
        board.Step(Frame);
        Assert.Equal(0, board.TargetLane);
        board.Move(TrailblazeMove.Right);
        board.Step(Frame);
        Assert.Equal(1, board.TargetLane);

        var cartZ = 0f;
        for (var index = 0; index < board.HazardCount; index++)
        {
            ref readonly var hazard = ref board.HazardAt(index);
            if (hazard.Kind == TrailblazeCell.Cart && hazard.Lane == 0)
            {
                cartZ = hazard.Z + 1f;
                break;
            }
        }

        while (board.Distance < cartZ && board.State == TrailblazeState.Running)
        {
            board.Step(Frame);
        }

        Assert.True(board.LaneBlocked(0));
        board.Move(TrailblazeMove.Left);
        board.Step(Frame);
        Assert.Equal(-1, board.BumpedThisStep);
        Assert.Equal(1, board.TargetLane);
        Assert.Equal(TrailblazeState.Running, board.State);
    }

    [Fact]
    public void ACartAlongsideStaysDrawnForAsLongAsItBlocksTheLane()
    {
        var board = Opening(CartChunk);
        var cartIndex = -1;
        for (var index = 0; index < board.HazardCount; index++)
        {
            ref readonly var hazard = ref board.HazardAt(index);
            if (hazard.Kind == TrailblazeCell.Cart && hazard.Lane == 2)
            {
                cartIndex = index;
                break;
            }
        }

        Assert.True(cartIndex >= 0);
        var cart = board.HazardAt(cartIndex);
        var alongside = cart.Z + TrailblazeView.PlayerDepth + 0.5f;
        Assert.True(alongside < cart.Z + cart.Length - TrailblazeBoard.BodyHalfDepth);
        while (board.Distance < alongside && board.State == TrailblazeState.Running)
        {
            board.Step(Frame);
        }

        var nearZ = TrailblazeView.NearWorldZAt(board.Distance);
        Assert.Equal(TrailblazeState.Running, board.State);
        Assert.True(board.LaneBlocked(2));
        Assert.True(cart.Z <= nearZ);
        Assert.True(TrailblazeRenderer.IsTrailingCart(cart, nearZ));
    }

    [Fact]
    public void OnlyACartStraddlingTheNearPlaneCountsAsTrailing()
    {
        var ahead = new TrailblazeHazard { Z = 20f, Length = 12f, Lane = 0, Kind = TrailblazeCell.Cart };
        var gone = new TrailblazeHazard { Z = 2f, Length = 6f, Lane = 0, Kind = TrailblazeCell.Cart };
        var barrier = new TrailblazeHazard { Z = 5f, Length = 0.5f, Lane = 0, Kind = TrailblazeCell.Barrier };

        Assert.False(TrailblazeRenderer.IsTrailingCart(ahead, 10f));
        Assert.False(TrailblazeRenderer.IsTrailingCart(gone, 10f));
        Assert.False(TrailblazeRenderer.IsTrailingCart(barrier, 5.2f));
    }

    [Fact]
    public void CoinsInYourLaneAreCollectedAndBuildAChain()
    {
        var board = Opening(WarmUpChunk);
        RunFrames(board, 240);

        Assert.True(board.Gil >= 6);
        Assert.True(board.BestChain >= 3);
        Assert.True(board.Score > board.Metres);
    }

    [Fact]
    public void ThePowerUpInYourLaneIsCollected()
    {
        var board = Opening(PowerChunk);
        var power = TrailblazePower.None;
        for (var frame = 0; frame < 360 && power == TrailblazePower.None; frame++)
        {
            board.Step(Frame);
            power = board.PowerThisStep;
        }

        Assert.NotEqual(TrailblazePower.None, power);
        Assert.True(board.PowerLeft(power) > 0f);
    }

    [Fact]
    public void WingsLiftTheRunnerOverEverythingThenLandWithGrace()
    {
        var board = PowerOpening(TrailblazePower.Wings);
        WaitForPower(board, TrailblazePower.Wings);
        Assert.True(board.Flying);
        Assert.True(board.Invulnerable);
        RunFrames(board, 60);
        Assert.Equal(TrailblazeBoard.FlyHeight, board.Height, 2);
        Assert.False(TrailblazeBoard.Collides(TrailblazeCell.Cart, 1, board.LaneX, board.Height, board.BodyTop));

        var ended = false;
        for (var frame = 0; frame < 600 && !ended; frame++)
        {
            board.Step(Frame);
            ended = board.PowerEndedThisStep == TrailblazePower.Wings;
        }

        Assert.True(ended);
        Assert.True(board.GraceLeft > 0f);
        Assert.True(board.Invulnerable);
    }

    [Fact]
    public void DoubleGilCountsEveryCoinTwice()
    {
        var board = PowerOpening(TrailblazePower.Double);
        WaitForPower(board, TrailblazePower.Double);
        var before = board.Gil;
        var collected = 0;
        for (var frame = 0; frame < 240; frame++)
        {
            board.Step(Frame);
            collected += board.CoinsThisStep;
        }

        Assert.True(collected > 0);
        Assert.Equal(before + collected * 2, board.Gil);
    }

    [Fact]
    public void TheMagnetPullsCoinsFromOtherLanes()
    {
        var board = PowerOpening(TrailblazePower.Magnet, MagnetChunk);
        var rowZ = TrailblazeBoard.RunwayLength + (MagnetSideRow + 0.5f) * TrailblazeBoard.RowSpacing;
        Assert.True(SideCoinsNear(board, rowZ) >= 6);

        RunAutopilot(board, rowZ + 3f);

        Assert.Equal(TrailblazeState.Running, board.State);
        Assert.Equal(1, board.TargetLane);
        Assert.True(board.PowerLeft(TrailblazePower.Magnet) > 0f);
        Assert.Equal(0, SideCoinsNear(board, rowZ));
    }

    [Fact]
    public void SpeedRampsWithDistanceTowardTheCap()
    {
        Assert.Equal(TrailblazeBoard.BaseSpeed, TrailblazeBoard.RunSpeed(0f));
        var previous = 0f;
        for (var distance = 0f; distance < 6000f; distance += 250f)
        {
            var speed = TrailblazeBoard.RunSpeed(distance);
            Assert.True(speed > previous);
            Assert.True(speed < TrailblazeBoard.MaxSpeed);
            previous = speed;
        }

        Assert.True(TrailblazeBoard.RunSpeed(6000f) > TrailblazeBoard.MaxSpeed - 1f);
    }

    [Fact]
    public void TheAutopilotClearsTheFirstKilometreOnEverySeedTried()
    {
        for (var seed = 1UL; seed <= 16UL; seed++)
        {
            var board = new TrailblazeBoard();
            board.Reset(GameRandom.FromSeed(seed));
            RunAutopilot(board, 1000);
            Assert.True(board.State == TrailblazeState.Running,
                $"seed {seed} died ({board.Death}) at {board.Metres} m in chunk {CurrentChunk(board)}");
        }
    }

    [Fact]
    public void JumpingWhileAirborneIsBufferedForTheLanding()
    {
        var board = Opening(WarmUpChunk);
        board.Move(TrailblazeMove.Jump);
        board.Step(Frame);
        Assert.True(board.Airborne);
        Assert.Equal(1, board.Jumps);

        var landed = false;
        for (var frame = 0; frame < 120 && !landed; frame++)
        {
            if (board.VerticalVelocity < 0f && board.Height < 0.3f)
            {
                board.Move(TrailblazeMove.Jump);
            }

            board.Step(Frame);
            landed = board.LandedThisStep;
        }

        Assert.True(landed);
        Assert.Equal(2, board.Jumps);
        Assert.True(board.Airborne);
    }

    [Fact]
    public void SlidingInTheAirSlamsDownAndSlidesOnLanding()
    {
        var board = Opening(WarmUpChunk);
        board.Move(TrailblazeMove.Jump);
        RunFrames(board, 10);
        board.Move(TrailblazeMove.Slide);
        board.Step(Frame);
        Assert.True(board.SlammedThisStep);
        Assert.True(board.VerticalVelocity <= -TrailblazeBoard.SlamVelocity);

        var landed = false;
        for (var frame = 0; frame < 60 && !landed; frame++)
        {
            board.Step(Frame);
            landed = board.LandedThisStep;
        }

        Assert.True(landed);
        Assert.True(board.Sliding);
    }

    private static TrailblazeBoard Opening(int chunk)
    {
        var board = new TrailblazeBoard();
        board.Reset(GameRandom.FromSeed(5), chunk);
        return board;
    }

    private static int SideCoinsNear(TrailblazeBoard board, float rowZ)
    {
        var count = 0;
        for (var index = 0; index < board.CoinCount; index++)
        {
            ref readonly var coin = ref board.CoinAt(index);
            if (MathF.Abs(coin.Z - rowZ) < 2.5f && MathF.Abs(coin.LaneX - 1f) > 0.6f)
            {
                count++;
            }
        }

        return count;
    }

    private static TrailblazeBoard PowerOpening(TrailblazePower wanted, int chunk = PowerChunk)
    {
        for (var seed = 1UL; seed < 400UL; seed++)
        {
            var board = new TrailblazeBoard();
            board.Reset(GameRandom.FromSeed(seed), chunk);
            for (var index = 0; index < board.PickupCount; index++)
            {
                if (board.PickupAt(index).Kind == wanted)
                {
                    return board;
                }
            }
        }

        throw new InvalidOperationException("no seed opened with the wanted power");
    }

    private static void WaitForPower(TrailblazeBoard board, TrailblazePower wanted)
    {
        for (var frame = 0; frame < 600; frame++)
        {
            board.Step(Frame);
            if (board.PowerThisStep == wanted)
            {
                return;
            }
        }

        throw new InvalidOperationException("the power was never collected");
    }

    private static int[] Sequence(ulong seed, int length)
    {
        var random = GameRandom.FromSeed(seed);
        var sequence = new int[length];
        sequence[0] = WarmUpChunk;
        for (var ordinal = 1; ordinal < length; ordinal++)
        {
            sequence[ordinal] = TrailblazeBoard.NextChunk(ref random, ordinal, sequence[ordinal - 1]);
        }

        return sequence;
    }

    private static TrailblazeBoard Drive(ulong seed, int frames, out string trace)
    {
        var board = new TrailblazeBoard();
        board.Reset(GameRandom.FromSeed(seed));
        var builder = new StringBuilder();
        for (var frame = 0; frame < frames && board.State != TrailblazeState.Over; frame++)
        {
            TrailblazeAutopilot.Drive(board);
            board.Step(Frame);
            if (frame % 30 == 0)
            {
                builder.Append(board.Score).Append(':').Append(board.TargetLane).Append(':')
                    .Append(board.Gil).Append(':').Append(board.ChunksLoaded).Append(';');
            }
        }

        trace = builder.ToString();
        return board;
    }

    private static void RunAutopilot(TrailblazeBoard board, float metres)
    {
        for (var frame = 0; frame < 60 * 600 && board.State == TrailblazeState.Running && board.Distance < metres; frame++)
        {
            TrailblazeAutopilot.Drive(board);
            board.Step(Frame);
        }
    }

    private static List<TrailblazeStunt> RunReacting(TrailblazeBoard board, TrailblazeCell kind, int frames)
    {
        var stunts = new List<TrailblazeStunt>();
        for (var frame = 0; frame < frames && board.State == TrailblazeState.Running; frame++)
        {
            for (var index = 0; index < board.HazardCount; index++)
            {
                ref readonly var hazard = ref board.HazardAt(index);
                if (hazard.Kind != kind || hazard.Lane != board.TargetLane || hazard.Z + hazard.Length < board.Distance)
                {
                    continue;
                }

                var ahead = hazard.Z - board.Distance;
                if (kind == TrailblazeCell.Gap && ahead < 2f && !board.Airborne)
                {
                    board.Move(TrailblazeMove.Jump);
                }

                if (kind == TrailblazeCell.Barrier && ahead < 2f && !board.Sliding)
                {
                    board.Move(TrailblazeMove.Slide);
                }

                break;
            }

            board.Step(Frame);
            if (board.StuntThisStep != TrailblazeStunt.None)
            {
                stunts.Add(board.StuntThisStep);
            }
        }

        return stunts;
    }

    private static void RunUntilOver(TrailblazeBoard board, int frames)
    {
        for (var frame = 0; frame < frames && board.State != TrailblazeState.Over; frame++)
        {
            board.Step(Frame);
        }
    }

    private static void RunFrames(TrailblazeBoard board, int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame);
        }
    }

    private static int CurrentChunk(TrailblazeBoard board)
    {
        var ordinal = (int)((board.Distance - TrailblazeBoard.RunwayLength) / TrailblazeBoard.ChunkLength);
        return board.ChunkAt(Math.Max(0, ordinal));
    }
}
