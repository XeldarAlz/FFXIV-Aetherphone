using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.WordRun;

internal enum WordTile : byte
{
    Absent,
    Present,
    Correct,
}

internal enum WordOutcome : byte
{
    Playing,
    Solved,
    Failed,
}

internal enum WordSubmit : byte
{
    TooShort,
    NotAWord,
    Accepted,
    Solved,
    Failed,
}

internal sealed class WordRunBoard
{
    public const int WordLength = 5;
    public const int MaxGuesses = 6;
    public const int LetterCount = 26;
    public const byte KeyUnknown = 0;
    public const byte KeyAbsent = 1;
    public const byte KeyPresent = 2;
    public const byte KeyCorrect = 3;
    public const int SpeedBonusMax = 50;
    public const float FastSeconds = 20f;
    public const int MaxPointsPerWord = 550;
    public static readonly int[] GuessPoints = { 500, 400, 300, 200, 150, 100 };
    private readonly char[] rows = new char[MaxGuesses * WordLength];
    private readonly WordTile[] tiles = new WordTile[MaxGuesses * WordLength];
    private readonly byte[] keyStates = new byte[LetterCount];
    private readonly char[] entry = new char[WordLength];
    private readonly char[] answerChars = new char[WordLength];
    private readonly int[] remaining = new int[LetterCount];
    private readonly HashSet<string> used = new();
    private GameRandom random;
    private string[] answers = Array.Empty<string>();
    private HashSet<string> valid = new();
    public int EntryLength { get; private set; }
    public int RowCount { get; private set; }
    public string Answer { get; private set; } = string.Empty;
    public int Score { get; private set; }
    public int WordsSolved { get; private set; }
    public int TotalGuesses { get; private set; }
    public float RunSeconds { get; private set; }
    public float WordSeconds { get; private set; }
    public int LastWordPoints { get; private set; }
    public int LastWordGuesses { get; private set; }
    public int BestWordGuesses { get; private set; }
    public WordOutcome Outcome { get; private set; } = WordOutcome.Failed;
    public bool Loaded => answers.Length > 0;
    public char Letter(int row, int column) => rows[row * WordLength + column];
    public WordTile Tile(int row, int column) => tiles[row * WordLength + column];
    public char EntryLetter(int index) => entry[index];
    public byte KeyState(int letterIndex) => keyStates[letterIndex];

    public void Load(string[] answerWords, HashSet<string> validWords)
    {
        answers = answerWords;
        valid = validWords;
        used.Clear();
    }

    public void StartRun(GameRandom seededRandom)
    {
        random = seededRandom;
        Score = 0;
        WordsSolved = 0;
        TotalGuesses = 0;
        RunSeconds = 0f;
        LastWordPoints = 0;
        LastWordGuesses = 0;
        BestWordGuesses = 0;
        used.Clear();
        BeginWord();
    }

    public void NextWord()
    {
        BeginWord();
    }

    private void BeginWord()
    {
        RowCount = 0;
        EntryLength = 0;
        WordSeconds = 0f;
        Array.Clear(keyStates);
        Array.Clear(tiles);
        Outcome = WordOutcome.Playing;
        Answer = PickAnswer();
        Answer.CopyTo(0, answerChars, 0, WordLength);
    }

    private string PickAnswer()
    {
        if (answers.Length == 0)
        {
            return "AAAAA";
        }

        if (used.Count >= answers.Length)
        {
            used.Clear();
        }

        string pick;
        do
        {
            pick = answers[random.Next(answers.Length)];
        }
        while (!used.Add(pick));

        return pick;
    }

    public void Tick(float deltaSeconds)
    {
        if (Outcome != WordOutcome.Playing || deltaSeconds <= 0f)
        {
            return;
        }

        RunSeconds += deltaSeconds;
        WordSeconds += deltaSeconds;
    }

    public bool TypeLetter(char letter)
    {
        if (Outcome != WordOutcome.Playing || EntryLength >= WordLength || letter < 'A' || letter > 'Z')
        {
            return false;
        }

        entry[EntryLength++] = letter;
        return true;
    }

    public bool Backspace()
    {
        if (Outcome != WordOutcome.Playing || EntryLength == 0)
        {
            return false;
        }

        EntryLength--;
        return true;
    }

    public WordSubmit Submit()
    {
        if (Outcome != WordOutcome.Playing || EntryLength < WordLength)
        {
            return WordSubmit.TooShort;
        }

        var guess = new string(entry, 0, WordLength);
        if (!valid.Contains(guess))
        {
            return WordSubmit.NotAWord;
        }

        var row = RowCount;
        Array.Copy(entry, 0, rows, row * WordLength, WordLength);
        Evaluate(entry, answerChars, tiles.AsSpan(row * WordLength, WordLength), remaining);
        UpdateKeys(row);
        RowCount++;
        TotalGuesses++;
        EntryLength = 0;
        if (guess == Answer)
        {
            CompleteWord();
            return WordSubmit.Solved;
        }

        if (RowCount >= MaxGuesses)
        {
            Outcome = WordOutcome.Failed;
            return WordSubmit.Failed;
        }

        return WordSubmit.Accepted;
    }

    public void EndRun()
    {
        if (Outcome == WordOutcome.Playing)
        {
            Outcome = WordOutcome.Failed;
        }
    }

    private void CompleteWord()
    {
        LastWordGuesses = RowCount;
        LastWordPoints = WordPoints(RowCount, WordSeconds);
        Score += LastWordPoints;
        WordsSolved++;
        if (BestWordGuesses == 0 || RowCount < BestWordGuesses)
        {
            BestWordGuesses = RowCount;
        }

        Outcome = WordOutcome.Solved;
    }

    private void UpdateKeys(int row)
    {
        for (var column = 0; column < WordLength; column++)
        {
            var letterIndex = rows[row * WordLength + column] - 'A';
            var incoming = tiles[row * WordLength + column] switch
            {
                WordTile.Correct => KeyCorrect,
                WordTile.Present => KeyPresent,
                _ => KeyAbsent,
            };
            if (incoming > keyStates[letterIndex])
            {
                keyStates[letterIndex] = incoming;
            }
        }
    }

    public static void Evaluate(ReadOnlySpan<char> guess, ReadOnlySpan<char> answer, Span<WordTile> result, Span<int> remaining)
    {
        remaining.Clear();
        for (var index = 0; index < WordLength; index++)
        {
            if (guess[index] == answer[index])
            {
                result[index] = WordTile.Correct;
                continue;
            }

            result[index] = WordTile.Absent;
            remaining[answer[index] - 'A']++;
        }

        for (var index = 0; index < WordLength; index++)
        {
            if (result[index] == WordTile.Correct)
            {
                continue;
            }

            var slot = guess[index] - 'A';
            if (remaining[slot] <= 0)
            {
                continue;
            }

            remaining[slot]--;
            result[index] = WordTile.Present;
        }
    }

    public static int SpeedBonus(float wordSeconds) =>
        (int)MathF.Round(SpeedBonusMax * Math.Clamp(1f - wordSeconds / FastSeconds, 0f, 1f));

    public static int WordPoints(int guessesUsed, float wordSeconds)
    {
        var index = Math.Clamp(guessesUsed - 1, 0, GuessPoints.Length - 1);
        return Math.Min(MaxPointsPerWord, GuessPoints[index] + SpeedBonus(wordSeconds));
    }
}
