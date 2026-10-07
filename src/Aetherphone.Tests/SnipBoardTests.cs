using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Snip;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SnipBoardTests
{
    private const float Frame = 1f / 60f;

    private static readonly string[] Solutions =
    {
        "0.5 cut 0",
        "0.3 cut 0",
        "0.3 cut 0",
        "1.1 cut 0",
        "1.1 cut 0",
        "0.95 cut 0",
        "0.6 cut 0",
        "1.65 cut 0",
        "1.4 cut 0",
        "1.8 cut 0",
        "0.5 cut 0; 0.5 cut 1",
        "1.55 cut 0",
        "2 cut 0",
        "1.5 cut 0",
        "1.55 cut 0",
        "1.95 cut 0",
        "1.5 cut 0",
        "0.85 cut 0",
        "0.7 cut 1; 2 cut 0",
        "0.85 cut 1; 1.35 cut 0",
        "0.55 cut 0; 2.4 cut 1",
        "0.95 cut 0; 2 cut 1",
        "0.65 cut 0; 1.7 cut 1",
        "0.65 cut 1; 2.4 cut 0",
        "0.4 cut 0; 2.0 cut 1",
        "0.5 cut 0; 2.3 cut 1",
        "0.45 cut 1; 2.1 cut 0",
        "0.75 cut 0",
        "1.7 cut 0",
        "0.45 cut 0; 0.45 cut 1",
        "0.6 cut 0; 0.6 cut 1",
        "1.9 cut 0",
        "0.45 cut 0; 2.25 cut 1",
        "1.7 cut 0",
        "1.25 cut 0",
        "1.65 cut 0",
        "1.95 cut 0",
        "1.55 cut 0",
        "0.9 cut 0",
        "1.45 cut 0",
        "1.6 cut 0",
        "1.5 cut 0",
        "1.05 cut 0; 1.25 puff 0",
        "1.45 cut 0; 1.95 puff 0",
        "0.65 puff 0; 2.35 cut 0",
        "0.55 puff 0; 2.2 cut 0",
        "0.3 cut 0; 0.75 puff 0",
        "0.95 cut 0",
        "2 cut 0",
        "1.3 cut 0",
        "0.3 cut 0; 1.2 cut 1",
        "0.6 cut 0; 2.4 cut 1",
        "0.95 cut 1; 2.75 cut 0",
        "0.3 cut 1; 1.5 cut 0",
        "0.95 cut 1; 2.7 cut 0",
        "0.95 cut 1; 2.8 cut 0",
        "1.5 cut 0; 1.9 puff 0",
        "0.75 cut 0; 1 puff 0",
        "0.3 puff 0; 1.4 cut 0",
        "1.3 cut 0; 1.65 puff 0",
        "0.65 cut 2; 1.2 cut 1; 1.9 cut 0",
        "0.7 cut 2; 1.05 cut 0; 1.85 cut 1",
        "0.55 cut 2; 1.5 cut 0; 2.2 cut 1",
        "0.5 cut 2; 1.6 cut 0; 3 cut 1",
        "0.8 cut 2; 1.3 cut 0; 1.95 cut 1",
        "0.8 cut 2; 1.25 cut 0; 2.05 cut 1",
        "0.55 cut 0; 1.2 cut 2; 2.6 cut 1",
        "0.35 cut 2; 1.05 cut 0; 1.8 cut 1",
        "0.3 cut 2; 0.3 cut 0; 1.7 cut 1",
        "0.45 cut 2; 1.45 cut 0; 1.9 cut 1",
        "0.7 cut 0; 1.7 cut 2; 2.6 cut 1",
        "0.3 cut 0; 0.6 puff 0",
        "0.4 cut 1; 1.85 cut 0",
        "0.3 cut 0; 1.8 cut 1",
        "0.55 cut 0; 1.3 cut 1",
        "1.4 cut 0; 1.85 puff 0",
        "1.85 cut 0; 2.4 puff 0",
        "0.75 cut 0; 1.05 puff 0",
        "1.4 cut 0; 1.9 puff 0",
        "0.6 cut 0; 0.85 puff 0",
        "1.5 cut 0; 1.85 puff 0",
        "0.3 cut 0; 0.85 puff 0",
        "1.85 cut 0; 2.2 puff 0; 2.7 puff 1",
        "0.3 puff 0; 1.4 puff 1; 2.35 cut 0",
        "0.4 cut 0; 1.15 cut 2; 1.75 cut 1",
        "0.65 cut 0; 2 cut 2; 3.35 cut 1",
        "0.8 cut 0; 1.35 cut 2; 2.6 cut 1",
        "0.8 cut 0; 1.35 puff 0",
        "1.9 cut 0; 2.25 puff 0; 2.6 puff 1",
        "1.1 cut 0; 1.45 puff 0; 1.85 puff 1",
        "0.5 cut 0; 0.95 puff 0",
        "1.35 cut 0; 1.85 puff 0",
        "0.3 cut 0; 1.0 puff 0",
        "0.3 cut 0; 1.6 puff 0",
        "0.3 cut 0; 2.7 puff 0; 3.6 pop",
        "0.3 cut 0; 2.85 puff 0; 4.4 pop",
        "1.65 cut 0; 2.1 puff 0; 2.55 puff 1",
        "1.1 cut 0; 1.45 puff 0; 2.15 puff 1",
        "0.55 cut 0; 1 puff 0; 1.4 puff 1",
        "1.75 cut 0; 2.15 puff 0; 2.55 puff 1",
    };

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Trace(SnipLevels.Get(30), Solutions[29], 7);
        var second = Trace(SnipLevels.Get(30), Solutions[29], 7);
        var other = Trace(SnipLevels.Get(30), Solutions[29], 8);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void EveryLevelParsesInsideTheWorld()
    {
        Assert.Equal(SnipLevels.Count, SnipLevels.Sources.Length);
        Assert.Equal(100, SnipLevels.Count);
        for (var index = 0; index < SnipLevels.Sources.Length; index++)
        {
            Assert.True(SnipLevel.TryParse(SnipLevels.Sources[index], out var level, out var error),
                $"level {index + 1}: {error}");
            Assert.Equal(SnipLevel.StarCount, level.Stars.Length);
            Assert.True(level.Ropes.Length + level.Bubbles.Length > 0, $"level {index + 1} has nothing to hold the candy");
            AssertInside(level.Candy, index);
            AssertInside(level.Moogle, index);
            Assert.True(Vector2.Distance(level.Candy, level.Moogle) > SnipBoard.SenseRadius,
                $"level {index + 1} starts the candy at the moogle");
            for (var star = 0; star < level.Stars.Length; star++)
            {
                AssertInside(level.Stars[star], index);
            }

            for (var rope = 0; rope < level.Ropes.Length; rope++)
            {
                AssertInside(level.Ropes[rope].Anchor, index);
                Assert.True(level.Ropes[rope].Length >= Vector2.Distance(level.Ropes[rope].Anchor, level.Candy) - 0.01f,
                    $"level {index + 1} rope {rope} starts stretched");
            }
        }
    }

    [Fact]
    public void BrokenLevelSourcesAreRejected()
    {
        Assert.False(SnipLevel.TryParse("c 3 3; r 3 1 2; s 3 4; s 3 5; m 3 8", out _, out _));
        Assert.False(SnipLevel.TryParse("c 3 3; s 3 4; s 3 5; s 3 6; m 3 8", out _, out _));
        Assert.False(SnipLevel.TryParse("c 3 3; r 3 1 2; a 1 1 up; s 3 4; s 3 5; s 3 6; m 3 8", out _, out _));
        Assert.False(SnipLevel.TryParse("c 3 3; r 3 1 2; s 3 4; s 3 5; s 3 6", out _, out _));
        Assert.True(SnipLevel.TryParse("c 3 3; r 3 1 2; s 3 4; s 3 5; s 3 6; m 3 8", out _, out _));
    }

    [Fact]
    public void EveryLevelHasAThreeStarSolution()
    {
        Assert.Equal(SnipLevels.Count, Solutions.Length);
        for (var index = 0; index < Solutions.Length; index++)
        {
            var board = Play(SnipLevels.Get(index + 1), Solutions[index], 1, 12f);
            Assert.True(board.State == SnipState.Eaten, $"level {index + 1} ended {board.State}");
            Assert.True(board.StarCount == SnipLevel.StarCount, $"level {index + 1} took {board.StarCount} stars");
        }
    }

    [Fact]
    public void NoLevelSolvesItselfWithoutAMove()
    {
        for (var level = 1; level <= SnipLevels.Count; level++)
        {
            var board = Play(SnipLevels.Get(level), string.Empty, 1, 12f);
            Assert.True(board.State != SnipState.Eaten, $"level {level} feeds the moogle on its own");
        }
    }

    [Fact]
    public void SwipeAcrossARopeCutsItAndASwipeBesideItDoesNot()
    {
        var board = Load("c 3 4; r 3 1 3; s 5 2; s 5 3; s 5 4; m 3 9");
        Step(board, 0.5f);

        Assert.Equal(0, board.Cut(new Vector2(3.4f, 2.5f), new Vector2(4.4f, 2.5f)));
        Assert.True(board.RopeHolds(0));

        Assert.Equal(1, board.Cut(new Vector2(2.4f, 2.5f), new Vector2(3.6f, 2.5f)));
        Assert.False(board.RopeHolds(0));
        Assert.True(board.FreedThisFrame);
        Assert.Equal(1, board.Cuts);
    }

    [Fact]
    public void ABubbleLiftsTheCandyUntilItPops()
    {
        var board = Load("c 3 6; b 3 6; s 5 2; s 5 3; s 5 4; m 3 1.5");
        board.BeginFrame();
        board.Step(Frame);
        Assert.True(board.InBubble);
        var start = board.CandyPosition.Y;

        Step(board, 1f);
        Assert.True(board.CandyPosition.Y < start - 0.5f);

        board.BeginFrame();
        Assert.Equal(SnipTap.Pop, board.Tap(board.CandyPosition));
        Assert.True(board.PoppedThisFrame);
        Assert.False(board.InBubble);
        var popped = board.CandyPosition.Y;
        Step(board, 0.8f);
        Assert.True(board.CandyPosition.Y > popped);
    }

    [Fact]
    public void StarsAreCollectedInFlight()
    {
        var board = Load(SnipLevels.Sources[0]);
        Step(board, 0.2f);
        board.BeginFrame();
        board.Cut(new Vector2(2.5f, 1.6f), new Vector2(3.5f, 1.6f));
        var seen = 0;
        for (var frame = 0; frame < 120 && board.State == SnipState.Playing; frame++)
        {
            board.BeginFrame();
            board.Step(Frame);
            seen |= board.StarsThisFrame;
        }

        Assert.Equal(0b111, seen);
        Assert.Equal(3, board.StarCount);
        Assert.Equal(SnipState.Eaten, board.State);
    }

    [Fact]
    public void SpikesShatterTheCandy()
    {
        var board = Load("c 3 3; r 3 1 2; k 2 5 4 5; s 5 2; s 5 3; s 5 4; m 3 9");
        board.BeginFrame();
        board.Cut(new Vector2(2.5f, 2f), new Vector2(3.5f, 2f));
        Step(board, 1.5f);

        Assert.Equal(SnipState.Lost, board.State);
        Assert.Equal(SnipLoss.Spikes, board.Loss);
    }

    [Fact]
    public void ACushionPushesTheCandyOnlyWhenItIsInFront()
    {
        var board = Load("c 3 4; r 3 1 3; a 1.5 4 e; a 1.5 6 w; s 5 2; s 5 3; s 5 4; m 3 9");
        Step(board, 0.3f);

        board.BeginFrame();
        Assert.Equal(SnipTap.Puff, board.Tap(new Vector2(1.5f, 6f)));
        Assert.Equal(1, board.PuffedThisFrame);
        Assert.False(board.PuffHit);

        board.BeginFrame();
        board.Puff(0);
        Assert.True(board.PuffHit);
        Assert.True(board.World.Velocity(board.Candy).X > 1f);
    }

    [Fact]
    public void CandyThatFallsOffTheWorldIsLost()
    {
        var board = Load("c 1 3; r 1 1 2; s 5 2; s 5 3; s 5 4; m 5 9");
        board.BeginFrame();
        board.Cut(new Vector2(0.5f, 2f), new Vector2(1.5f, 2f));
        Step(board, 3f);

        Assert.Equal(SnipState.Lost, board.State);
        Assert.Equal(SnipLoss.Fell, board.Loss);
    }

    private static void AssertInside(Vector2 point, int index)
    {
        Assert.True(point.X >= 0f && point.X <= SnipBoard.WorldWidth && point.Y >= 0f && point.Y <= SnipBoard.WorldHeight,
            $"level {index + 1} places a piece at {point}");
    }

    private static SnipBoard Load(string source)
    {
        Assert.True(SnipLevel.TryParse(source, out var level, out var error), error);
        var board = new SnipBoard();
        board.Load(level, GameRandom.FromSeed(1));
        return board;
    }

    private static void Step(SnipBoard board, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += Frame)
        {
            board.BeginFrame();
            board.Step(Frame);
        }
    }

    private static string Trace(SnipLevel level, string script, ulong seed)
    {
        var trace = new StringBuilder();
        Play(level, script, seed, 6f, trace);
        return trace.ToString();
    }

    private static SnipBoard Play(SnipLevel level, string script, ulong seed, float limit, StringBuilder? trace = null)
    {
        var board = new SnipBoard();
        board.Load(level, GameRandom.FromSeed(seed));
        var actions = script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var next = 0;
        var time = 0f;
        while (time < limit && board.State == SnipState.Playing)
        {
            board.BeginFrame();
            while (next < actions.Length)
            {
                var tokens = actions[next].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (float.Parse(tokens[0], CultureInfo.InvariantCulture) > time)
                {
                    break;
                }

                Apply(board, tokens);
                next++;
            }

            board.Step(Frame);
            time += Frame;
            if (trace is null)
            {
                continue;
            }

            var position = board.CandyPosition;
            trace.Append(position.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(position.Y.ToString("R", CultureInfo.InvariantCulture)).Append(board.Blinking ? '*' : ' ')
                .Append(board.StarMask).Append(';');
        }

        return board;
    }

    private static void Apply(SnipBoard board, string[] tokens)
    {
        switch (tokens[1])
        {
            case "cut":
            {
                var world = board.World;
                var rope = board.Rope(int.Parse(tokens[2], CultureInfo.InvariantCulture));
                var middle = world.RopeSegments(rope) / 2;
                var start = world.RopePoint(rope, middle);
                var end = world.RopePoint(rope, middle + 1);
                var along = end - start;
                var across = Vector2.Normalize(new Vector2(-along.Y, along.X)) * 0.3f;
                var center = (start + end) * 0.5f;
                Assert.True(board.Cut(center - across, center + across) > 0);
                return;
            }
            case "pop":
                board.Pop();
                return;
            default:
                board.Puff(int.Parse(tokens[2], CultureInfo.InvariantCulture));
                return;
        }
    }
}
