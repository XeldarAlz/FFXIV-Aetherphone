using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Trivia;

internal enum TriviaState : byte
{
    Ready,
    Asking,
    Revealing,
    Over,
}

internal enum TriviaKind : byte
{
    IconToName,
    NameToIcon,
}

internal enum TriviaCategory : byte
{
    All,
    Mounts,
    Minions,
    Actions,
    Emotes,
}

internal sealed class TriviaBoard
{
    public const int Options = 4;
    public const float QuestionSeconds = 10f;
    public const float RevealSeconds = 0.8f;
    public const int StartLives = 3;
    public const int BasePoints = 10;
    public const int MinimumPool = 24;
    private const float ComboWindowSeconds = QuestionSeconds + RevealSeconds + 1f;
    private const int BuildAttempts = 128;
    private static readonly TriviaCategory[] Pickable =
    {
        TriviaCategory.All, TriviaCategory.Mounts, TriviaCategory.Minions, TriviaCategory.Actions,
        TriviaCategory.Emotes,
    };

    private static readonly TriviaCategory[] Concrete =
    {
        TriviaCategory.Mounts, TriviaCategory.Minions, TriviaCategory.Actions, TriviaCategory.Emotes,
    };

    private readonly ITriviaSource source;
    private readonly NamedIcon[] options = new NamedIcon[Options];
    private readonly TriviaCategory[] available = new TriviaCategory[Concrete.Length];
    private GameRandom random;
    private ComboMeter combo = new(ComboWindowSeconds);
    private float revealTimer;

    public TriviaBoard(ITriviaSource source)
    {
        this.source = source;
    }

    public TriviaState State { get; private set; } = TriviaState.Ready;

    public TriviaKind Kind { get; private set; }

    public TriviaCategory Category { get; private set; } = TriviaCategory.All;

    public int CorrectIndex { get; private set; }

    public int PickedIndex { get; private set; } = -1;

    public int Score { get; private set; }

    public int Lives { get; private set; } = StartLives;

    public int Asked { get; private set; }

    public int Correct { get; private set; }

    public int BestCombo { get; private set; }

    public float TimeLeft { get; private set; }

    public int LastPoints { get; private set; }

    public bool HasQuestion { get; private set; }

    public ComboMeter Combo => combo;

    public int Multiplier => Math.Max(1, combo.Multiplier);

    public float RevealProgress =>
        State == TriviaState.Revealing ? 1f - Math.Clamp(revealTimer / RevealSeconds, 0f, 1f) : 0f;

    public static int PickableCount => Pickable.Length;

    public static TriviaCategory PickableAt(int index) => Pickable[Math.Clamp(index, 0, Pickable.Length - 1)];

    public NamedIcon Option(int index) => options[index];

    public NamedIcon CorrectEntry => options[CorrectIndex];

    public static LocString LabelOf(TriviaCategory category)
    {
        switch (category)
        {
            case TriviaCategory.Mounts:
                return L.Games.CategoryMounts;
            case TriviaCategory.Minions:
                return L.Games.CategoryMinions;
            case TriviaCategory.Actions:
                return L.Games.CategoryActions;
            case TriviaCategory.Emotes:
                return L.Games.CategoryEmotes;
            default:
                return L.Games.CategoryAll;
        }
    }

    public bool IsAvailable(TriviaCategory category)
    {
        if (category != TriviaCategory.All)
        {
            return source.PoolOf(category).Length >= MinimumPool;
        }

        for (var index = 0; index < Concrete.Length; index++)
        {
            if (source.PoolOf(Concrete[index]).Length >= MinimumPool)
            {
                return true;
            }
        }

        return false;
    }

    public void Reset()
    {
        State = TriviaState.Ready;
        Score = 0;
        Lives = StartLives;
        Asked = 0;
        Correct = 0;
        BestCombo = 0;
        LastPoints = 0;
        PickedIndex = -1;
        TimeLeft = 0f;
        revealTimer = 0f;
        HasQuestion = false;
        combo.Reset();
    }

    public bool Start(TriviaCategory category, GameRandom seededRandom)
    {
        if (!IsAvailable(category))
        {
            return false;
        }

        Reset();
        random = seededRandom;
        Category = category;
        HasQuestion = BuildQuestion();
        if (!HasQuestion)
        {
            return false;
        }

        TimeLeft = QuestionSeconds;
        State = TriviaState.Asking;
        return true;
    }

    public bool Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return false;
        }

        if (State == TriviaState.Revealing)
        {
            combo.Update(deltaSeconds);
            revealTimer -= deltaSeconds;
            if (revealTimer <= 0f)
            {
                Advance();
            }

            return false;
        }

        if (State != TriviaState.Asking)
        {
            return false;
        }

        combo.Update(deltaSeconds);
        TimeLeft -= deltaSeconds;
        if (TimeLeft > 0f)
        {
            return false;
        }

        TimeLeft = 0f;
        PickedIndex = -1;
        LastPoints = 0;
        combo.Reset();
        Lives--;
        revealTimer = RevealSeconds;
        State = TriviaState.Revealing;
        return true;
    }

    public bool Answer(int index)
    {
        if (State != TriviaState.Asking || index < 0 || index >= Options)
        {
            return false;
        }

        PickedIndex = index;
        revealTimer = RevealSeconds;
        State = TriviaState.Revealing;
        if (index != CorrectIndex)
        {
            combo.Reset();
            LastPoints = 0;
            Lives--;
            return false;
        }

        var multiplier = combo.Hit();
        var speedBonus = (int)MathF.Ceiling(TimeLeft);
        LastPoints = (BasePoints + speedBonus) * multiplier;
        Score += LastPoints;
        Correct++;
        BestCombo = Math.Max(BestCombo, combo.Count);
        return true;
    }

    private void Advance()
    {
        Asked++;
        if (Lives <= 0)
        {
            State = TriviaState.Over;
            return;
        }

        PickedIndex = -1;
        HasQuestion = BuildQuestion();
        if (!HasQuestion)
        {
            State = TriviaState.Over;
            return;
        }

        TimeLeft = QuestionSeconds;
        State = TriviaState.Asking;
    }

    private bool BuildQuestion()
    {
        var category = Category == TriviaCategory.All ? RollCategory() : Category;
        var ids = source.PoolOf(category);
        if (ids.Length < MinimumPool)
        {
            return false;
        }

        var filled = 0;
        var attempts = 0;
        while (filled < Options && attempts < BuildAttempts)
        {
            attempts++;
            var entry = source.EntryOf(category, ids[random.Next(ids.Length)]);
            if (!entry.IsValid || Duplicate(entry, filled))
            {
                continue;
            }

            options[filled] = entry;
            filled++;
        }

        if (filled < Options)
        {
            return false;
        }

        CorrectIndex = random.Next(Options);
        Kind = random.Next(2) == 0 ? TriviaKind.IconToName : TriviaKind.NameToIcon;
        return true;
    }

    private TriviaCategory RollCategory()
    {
        var count = 0;
        for (var index = 0; index < Concrete.Length; index++)
        {
            if (source.PoolOf(Concrete[index]).Length < MinimumPool)
            {
                continue;
            }

            available[count] = Concrete[index];
            count++;
        }

        if (count == 0)
        {
            return TriviaCategory.Mounts;
        }

        return available[random.Next(count)];
    }

    private bool Duplicate(NamedIcon entry, int filled)
    {
        for (var index = 0; index < filled; index++)
        {
            if (options[index].IconId == entry.IconId ||
                string.Equals(options[index].Name, entry.Name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
