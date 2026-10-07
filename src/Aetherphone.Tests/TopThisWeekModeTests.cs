using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TopThisWeekModeTests
{
    private static readonly LocString Title = new("t.golf", "Golf");
    private static readonly LocString[] TwoModes = { new("t.full", "Full"), new("t.half", "Half") };

    [Fact]
    public void AGameWithoutModesRanksItsOnlyBoard()
    {
        var spec = new GameSpec("golf", Title, GameGenre.Arcade);

        Assert.Equal(0, GamesApp.TopWeekMode(spec, 0));
        Assert.Equal(0, GamesApp.TopWeekMode(spec, 5));
    }

    [Fact]
    public void TheLastPlayedModeWinsWhenItRanks()
    {
        var spec = new GameSpec("golf", Title, GameGenre.Arcade, modes: TwoModes);

        Assert.Equal(1, GamesApp.TopWeekMode(spec, 1));
        Assert.Equal(1, GamesApp.TopWeekMode(spec, 9));
        Assert.Equal(0, GamesApp.TopWeekMode(spec, -3));
    }

    [Fact]
    public void AnUnrankedLastModeFallsBackToTheFirstRankedOne()
    {
        var spec = new GameSpec("golf", Title, GameGenre.Arcade, modes: TwoModes,
            unrankedModes: new[] { false, true });

        Assert.Equal(0, GamesApp.TopWeekMode(spec, 1));
    }

    [Fact]
    public void AGameWithEveryModeUnrankedHasNoBoard()
    {
        var spec = new GameSpec("golf", Title, GameGenre.Arcade, modes: TwoModes,
            unrankedModes: new[] { true, true });

        Assert.Equal(-1, GamesApp.TopWeekMode(spec, 0));
    }
}
