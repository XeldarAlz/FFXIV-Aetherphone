using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Mahjong;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MahjongBoardTests
{
    private const int SeedsPerLayout = 60;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Dealt(MahjongLayouts.Dragon, 4242);
        var second = Dealt(MahjongLayouts.Dragon, 4242);
        var other = Dealt(MahjongLayouts.Dragon, 77);

        Assert.Equal(Faces(first), Faces(second));
        Assert.NotEqual(Faces(first), Faces(other));
        for (var pair = 0; pair < first.SolutionPairs; pair++)
        {
            first.SolutionPair(pair, out var firstA, out var firstB);
            second.SolutionPair(pair, out var secondA, out var secondB);
            Assert.Equal(firstA, secondA);
            Assert.Equal(firstB, secondB);
        }
    }

    [Fact]
    public void LayoutsHoldTheirTierTileCounts()
    {
        Assert.Equal(72, MahjongLayouts.Moogle.Count);
        Assert.Equal(72, MahjongLayouts.Bridge.Count);
        Assert.Equal(108, MahjongLayouts.Tower.Count);
        Assert.Equal(108, MahjongLayouts.Crossroads.Count);
        Assert.Equal(144, MahjongLayouts.Fortress.Count);
        Assert.Equal(144, MahjongLayouts.Dragon.Count);
        Assert.Equal(0, MahjongLayouts.IndexFor(0, 0));
        Assert.Equal(1, MahjongLayouts.IndexFor(0, 1));
        Assert.Equal(4, MahjongLayouts.IndexFor(2, 0));
    }

    [Fact]
    public void NoTwoTilesShareASpotAndEveryRaisedTileRestsOnAnother()
    {
        for (var layoutIndex = 0; layoutIndex < MahjongLayouts.All.Length; layoutIndex++)
        {
            var layout = MahjongLayouts.All[layoutIndex];
            for (var tile = 0; tile < layout.Count; tile++)
            {
                for (var other = tile + 1; other < layout.Count; other++)
                {
                    Assert.False(layout.Layer(tile) == layout.Layer(other) && layout.Overlaps(tile, other));
                }

                if (layout.Layer(tile) > 0)
                {
                    Assert.True(layout.Below(tile).Length > 0);
                }
            }
        }
    }

    [Fact]
    public void EverySeededDealIsSolvableInItsRecordedOrder()
    {
        for (var layoutIndex = 0; layoutIndex < MahjongLayouts.All.Length; layoutIndex++)
        {
            for (var seed = 1; seed <= SeedsPerLayout; seed++)
            {
                var board = Dealt(MahjongLayouts.All[layoutIndex], (ulong)(seed * 7919 + layoutIndex));
                Assert.Equal(board.Layout.Count / 2, board.SolutionPairs);
                SolveFromRecordedOrder(board);
            }
        }
    }

    [Fact]
    public void ShufflingMidGameKeepsTheBoardSolvable()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var board = Dealt(MahjongLayouts.Fortress, (ulong)seed);
            for (var pair = 0; pair < 20; pair++)
            {
                board.SolutionPair(pair, out var first, out var second);
                Assert.True(board.TryMatch(first, second));
            }

            var remaining = board.Remaining;
            Assert.True(board.Shuffle());
            Assert.Equal(remaining, board.Remaining);
            Assert.Equal(1, board.Shuffles);
            Assert.False(board.CanUndo);
            SolveFromRecordedOrder(board);
        }
    }

    [Fact]
    public void ShuffleKeepsTheSameFacesOnTheTable()
    {
        var board = Dealt(MahjongLayouts.Tower, 5);
        var before = GroupCounts(board);
        Assert.True(board.Shuffle());
        Assert.Equal(before, GroupCounts(board));
    }

    [Fact]
    public void AFreeTileHasNothingOnTopAndAnOpenSide()
    {
        var layout = new MahjongLayout(new LayoutBlock[]
        {
            new(0, 0, 0, 3, 1), new(1, 2, 0, 1, 1),
        });
        var board = new MahjongBoard();
        board.Deal(layout, GameRandom.FromSeed(3));

        var left = TileAt(layout, 0, 0, 0);
        var middle = TileAt(layout, 2, 0, 0);
        var right = TileAt(layout, 4, 0, 0);
        var top = TileAt(layout, 2, 0, 1);
        Assert.True(board.IsFree(left));
        Assert.True(board.IsFree(right));
        Assert.False(board.IsFree(middle));
        Assert.True(board.IsFree(top));
    }

    [Fact]
    public void ATileUnderAnotherIsNeverFreeAndAMiddleTileFreesWhenAnEndLeaves()
    {
        var layout = new MahjongLayout(new LayoutBlock[]
        {
            new(0, 0, 0, 3, 1), new(1, 0, 0, 1, 1),
        });
        var board = new MahjongBoard();
        board.Deal(layout, GameRandom.FromSeed(9));
        var left = TileAt(layout, 0, 0, 0);
        var middle = TileAt(layout, 2, 0, 0);
        var right = TileAt(layout, 4, 0, 0);
        var top = TileAt(layout, 0, 0, 1);

        Assert.False(board.IsFree(left));
        Assert.False(board.IsFree(middle));
        Assert.True(board.IsFree(right));
        Assert.True(board.IsFree(top));
        board.SolutionPair(0, out var first, out var second);
        Assert.True(board.TryMatch(first, second));
        board.SolutionPair(1, out first, out second);
        Assert.True(board.TryMatch(first, second));
        Assert.True(board.Cleared);
    }

    [Fact]
    public void BlockedOrMismatchedPairsAreRefused()
    {
        var board = Dealt(MahjongLayouts.Moogle, 11);
        var blocked = -1;
        for (var tile = 0; tile < board.Layout.Count && blocked < 0; tile++)
        {
            if (!board.IsFree(tile))
            {
                blocked = tile;
            }
        }

        var partner = -1;
        for (var tile = 0; tile < board.Layout.Count && partner < 0; tile++)
        {
            if (tile != blocked && MahjongTiles.Matches(board.Face(tile), board.Face(blocked)))
            {
                partner = tile;
            }
        }

        Assert.False(board.TryMatch(blocked, partner));
        Assert.False(board.TryMatch(partner, partner));
        Assert.Equal(board.Layout.Count, board.Remaining);
    }

    [Fact]
    public void AnySeasonMatchesAnySeasonAndFlowersStayApart()
    {
        Assert.True(MahjongTiles.Matches(MahjongTiles.FirstSeason, MahjongTiles.FirstSeason + 3));
        Assert.True(MahjongTiles.Matches(MahjongTiles.FirstSeason + 1, MahjongTiles.FirstSeason + 2));
        Assert.True(MahjongTiles.Matches(MahjongTiles.FirstFlower, MahjongTiles.FirstFlower + 3));
        Assert.False(MahjongTiles.Matches(MahjongTiles.FirstSeason, MahjongTiles.FirstFlower));
        Assert.True(MahjongTiles.Matches(MahjongTiles.FirstCrystal + 2, MahjongTiles.FirstCrystal + 2));
        Assert.False(MahjongTiles.Matches(MahjongTiles.FirstCrystal + 2, MahjongTiles.FirstGil + 2));
        Assert.False(MahjongTiles.Matches(MahjongTiles.FirstWind, MahjongTiles.FirstWind + 1));
        Assert.Equal(TileKind.Dragon, MahjongTiles.KindOf(MahjongTiles.FirstDragon + 2));
        Assert.Equal(9, MahjongTiles.Rank(MahjongTiles.FirstRole + 8));
    }

    [Fact]
    public void TheFullSetIsOneHundredFortyFourTilesOfMatchingPairs()
    {
        var counts = new int[MahjongTiles.FaceCount];
        for (var pair = 0; pair < MahjongTiles.PairCount; pair++)
        {
            Assert.True(MahjongTiles.Matches(MahjongTiles.PairFirst(pair), MahjongTiles.PairSecond(pair)));
            counts[MahjongTiles.PairFirst(pair)]++;
            counts[MahjongTiles.PairSecond(pair)]++;
        }

        var total = 0;
        for (var face = 0; face < counts.Length; face++)
        {
            Assert.Equal(face < MahjongTiles.FirstSeason ? 4 : 1, counts[face]);
            total += counts[face];
        }

        Assert.Equal(144, total);
    }

    [Fact]
    public void UndoPutsTheLastPairBack()
    {
        var board = Dealt(MahjongLayouts.Bridge, 21);
        board.SolutionPair(0, out var first, out var second);
        Assert.True(board.TryMatch(first, second));
        Assert.False(board.IsPresent(first));

        Assert.True(board.Undo(out var restoredFirst, out var restoredSecond));
        Assert.Equal(first, restoredFirst);
        Assert.Equal(second, restoredSecond);
        Assert.True(board.IsPresent(first));
        Assert.True(board.IsPresent(second));
        Assert.Equal(board.Layout.Count, board.Remaining);
        Assert.Equal(1, board.Undos);
        Assert.False(board.Undo(out _, out _));
    }

    [Fact]
    public void HintFindsAFreeMatchingPair()
    {
        var board = Dealt(MahjongLayouts.Crossroads, 8);
        Assert.True(board.HasMoves);
        Assert.True(board.FreePairs > 0);
        Assert.True(board.FindHint(out var first, out var second));
        Assert.True(board.CanPair(first, second));
    }

    private static MahjongBoard Dealt(MahjongLayout layout, ulong seed)
    {
        var board = new MahjongBoard();
        board.Deal(layout, GameRandom.FromSeed(seed));
        return board;
    }

    private static void SolveFromRecordedOrder(MahjongBoard board)
    {
        var pairs = board.SolutionPairs;
        for (var pair = 0; pair < pairs; pair++)
        {
            board.SolutionPair(pair, out var first, out var second);
            Assert.True(board.TryMatch(first, second));
        }

        Assert.True(board.Cleared);
    }

    private static int[] Faces(MahjongBoard board)
    {
        var faces = new int[board.Layout.Count];
        for (var tile = 0; tile < faces.Length; tile++)
        {
            faces[tile] = board.Face(tile);
        }

        return faces;
    }

    private static int[] GroupCounts(MahjongBoard board)
    {
        var counts = new int[MahjongTiles.FaceCount];
        for (var tile = 0; tile < board.Layout.Count; tile++)
        {
            if (board.IsPresent(tile))
            {
                counts[board.Face(tile)]++;
            }
        }

        return counts;
    }

    private static int TileAt(MahjongLayout layout, int x, int y, int layer)
    {
        for (var tile = 0; tile < layout.Count; tile++)
        {
            if (layout.X(tile) == x && layout.Y(tile) == y && layout.Layer(tile) == layer)
            {
                return tile;
            }
        }

        return -1;
    }
}
