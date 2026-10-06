using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.CrystalDrop;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CrystalDropBoardTests
{
    private const float Frame = 1f / 60f;
    private const float FloorY = CrystalDropBoard.JarHeight - 0.073f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.Merges, second.Merges);
        Assert.Equal(first.HeldTier, second.HeldTier);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void TwoCrystalsOfTheSameTierMergeIntoTheNextTier()
    {
        var board = Seeded(1);
        board.Place(new Vector2(0.45f, FloorY), 2);
        board.Place(new Vector2(0.55f, FloorY), 2);

        board.Step(Frame);

        Assert.Equal(1, board.Count);
        Assert.Equal(1, board.MergeCount);
        Assert.Equal(3, board.Merge(0).Tier);
        Assert.Equal(3, board.At(0).Tier);
        Assert.Equal(CrystalDropBoard.PointsOf(2), board.Score);
        Assert.Equal(1, board.Merges);
        Assert.Equal(1, board.Combo.Count);
    }

    [Fact]
    public void DifferentTiersRestAgainstEachOtherWithoutMerging()
    {
        var board = Seeded(2);
        board.Place(new Vector2(0.45f, FloorY), 2);
        board.Place(new Vector2(0.55f, FloorY), 3);

        for (var frame = 0; frame < 60; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(2, board.Count);
        Assert.Equal(0, board.Score);
    }

    [Fact]
    public void MergesInsideTheComboWindowMultiplyThePoints()
    {
        var board = Seeded(3);
        board.Place(new Vector2(0.2f, FloorY), 0);
        board.Place(new Vector2(0.26f, FloorY), 0);
        board.Step(Frame);
        var single = board.Score;
        board.ClearMerges();
        for (var hit = 1; hit < 4; hit++)
        {
            var spot = 0.2f + hit * 0.22f;
            board.Place(new Vector2(spot, FloorY), 0);
            board.Place(new Vector2(spot + 0.06f, FloorY), 0);
            board.Step(Frame);
            board.ClearMerges();
        }

        Assert.Equal(4, board.Combo.Count);
        Assert.Equal(2, board.Combo.Multiplier);
        Assert.Equal(single * 3 + single * 2, board.Score);
        Assert.Equal(4, board.BestCombo);
    }

    [Fact]
    public void ACrystalRestingAboveTheDangerLineEndsTheRunAfterTheOverflowLimit()
    {
        var board = Seeded(4);
        var bottom = CrystalDropBoard.JarHeight - CrystalDropBoard.RadiusOf(10);
        var middle = bottom - CrystalDropBoard.RadiusOf(10) - CrystalDropBoard.RadiusOf(9);
        var top = middle - CrystalDropBoard.RadiusOf(9) - CrystalDropBoard.RadiusOf(10);
        board.Place(new Vector2(0.5f, bottom), 10);
        board.Place(new Vector2(0.5f, middle), 9);
        board.Place(new Vector2(0.5f, top), 10);

        var graceFrames = (int)(CrystalDropBoard.SpawnGrace / Frame) + 2;
        for (var frame = 0; frame < graceFrames; frame++)
        {
            board.Step(Frame);
        }

        Assert.False(board.GameOver);
        Assert.True(board.OverflowSeconds > 0f);
        for (var frame = 0; frame < (int)(CrystalDropBoard.OverflowLimit / Frame) + 2; frame++)
        {
            board.Step(Frame);
        }

        Assert.True(board.GameOver);
        Assert.False(board.CanDrop);
    }

    [Fact]
    public void TheDropCooldownRefusesASecondDropUntilItExpires()
    {
        var board = Seeded(5);
        var held = board.HeldTier;
        var next = board.NextTier;

        Assert.True(board.Drop(0.5f));
        Assert.Equal(next, board.HeldTier);
        Assert.False(board.Drop(0.5f));
        Assert.Equal(1, board.Count);
        Assert.Equal(held, board.At(0).Tier);

        for (var frame = 0; frame < (int)(CrystalDropBoard.DropCooldown / Frame) + 2; frame++)
        {
            board.Step(Frame);
        }

        Assert.True(board.Drop(0.5f));
        Assert.Equal(2, board.Count);
    }

    private static CrystalDropBoard Seeded(int seed)
    {
        var board = new CrystalDropBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static CrystalDropBoard Play(int seed, out string trace)
    {
        var board = Seeded(seed);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 40 && !board.GameOver; frame++)
        {
            if (frame % 24 == 0)
            {
                board.Drop(0.15f + frame * 7 % 10 * 0.07f);
            }

            board.Step(Frame);
            for (var merge = 0; merge < board.MergeCount; merge++)
            {
                builder.Append(board.Merge(merge).Tier).Append(':').Append(board.Merge(merge).Points).Append(' ');
            }

            board.ClearMerges();
            if (frame % 120 == 0)
            {
                builder.Append('|').Append(board.Count).Append('/').Append(board.Score).Append('/').Append(board.HeldTier);
            }
        }

        trace = builder.ToString();
        return board;
    }
}
