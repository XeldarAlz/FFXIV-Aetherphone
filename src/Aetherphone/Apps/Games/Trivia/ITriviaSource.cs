using Aetherphone.Core.Game;

namespace Aetherphone.Apps.Games.Trivia;

internal interface ITriviaSource
{
    uint[] PoolOf(TriviaCategory category);

    NamedIcon EntryOf(TriviaCategory category, uint rowId);
}

internal sealed class GameDataTriviaSource : ITriviaSource
{
    private readonly GameData gameData;

    public GameDataTriviaSource(GameData gameData)
    {
        this.gameData = gameData;
    }

    public uint[] PoolOf(TriviaCategory category)
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

    public NamedIcon EntryOf(TriviaCategory category, uint rowId)
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
}
