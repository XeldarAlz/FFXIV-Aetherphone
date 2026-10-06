using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Trivia;
using Aetherphone.Core.Game;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TriviaDeckTests
{
    private const float Frame = 1f / 60f;
    private const int QuestionsPlayed = 15;
    private const uint MountBase = 1;
    private const uint MinionBase = 101;
    private const int PoolSize = 40;
    private const uint MinionLast = MinionBase + (uint)PoolSize - 1u;

    private sealed class FakeTriviaSource : ITriviaSource
    {
        private readonly uint[] mounts = Pool(MountBase);
        private readonly uint[] minions = Pool(MinionBase);

        public uint[] PoolOf(TriviaCategory category)
        {
            switch (category)
            {
                case TriviaCategory.Mounts:
                    return mounts;
                case TriviaCategory.Minions:
                    return minions;
                default:
                    return Array.Empty<uint>();
            }
        }

        public NamedIcon EntryOf(TriviaCategory category, uint rowId) =>
            new(string.Concat(category.ToString(), "-", rowId.ToString()), rowId);

        private static uint[] Pool(uint first)
        {
            var ids = new uint[PoolSize];
            for (var index = 0; index < PoolSize; index++)
            {
                ids[index] = first + (uint)index;
            }

            return ids;
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(QuestionsPlayed, first.Correct);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void AnUnavailableCategoryDoesNotStart()
    {
        var board = new TriviaBoard(new FakeTriviaSource());

        Assert.False(board.IsAvailable(TriviaCategory.Actions));
        Assert.False(board.Start(TriviaCategory.Emotes, GameRandom.FromSeed(1)));
        Assert.Equal(TriviaState.Ready, board.State);
        Assert.True(board.Start(TriviaCategory.All, GameRandom.FromSeed(1)));
        Assert.Equal(TriviaState.Asking, board.State);
    }

    [Fact]
    public void AChosenCategoryOnlyDrawsFromItsPoolWithoutRepeatingAnOption()
    {
        var board = Started(TriviaCategory.Minions, 7);
        for (var question = 0; question < 20; question++)
        {
            for (var index = 0; index < TriviaBoard.Options; index++)
            {
                var icon = board.Option(index).IconId;
                Assert.InRange(icon, MinionBase, MinionLast);
                for (var other = index + 1; other < TriviaBoard.Options; other++)
                {
                    Assert.NotEqual(icon, board.Option(other).IconId);
                }
            }

            board.Answer(board.CorrectIndex);
            Run(board, TriviaBoard.RevealSeconds);
        }

        Assert.Equal(20, board.Correct);
    }

    [Fact]
    public void ACorrectAnswerScoresBasePlusSpeedBonusTimesTheMultiplier()
    {
        var board = Started(TriviaCategory.Mounts, 3);
        board.Step(2f);

        Assert.True(board.Answer(board.CorrectIndex));

        Assert.Equal((TriviaBoard.BasePoints + 8) * ComboMeter.MultiplierFor(1), board.LastPoints);
        Assert.Equal(board.LastPoints, board.Score);
        Assert.Equal(1, board.Combo.Count);
        Assert.Equal(TriviaState.Revealing, board.State);
        Assert.Equal(TriviaBoard.StartLives, board.Lives);
    }

    [Fact]
    public void AWrongAnswerCostsALifeAndResetsTheCombo()
    {
        var board = Started(TriviaCategory.Mounts, 3);
        board.Answer(board.CorrectIndex);
        Run(board, TriviaBoard.RevealSeconds);
        var scoreBefore = board.Score;

        Assert.False(board.Answer((board.CorrectIndex + 1) % TriviaBoard.Options));

        Assert.Equal(TriviaBoard.StartLives - 1, board.Lives);
        Assert.Equal(0, board.Combo.Count);
        Assert.Equal(0, board.LastPoints);
        Assert.Equal(scoreBefore, board.Score);
        Assert.Equal(1, board.BestCombo);
    }

    [Fact]
    public void RunningOutOfTimeCostsALifeAndMovesOn()
    {
        var board = Started(TriviaBoard.PickableAt(0), 5);
        var asked = board.Asked;

        Assert.True(board.Step(TriviaBoard.QuestionSeconds + Frame));

        Assert.Equal(TriviaBoard.StartLives - 1, board.Lives);
        Assert.Equal(TriviaState.Revealing, board.State);
        Assert.Equal(-1, board.PickedIndex);
        Run(board, TriviaBoard.RevealSeconds);
        Assert.Equal(TriviaState.Asking, board.State);
        Assert.Equal(asked + 1, board.Asked);
        Assert.True(board.TimeLeft > TriviaBoard.QuestionSeconds - 0.1f);
    }

    [Fact]
    public void ThreeMissesEndTheRun()
    {
        var board = Started(TriviaCategory.Mounts, 9);
        for (var miss = 0; miss < TriviaBoard.StartLives; miss++)
        {
            Assert.NotEqual(TriviaState.Over, board.State);
            board.Answer((board.CorrectIndex + 1) % TriviaBoard.Options);
            Run(board, TriviaBoard.RevealSeconds);
        }

        Assert.Equal(TriviaState.Over, board.State);
        Assert.Equal(0, board.Lives);
        Assert.False(board.Answer(0));
        Assert.False(board.Step(Frame));
    }

    private static TriviaBoard Started(TriviaCategory category, int seed)
    {
        var board = new TriviaBoard(new FakeTriviaSource());
        Assert.True(board.Start(category, GameRandom.FromSeed((ulong)seed)));
        return board;
    }

    private static void Run(TriviaBoard board, float seconds)
    {
        var frames = (int)MathF.Ceiling(seconds / Frame) + 1;
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame);
        }
    }

    private static TriviaBoard Play(int seed, out string trace)
    {
        var board = Started(TriviaCategory.All, seed);
        var builder = new StringBuilder();
        for (var question = 0; question < QuestionsPlayed; question++)
        {
            builder.Append((int)board.Kind).Append(':').Append(board.CorrectIndex).Append(':');
            for (var index = 0; index < TriviaBoard.Options; index++)
            {
                builder.Append(board.Option(index).IconId).Append(',');
            }

            board.Step(Frame * (question % 7 + 1));
            board.Answer(board.CorrectIndex);
            builder.Append(board.LastPoints).Append(';');
            Run(board, TriviaBoard.RevealSeconds);
        }

        trace = builder.ToString();
        return board;
    }
}
