using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Crawler;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CrawlerBoardTests
{
    private const float Frame = 1f / 60f;
    private static readonly CrawlerControls Idle = new(0f, 0f, false, false, Vector2.Zero);
    private static readonly CrawlerControls FireOnly = new(0f, 0f, true, false, Vector2.Zero);

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(2024, out var firstTrace);
        var second = Play(2024, out var secondTrace);
        Play(31, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Player, second.Player);
        Assert.Equal(first.AliveSegments, second.AliveSegments);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void AHeadBlockedByAMushroomDropsARowAndTurnsAround()
    {
        var board = Empty(1);
        var head = board.SpawnChain(5, 3, 1, 1);
        board.PlaceMushroom(6, 3);

        board.AdvanceCrawlers();

        var segment = board.SegmentAt(head);
        Assert.Equal(5, segment.Column);
        Assert.Equal(4, segment.Row);
        Assert.Equal(-1, segment.StepX);
        board.AdvanceCrawlers();
        Assert.Equal(4, board.SegmentAt(head).Column);
        Assert.Equal(4, board.SegmentAt(head).Row);
    }

    [Fact]
    public void AHeadAtTheWallDropsARowAndTurnsAround()
    {
        var board = Empty(2);
        var head = board.SpawnChain(CrawlerBoard.Columns - 1, 2, 1, 1);

        board.AdvanceCrawlers();

        Assert.Equal(CrawlerBoard.Columns - 1, board.SegmentAt(head).Column);
        Assert.Equal(3, board.SegmentAt(head).Row);
        Assert.Equal(-1, board.SegmentAt(head).StepX);
    }

    [Fact]
    public void TheBodyFollowsTheExactPathOfTheHead()
    {
        var board = Empty(3);
        var head = board.SpawnChain(6, 2, 4, 1);
        board.PlaceMushroom(8, 2);
        board.PlaceMushroom(3, 3);
        var history = new List<(int Column, int Row)>();
        for (var link = 3; link >= 1; link--)
        {
            history.Add((6 - link, 2));
        }

        history.Add((6, 2));
        for (var tick = 0; tick < 12; tick++)
        {
            board.AdvanceCrawlers();
            history.Add((board.SegmentAt(head).Column, board.SegmentAt(head).Row));
            var follower = board.SegmentAt(head).Behind;
            for (var link = 1; follower >= 0; link++)
            {
                var expected = history[history.Count - 1 - link];
                Assert.Equal(expected.Column, board.SegmentAt(follower).Column);
                Assert.Equal(expected.Row, board.SegmentAt(follower).Row);
                follower = board.SegmentAt(follower).Behind;
            }
        }
    }

    [Fact]
    public void ShootingABodySegmentSplitsTheCrawlerAndSproutsAMushroom()
    {
        var board = Empty(4);
        var head = board.SpawnChain(10, 4, 5, 1);
        var first = board.SegmentAt(head).Behind;
        var second = board.SegmentAt(first).Behind;
        var third = board.SegmentAt(second).Behind;
        Assert.Equal(8, board.SegmentAt(second).Column);

        board.HitSegment(second);

        Assert.False(board.SegmentAt(second).Alive);
        Assert.Equal(CrawlerBoard.MushroomHealth, board.MushroomAt(8, 4));
        Assert.True(board.SegmentAt(third).Head);
        Assert.Equal(-1, board.SegmentAt(first).Behind);
        Assert.Equal(head, board.SegmentAt(first).Ahead);
        Assert.Equal(4, board.AliveSegments);
        Assert.Equal(2, CountHeads(board));
        Assert.Equal(CrawlerBoard.BodyPoints, board.Score);
        Assert.Equal(1, board.HitCount);
    }

    [Fact]
    public void ShootingTheHeadPromotesTheNextSegmentAndPaysAHundred()
    {
        var board = Empty(5);
        var head = board.SpawnChain(10, 4, 3, 1);
        var next = board.SegmentAt(head).Behind;

        board.HitSegment(head);

        Assert.True(board.SegmentAt(next).Head);
        Assert.Equal(CrawlerBoard.HeadPoints, board.Score);
        Assert.Equal(CrawlerBoard.MushroomHealth, board.MushroomAt(10, 4));
        Assert.True(board.HitAt(0).Head);
    }

    [Fact]
    public void TheNewHeadTurnsDownAtTheMushroomLeftByTheShotSegment()
    {
        var board = Empty(6);
        var head = board.SpawnChain(10, 4, 5, 1);
        var first = board.SegmentAt(head).Behind;
        var second = board.SegmentAt(first).Behind;
        var third = board.SegmentAt(second).Behind;
        board.HitSegment(second);

        board.AdvanceCrawlers();

        Assert.Equal(7, board.SegmentAt(third).Column);
        Assert.Equal(5, board.SegmentAt(third).Row);
        Assert.Equal(-1, board.SegmentAt(third).StepX);
    }

    [Fact]
    public void TheCrawlerBouncesBackUpFromTheBottomRow()
    {
        var board = Empty(7);
        var head = board.SpawnChain(5, CrawlerBoard.Rows - 1, 1, 1);
        board.PlaceMushroom(6, CrawlerBoard.Rows - 1);

        board.AdvanceCrawlers();

        Assert.Equal(CrawlerBoard.Rows - 2, board.SegmentAt(head).Row);
        Assert.Equal(-1, board.SegmentAt(head).StepY);
        Assert.Equal(-1, board.SegmentAt(head).StepX);
    }

    [Fact]
    public void OnceInThePlayerZoneTheCrawlerNeverLeavesIt()
    {
        var board = Empty(8);
        var head = board.SpawnChain(0, CrawlerBoard.ZoneTop, 1, 1);
        board.PlaceMushroom(4, CrawlerBoard.ZoneTop + 1);
        board.PlaceMushroom(9, CrawlerBoard.ZoneTop + 3);
        board.PlaceMushroom(2, CrawlerBoard.Rows - 2);

        for (var tick = 0; tick < 400; tick++)
        {
            board.AdvanceCrawlers();
            Assert.InRange(board.SegmentAt(head).Row, CrawlerBoard.ZoneTop, CrawlerBoard.Rows - 1);
            Assert.InRange(board.SegmentAt(head).Column, 0, CrawlerBoard.Columns - 1);
        }
    }

    [Fact]
    public void AMushroomTakesFourShotsAndPaysOnePoint()
    {
        var board = Empty(9);
        board.SpawnChain(0, 0, 1, 1);
        var column = (int)board.Player.X;
        board.PlaceMushroom(column, 10);
        var chips = 0;
        for (var frame = 0; frame < 240 && board.MushroomAt(column, 10) > 0; frame++)
        {
            board.Step(Frame, FireOnly);
            chips += board.ChipCount;
        }

        Assert.Equal(0, board.MushroomAt(column, 10));
        Assert.Equal(CrawlerBoard.MushroomHealth, chips);
        Assert.Equal(CrawlerBoard.MushroomPoints, board.Score);
    }

    [Fact]
    public void AFleaDropsMushroomsOnlyInItsOwnColumn()
    {
        var board = Empty(10);
        board.SpawnChain(0, 0, 1, 1);
        board.LaunchFlea(3);
        for (var frame = 0; frame < 240 && board.FleaActive; frame++)
        {
            board.Step(Frame, Idle);
        }

        Assert.False(board.FleaActive);
        var dropped = 0;
        for (var row = 0; row < CrawlerBoard.Rows; row++)
        {
            for (var column = 0; column < CrawlerBoard.Columns; column++)
            {
                if (board.MushroomAt(column, row) == 0)
                {
                    continue;
                }

                Assert.Equal(3, column);
                Assert.InRange(row, 1, CrawlerBoard.Rows - 2);
                dropped++;
            }
        }

        Assert.True(dropped > 0);
    }

    [Fact]
    public void ASpiderShotUpClosePaysTheMost()
    {
        Assert.Equal(900, CrawlerBoard.SpiderPointsFor(1f));
        Assert.Equal(600, CrawlerBoard.SpiderPointsFor(3f));
        Assert.Equal(300, CrawlerBoard.SpiderPointsFor(6f));
    }

    [Fact]
    public void ClearingTheCrawlerStartsAFasterWave()
    {
        var board = Empty(11);
        board.Step(Frame, Idle);
        Assert.True(board.WaveClearedThisFrame);

        for (var frame = 0; frame < 90; frame++)
        {
            board.Step(Frame, Idle);
        }

        Assert.Equal(2, board.Wave);
        Assert.Equal(CrawlerBoard.ChainLength + CrawlerBoard.ExtraHeadsFor(2), board.AliveSegments);
        Assert.True(CrawlerBoard.TickFor(2) < CrawlerBoard.TickFor(1));
    }

    [Fact]
    public void LosingALifeRegrowsDamagedMushroomsForFivePointsEach()
    {
        var board = Empty(12);
        board.SpawnChain(0, 0, 1, 1);
        var column = (int)board.Player.X;
        board.PlaceMushroom(column, 8);
        for (var frame = 0; frame < 60 && board.MushroomAt(column, 8) == CrawlerBoard.MushroomHealth; frame++)
        {
            board.Step(Frame, FireOnly);
        }

        Assert.Equal(CrawlerBoard.MushroomHealth - 1, board.MushroomAt(column, 8));
        board.SpawnChain(column, CrawlerBoard.Rows - 1, 1, 1);
        board.Step(Frame, Idle);
        Assert.True(board.PlayerLostThisFrame);
        Assert.Equal(CrawlerBoard.StartLives - 1, board.Lives);
        var regrew = false;
        for (var frame = 0; frame < 120 && !regrew; frame++)
        {
            board.Step(Frame, Idle);
            regrew = board.RegrowThisFrame;
        }

        Assert.True(regrew);
        Assert.Equal(CrawlerBoard.MushroomHealth, board.MushroomAt(column, 8));
        Assert.Equal(CrawlerBoard.RegrowPoints, board.Score);
        Assert.False(board.GameOver);
    }

    private static CrawlerBoard Empty(ulong seed)
    {
        var board = new CrawlerBoard();
        board.Reset(GameRandom.FromSeed(seed));
        board.ClearField();
        board.ClearCrawlers();
        return board;
    }

    private static int CountHeads(CrawlerBoard board)
    {
        var heads = 0;
        for (var index = 0; index < board.SegmentCapacity; index++)
        {
            if (board.SegmentAt(index).Alive && board.SegmentAt(index).Head)
            {
                heads++;
            }
        }

        return heads;
    }

    private static CrawlerBoard Play(ulong seed, out string trace)
    {
        var board = new CrawlerBoard();
        board.Reset(GameRandom.FromSeed(seed));
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 40 && !board.GameOver; frame++)
        {
            var move = (frame / 50 % 3) - 1;
            var controls = new CrawlerControls(move, frame / 70 % 2 == 0 ? 0.5f : -0.5f, true, false, Vector2.Zero);
            board.Step(Frame, controls);
            if (frame % 30 != 0)
            {
                continue;
            }

            builder.Append(board.Score).Append(',').Append(board.AliveSegments).Append(',').Append(board.Wave)
                .Append(',').Append(board.Lives).Append(',')
                .Append(board.Player.X.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(board.Player.Y.ToString("F3", CultureInfo.InvariantCulture)).Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
