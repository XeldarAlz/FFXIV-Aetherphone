using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.WordRun;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WordRunBoardTests
{
    private const int ReplayWords = 12;
    private static readonly string[] ReplayAnswers =
    {
        "APPLE", "BREAD", "CRANE", "DRIVE", "EARTH", "FLAME", "GRAPE", "HOUSE", "INDEX", "JUICE", "KNIFE", "LEMON",
        "MANGO", "NIGHT", "OCEAN", "PIANO", "QUEEN", "RIVER", "STONE", "TIGER",
    };

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Answer, second.Answer);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Theory]
    [InlineData("APPLE", "PAPAL", "PPCAP")]
    [InlineData("ROBOT", "BOOTS", "PCPPA")]
    [InlineData("LEVEL", "HELLO", "ACPPA")]
    [InlineData("CRANE", "CRANE", "CCCCC")]
    [InlineData("SPEED", "ERASE", "PAAPP")]
    [InlineData("ABBEY", "BABES", "PPCCA")]
    public void EvaluationNeverOverReportsDuplicateLetters(string answer, string guess, string expected)
    {
        Span<WordTile> result = stackalloc WordTile[WordRunBoard.WordLength];
        Span<int> remaining = stackalloc int[WordRunBoard.LetterCount];
        WordRunBoard.Evaluate(guess, answer, result, remaining);
        var actual = new char[WordRunBoard.WordLength];
        for (var index = 0; index < actual.Length; index++)
        {
            actual[index] = result[index] switch
            {
                WordTile.Correct => 'C',
                WordTile.Present => 'P',
                _ => 'A',
            };
        }

        Assert.Equal(expected, new string(actual));
    }

    [Theory]
    [InlineData(1, 0f, 550)]
    [InlineData(1, 10f, 525)]
    [InlineData(3, 20f, 300)]
    [InlineData(6, 45f, 100)]
    [InlineData(9, 0f, 150)]
    public void WordPointsFallWithGuessesAndPayASpeedBonus(int guesses, float seconds, int expected)
    {
        Assert.Equal(expected, WordRunBoard.WordPoints(guesses, seconds));
    }

    [Fact]
    public void AMarathonDealsTheNextWordAfterASolveAndEndsOnAMiss()
    {
        var board = new WordRunBoard();
        board.Load(new[] { "APPLE" }, new HashSet<string> { "APPLE", "PAPAL", "BREAD", "CRANE", "DRIVE", "EARTH", "FLAME" });
        board.StartRun(GameRandom.FromSeed(1));
        Assert.Equal(WordOutcome.Playing, board.Outcome);
        Type(board, "APP");
        Assert.Equal(WordSubmit.TooShort, board.Submit());
        Type(board, "LE");
        Assert.Equal(WordSubmit.Solved, board.Submit());
        Assert.Equal(1, board.WordsSolved);
        Assert.Equal(550, board.Score);
        board.NextWord();
        Assert.Equal(WordOutcome.Playing, board.Outcome);
        Type(board, "ZZZZZ");
        Assert.Equal(WordSubmit.NotAWord, board.Submit());
        for (var attempt = 0; attempt < 5; attempt++)
        {
            board.Backspace();
        }

        var misses = new[] { "PAPAL", "BREAD", "CRANE", "DRIVE", "EARTH", "FLAME" };
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Type(board, misses[attempt]);
            Assert.Equal(WordSubmit.Accepted, board.Submit());
        }

        Type(board, misses[5]);
        Assert.Equal(WordSubmit.Failed, board.Submit());
        Assert.Equal(WordOutcome.Failed, board.Outcome);
        Assert.Equal(7, board.TotalGuesses);
    }

    [Fact]
    public void KeyStatesOnlyEverUpgrade()
    {
        var board = new WordRunBoard();
        board.Load(new[] { "APPLE" }, new HashSet<string> { "APPLE", "PLEAD", "LAPSE" });
        board.StartRun(GameRandom.FromSeed(2));
        Type(board, "PLEAD");
        board.Submit();
        Assert.Equal(WordRunBoard.KeyPresent, board.KeyState('P' - 'A'));
        Assert.Equal(WordRunBoard.KeyAbsent, board.KeyState('D' - 'A'));
        Type(board, "LAPSE");
        board.Submit();
        Assert.Equal(WordRunBoard.KeyCorrect, board.KeyState('P' - 'A'));
        Assert.Equal(WordRunBoard.KeyCorrect, board.KeyState('E' - 'A'));
        Assert.Equal(WordRunBoard.KeyPresent, board.KeyState('L' - 'A'));
    }

    private static void Type(WordRunBoard board, string letters)
    {
        for (var index = 0; index < letters.Length; index++)
        {
            board.TypeLetter(letters[index]);
        }
    }

    private static WordRunBoard Play(int seed, out string trace)
    {
        var board = new WordRunBoard();
        board.Load(ReplayAnswers, new HashSet<string>(ReplayAnswers));
        board.StartRun(GameRandom.FromSeed((ulong)seed));
        var builder = new StringBuilder();
        for (var word = 0; word < ReplayWords; word++)
        {
            builder.Append(board.Answer).Append(' ');
            Type(board, board.Answer);
            Assert.Equal(WordSubmit.Solved, board.Submit());
            board.NextWord();
        }

        trace = builder.ToString();
        return board;
    }
}
