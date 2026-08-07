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
    private const float RevealSeconds = 0.8f;
    private const int StartLives = 3;
    private const int MinimumPool = 24;
    private const int MaxMultiplier = 5;
    private const int BasePoints = 10;
    private static readonly TriviaCategory[] Pickable =
    {
        TriviaCategory.All, TriviaCategory.Mounts, TriviaCategory.Minions, TriviaCategory.Actions,
        TriviaCategory.Emotes,
    };

    private static readonly TriviaCategory[] Concrete =
    {
        TriviaCategory.Mounts, TriviaCategory.Minions, TriviaCategory.Actions, TriviaCategory.Emotes,
    };

    private readonly GameData gameData;
    private readonly NamedIcon[] options = new NamedIcon[Options];
    private readonly TriviaCategory[] available = new TriviaCategory[Concrete.Length];
    private readonly Random random = new();
    private float revealTimer;
    public TriviaState State { get; private set; } = TriviaState.Ready;
    public TriviaKind Kind { get; private set; }
    public TriviaCategory Category { get; private set; } = TriviaCategory.All;
    public int CorrectIndex { get; private set; }
    public int PickedIndex { get; private set; } = -1;
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Lives { get; private set; } = StartLives;
    public int Asked { get; private set; }
    public float TimeLeft { get; private set; }
    public int LastPoints { get; private set; }
    public bool HasQuestion { get; private set; }
    public int Multiplier => 1 + Math.Min(MaxMultiplier - 1, Combo / 3);
    public static int PickableCount => Pickable.Length;
    public static TriviaCategory PickableAt(int index) => Pickable[index];
    public NamedIcon Option(int index) => options[index];
    public NamedIcon Correct => options[CorrectIndex];

    public TriviaBoard(GameData gameData)
    {
        this.gameData = gameData;
    }

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
            return PoolOf(category).Length >= MinimumPool;
        }

        for (var index = 0; index < Concrete.Length; index++)
        {
            if (PoolOf(Concrete[index]).Length >= MinimumPool)
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
        Combo = 0;
        Lives = StartLives;
        Asked = 0;
        LastPoints = 0;
        PickedIndex = -1;
        TimeLeft = 0f;
        revealTimer = 0f;
        HasQuestion = false;
    }

    public bool Start(TriviaCategory category)
    {
        if (!IsAvailable(category))
        {
            return false;
        }

        Reset();
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
        if (State == TriviaState.Revealing)
        {
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

        TimeLeft -= deltaSeconds;
        if (TimeLeft > 0f)
        {
            return false;
        }

        TimeLeft = 0f;
        PickedIndex = -1;
        LastPoints = 0;
        Combo = 0;
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
            Combo = 0;
            LastPoints = 0;
            Lives--;
            return false;
        }

        Combo++;
        var speedBonus = (int)MathF.Ceiling(TimeLeft);
        LastPoints = (BasePoints + speedBonus) * Multiplier;
        Score += LastPoints;
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
        var ids = PoolOf(category);
        if (ids.Length < MinimumPool)
        {
            return false;
        }

        var filled = 0;
        var attempts = 0;
        while (filled < Options && attempts < 128)
        {
            attempts++;
            var entry = EntryOf(category, ids[random.Next(ids.Length)]);
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
            if (PoolOf(Concrete[index]).Length < MinimumPool)
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

    private uint[] PoolOf(TriviaCategory category)
    {
        switch (category)
        {
            case TriviaCategory.Mounts:
                return gameData.CollectableMountIds();
            case TriviaCategory.Minions:
                return gameData.CollectableMinionIds();
            case TriviaCategory.Actions:
                return gameData.TriviaActionIds();
            case TriviaCategory.Emotes:
                return gameData.TriviaEmoteIds();
            default:
                return Array.Empty<uint>();
        }
    }

    private NamedIcon EntryOf(TriviaCategory category, uint rowId)
    {
        switch (category)
        {
            case TriviaCategory.Mounts:
                return gameData.MountEntry(rowId);
            case TriviaCategory.Minions:
                return gameData.MinionEntry(rowId);
            case TriviaCategory.Actions:
                return gameData.ActionEntry(rowId);
            case TriviaCategory.Emotes:
                return gameData.EmoteEntry(rowId);
            default:
                return default;
        }
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
