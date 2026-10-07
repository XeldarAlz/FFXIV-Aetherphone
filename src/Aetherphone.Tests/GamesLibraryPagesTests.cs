using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesLibraryPagesTests
{
    private sealed class FakeGame : IMiniGame
    {
        public FakeGame(string id, GameGenre genre)
        {
            Spec = new GameSpec(id, new LocString(string.Concat("test.", id), id), genre);
        }

        public GameSpec Spec { get; }

        public void Start(in GameStart start)
        {
        }

        public void Close()
        {
        }

        public void Draw(in GameContext context)
        {
        }

        public void Dispose()
        {
        }
    }

    private static readonly IMiniGame[] Games =
    {
        new FakeGame("snake", GameGenre.Arcade),
        new FakeGame("tetris", GameGenre.Puzzle),
        new FakeGame("coil", GameGenre.Puzzle),
        new FakeGame("chess", GameGenre.Tabletop),
        new FakeGame("tempo", GameGenre.Arcade),
    };

    private static GamesLibrary Build() => new(Games, new GameStatsStore(new Configuration()));

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(4, 4, 1)]
    [InlineData(5, 4, 2)]
    [InlineData(67, 4, 17)]
    [InlineData(3, 0, 0)]
    public void TheRowCountRoundsUpToWholeRows(int itemCount, int columns, int expected)
    {
        Assert.Equal(expected, VisibleRows.Count(itemCount, columns));
    }

    [Fact]
    public void AGridInsideTheClipDrawsEveryRow()
    {
        var rows = VisibleRows.Between(5, 100f, 120f, 0f, 1000f);

        Assert.Equal(0, rows.First);
        Assert.Equal(5, rows.End);
    }

    [Fact]
    public void ScrollingSkipsTheRowsAboveAndBelowTheClip()
    {
        var rows = VisibleRows.Between(17, 0f, 120f, 500f, 900f);

        Assert.Equal(4, rows.First);
        Assert.Equal(8, rows.End);
    }

    [Fact]
    public void ARowCutByTheClipEdgeStillDraws()
    {
        var rows = VisibleRows.Between(10, 0f, 100f, 250f, 301f);

        Assert.Equal(2, rows.First);
        Assert.Equal(4, rows.End);
    }

    [Theory]
    [InlineData(2000f, 3000f)]
    [InlineData(-500f, -100f)]
    public void AGridOutsideTheClipDrawsNothing(float clipTop, float clipBottom)
    {
        var rows = VisibleRows.Between(10, 0f, 100f, clipTop, clipBottom);

        Assert.Equal(rows.First, rows.End);
    }

    [Fact]
    public void AnEmptyGridOrAFlatPitchDrawsNothing()
    {
        Assert.Equal(0, VisibleRows.Between(0, 0f, 100f, 0f, 500f).End);
        Assert.Equal(0, VisibleRows.Between(10, 0f, 0f, 0f, 500f).End);
    }

    [Fact]
    public void TheClampedLineCarriesTheRestOfTheHook()
    {
        const string hook = "Tap to flap through the gaps and dodge every pipe on the way";

        Assert.Equal("the gaps and dodge every pipe on the way",
            LineClamp.Remainder(hook, "Tap to flap through", "the gaps and dodge"));
    }

    [Fact]
    public void TheClampedLineStartsAfterTheFirstLineWhenTheWordsRepeat()
    {
        const string hook = "go go go go go";

        Assert.Equal("go go go", LineClamp.Remainder(hook, "go go", "go go"));
    }

    [Fact]
    public void AWrappedLineMissingFromTheHookFallsBackToItself()
    {
        Assert.Equal("elsewhere", LineClamp.Remainder("Line one line two", "Line one", "elsewhere"));
    }

    [Fact]
    public void JustAddedShowsTheNewestWave()
    {
        var library = Build();

        Assert.Equal(library.Latest.ToArray(), GamesApp.ShelfEntries(library, GamesShelf.New).ToArray());
    }

    [Fact]
    public void AGenreShelfShowsThatGenreAndTheAllShelfShowsEverything()
    {
        var library = Build();

        Assert.Equal(library.Genre(GameGenre.Puzzle).ToArray(),
            GamesApp.ShelfEntries(library, GamesShelf.Puzzle).ToArray());
        Assert.Equal(library.Entries.Length, GamesApp.ShelfEntries(library, GamesShelf.All).Length);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void ARowKeepsItsStandardHeightAtTheDefaultTextSize(float scale)
    {
        Assert.Equal(76f * scale, GamesApp.CategoryRowHeightFor(21f * scale, 17f * scale, scale), 3);
    }

    [Fact]
    public void ARowGrowsSoLargeTextNeverSpillsOutOfIt()
    {
        var height = GamesApp.CategoryRowHeightFor(32f, 26f, 1f);

        Assert.Equal(32f + 2f + 26f * 2f + 16f, height, 3);
    }
}
